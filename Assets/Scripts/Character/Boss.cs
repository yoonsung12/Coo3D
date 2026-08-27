using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 페이즈 진행을 담당한다.
// 체력 관리/피격 연출/사망 연출은 Enemy를 그대로 재사용하고, 여기서는 보스 전용 로직만 얹는다:
// - HP 75/50/25/10%마다 순서대로 봄/여름/가을/겨울 계절 패턴 진입 신호를 1회씩 보낸다.
// - 계절 패턴이 진행되는 동안은 무적 상태가 된다 (TakeDamage를 재정의해서 막는다).
// - HP 50% 이하부터 EnemyMovement로 플레이어를 향해 추적 이동을 시작한다.
// 실제 계절 패턴(꽃가루/비구름/은행/고드름)의 연출과 로직은 각 패턴 전용 컨트롤러가
// OnPatternTriggered 이벤트를 구독해서 구현한다 (이번 단계에서는 아직 없음).
[RequireComponent(typeof(EnemyMovement))]
public class Boss : Enemy
{
    public enum SeasonPattern { Spring, Summer, Autumn, Winter }

    [Title("페이즈 설정")]
    [SerializeField, LabelText("계절 패턴 진입 체력 비율 (봄→여름→가을→겨울 순서)")]
    private float[] patternThresholds = { 0.75f, 0.5f, 0.25f, 0.1f };
    // HealthRatio가 이 값 아래로 내려갈 때마다 배열 순서대로(=봄/여름/가을/겨울 순서로) 패턴이 1회씩 발동한다.

    [SerializeField, LabelText("추적 이동 시작 체력 비율")]
    private float moveStartHealthRatio = 0.5f;
    // 이 비율보다 체력이 높을 때는 보스가 제자리에 고정된다.

    [SerializeField, LabelText("파훼 성공 후 무방비 지속 시간")]
    private float vulnerableWindowDuration = 2.5f;
    // 계절 패턴을 파훼하면 이 시간 동안 무적이 풀린 채로 유지된 뒤 기본 패턴으로 돌아간다.

    [Title("연결")]
    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;
    // Inspector에서 씬의 Player 오브젝트를 연결한다. 추적 이동 방향 계산에 사용한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("무적 상태")]
    public bool IsInvincible { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("다음에 발동할 계절 패턴 순번")]
    private int _nextPatternIndex;

    [ReadOnly, ShowInInspector, LabelText("현재 진행 중인 계절 패턴")]
    private SeasonPattern? _activePattern;

    // 계절 패턴이 발동될 때 발행된다. 각 패턴 전용 컨트롤러가 구독해서 실제 연출/로직을 시작한다.
    public event Action<SeasonPattern> OnPatternTriggered;

    // 파훼 성공 후 무방비 시간이 끝나고 기본 패턴으로 돌아올 때 발행된다.
    public event Action OnPatternEnded;

    private EnemyMovement _movement;
    private float _vulnerableTimer;
    private bool _isInVulnerableWindow;

    // Enemy.Awake()는 private이라 여기서 재정의할 수 없으므로, Boss 전용 초기화는 Start()에서 한다.
    private void Start()
    {
        _movement = GetComponent<EnemyMovement>();
    }

    private void OnEnable()
    {
        OnDamageTaken += HandleDamageForPatternCheck;
    }

    private void OnDisable()
    {
        OnDamageTaken -= HandleDamageForPatternCheck;
    }

    private void Update()
    {
        if (IsDead) return;

        TickVulnerableWindow();
        HandleChaseMovement();
    }

    // 무적일 때는 데미지를 아예 받지 않는다. base.TakeDamage를 호출하지 않으므로
    // Enemy의 체력 감소/피격 연출/OnDamageTaken 이벤트 발행이 전부 일어나지 않는다.
    public override void TakeDamage(float amount)
    {
        if (IsInvincible) return;
        base.TakeDamage(amount);
    }

    // 데미지를 받아 체력이 줄어들 때마다 다음 계절 패턴 임계값을 넘었는지 확인한다.
    private void HandleDamageForPatternCheck(float amount) => CheckPatternThreshold();

    // 한 번의 큰 피해로 임계값을 두 개 이상 동시에 넘는 경우(예: 테스트용 대량 데미지),
    // 첫 번째 패턴만 발동하고 두 번째는 다음 피격까지 미뤄지면 어색하므로,
    // 패턴을 파훼해서 무적이 풀리는 시점에도 같은 확인을 다시 해서 바로 이어서 발동시킨다.
    private void CheckPatternThreshold()
    {
        if (_activePattern != null) return; // 이미 패턴이 진행 중이면 다음 임계값을 새로 확인하지 않는다.
        if (_nextPatternIndex >= patternThresholds.Length) return; // 4개 패턴을 전부 소진했다.

        if (HealthRatio <= patternThresholds[_nextPatternIndex])
            TriggerNextPattern();
    }

    private void TriggerNextPattern()
    {
        var pattern = (SeasonPattern)_nextPatternIndex;
        _nextPatternIndex++;
        _activePattern = pattern;
        IsInvincible = true;

        Debug.Log($"[Boss] 계절 패턴 발동: {pattern} (HP {HealthRatio:P0})");
        OnPatternTriggered?.Invoke(pattern);
    }

    // 계절 패턴을 실제로 파훼했을 때 각 패턴 컨트롤러가 호출한다.
    // 필드 소멸은 패턴 컨트롤러 쪽 책임이고, Boss는 무적 해제 + 무방비 타이머만 관리한다.
    public void NotifyPatternSolved()
    {
        if (_activePattern == null) return;

        _activePattern = null;
        IsInvincible = false;
        _isInVulnerableWindow = true;
        _vulnerableTimer = vulnerableWindowDuration;

        CheckPatternThreshold();
    }

    private void TickVulnerableWindow()
    {
        if (!_isInVulnerableWindow) return;

        _vulnerableTimer -= Time.deltaTime;
        if (_vulnerableTimer > 0f) return;

        _isInVulnerableWindow = false;
        OnPatternEnded?.Invoke();
    }

    // HP 50% 이하부터, 계절 패턴이 진행 중이 아닐 때만 플레이어를 향해 좌우로 추적한다.
    // 패턴이 진행 중일 때는 각 패턴 컨트롤러가 보스의 이동을 직접 담당하므로 여기서는 개입하지 않는다.
    private void HandleChaseMovement()
    {
        if (_activePattern != null) return;
        if (playerTransform == null || HealthRatio > moveStartHealthRatio)
        {
            _movement.Move(0f);
            return;
        }

        float dx = playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.1f)
        {
            _movement.Move(0f);
            return;
        }

        _movement.Move(Mathf.Sign(dx));
    }

    [Button("다음 계절 패턴 강제 발동 (테스트)")]
    private void TestTriggerNextPattern()
    {
        if (_nextPatternIndex >= patternThresholds.Length)
        {
            Debug.Log("[Boss] 이미 4개 패턴을 전부 발동했다.");
            return;
        }

        TriggerNextPattern();
    }

    [Button("현재 패턴 파훼 처리 (테스트)")]
    private void TestSolvePattern() => NotifyPatternSolved();
}
