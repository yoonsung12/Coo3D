using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 페이즈 진행을 담당한다.
// 체력 관리/피격 연출/사망 연출은 Enemy를 그대로 재사용하고, 여기서는 보스 전용 로직만 얹는다:
// - HP 75/50/25/10%마다 순서대로 봄/여름/가을/겨울 계절 패턴 진입 신호를 1회씩 보낸다.
// - 계절 패턴이 진행되는 동안은 무적 상태가 된다 (TakeDamage를 재정의해서 막는다).
// - 평상시 이동(발판 사이를 오가는 비행)은 BossFlightMovement가 별도로 담당하고,
//   Boss는 그 결과(IsFlying)만 참조해서 비행 중엔 공격을 쉰다.
// 실제 계절 패턴(꽃가루/비구름/은행/고드름)의 연출과 로직은 각 패턴 전용 컨트롤러가
// OnPatternTriggered 이벤트를 구독해서 구현한다.
// 평범한 패턴(부리 급강하 찍기/깃털 부채꼴 발사/날개 돌풍)은 BossBasicPattern이 별도로 담당하고,
// CanBasicAttack(무방비 시간 아님 + 비행 중 아님 + 계절 패턴 진행 중 아님)만 이 클래스에서 판정해 넘겨준다.
// 계절 패턴(봄/여름/가을/겨울) 진행 중(무적 상태)에는 평범한 패턴을 봉인한다 — 계절 패턴에만 집중하도록.
[RequireComponent(typeof(EnemyMovement))]
public class Boss : Enemy
{
    public enum SeasonPattern { Spring, Summer, Autumn, Winter }

    [Title("페이즈 설정")]
    [SerializeField, LabelText("계절 패턴 진입 체력 비율 (봄→여름→가을→겨울 순서)")]
    private float[] patternThresholds = { 0.75f, 0.5f, 0.25f, 0.1f };
    // HealthRatio가 이 값 아래로 내려갈 때마다 배열 순서대로(=봄/여름/가을/겨울 순서로) 패턴이 1회씩 발동한다.

    [SerializeField, LabelText("파훼 성공 후 무방비 지속 시간")]
    private float vulnerableWindowDuration = 2.5f;
    // 계절 패턴을 파훼하면 이 시간 동안 무적이 풀린 채로 유지된 뒤 기본 패턴으로 돌아간다.

    [Title("연결")]
    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;
    // Inspector에서 씬의 Player 오브젝트를 연결한다. 추적 이동 방향 계산에 사용한다.

    // BossBasicPattern 등 외부 컴포넌트가 플레이어 위치를 참조하기 위한 공개 접근자다.
    public Transform PlayerTransform => playerTransform;

    [Title("착지 지점")]
    [SerializeField, LabelText("착지 가능 지점 목록")]
    private List<LandingPoint> landingPoints = new List<LandingPoint>
    {
        new LandingPoint { label = "Floor", position = new Vector2(0f, 1.0f) },
        new LandingPoint { label = "Platform_MidLeft", position = new Vector2(-8.5f, 4.1f) },
        new LandingPoint { label = "Platform_MidRight", position = new Vector2(8.5f, 4.1f) },
        new LandingPoint { label = "Platform_TopCenter", position = new Vector2(0f, 7.1f) },
        new LandingPoint { label = "Platform_TopFarLeft", position = new Vector2(-19.0f, 7.7f) },
        new LandingPoint { label = "Platform_TopFarRight", position = new Vector2(19.0f, 7.7f) },
    };
    // 봄 패턴 돌진(BossSpringPattern)과 평상시 비행 이동(BossFlightMovement)이 공통으로 쓰는
    // 착지 지점 목록이다. 각 좌표는 BossArena 씬의 바닥/발판 윗면 + 보스 몸(BoxCollider 반높이 1.0,
    // 보스 크기를 2배로 키우면서 0.5→1.0으로 변경됨)을 더한 착지 높이다.

    // Inspector에서 착지 지점을 알아보기 쉽게 이름표를 붙이기 위한 자료구조다.
    [System.Serializable]
    public class LandingPoint
    {
        [LabelText("이름")]
        public string label;

        [LabelText("좌표 (X, Y)")]
        public Vector2 position;
    }

    // BossSpringPattern/BossFlightMovement가 읽기 전용으로 참조하는 공개 접근자다.
    public IReadOnlyList<LandingPoint> LandingPoints => landingPoints;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("무적 상태")]
    public bool IsInvincible { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("비행 이동 중")]
    public bool IsFlying { get; private set; }
    // BossFlightMovement가 SetFlying()으로 갱신한다. 비행 중엔 평범한 패턴(BossBasicPattern)이 공격을 쉰다.

    // BossBasicPattern이 매 프레임 참조해서 지금 평범한 패턴을 시도해도 되는지 판단하는 접근자다.
    // 무방비 시간(파훼 직후), 비행 이동 중, 계절 패턴(봄/여름/가을/겨울) 진행 중(무적 상태)에는
    // 공격을 쉰다 — 계절 패턴 동안에는 평범한 패턴을 봉인해서 계절 패턴에만 집중하게 한다.
    public bool CanBasicAttack => !_isInVulnerableWindow && !IsFlying && !IsInvincible;

    [ReadOnly, ShowInInspector, LabelText("평범한 패턴 공격 중")]
    public bool IsBasicAttacking { get; private set; }
    // BossBasicPattern이 공격을 시작/종료할 때 갱신한다. 부리 급강하 찍기처럼 보스가 직접 이동하는
    // 공격과 BossFlightMovement의 평상시 비행 이동이 같은 Rigidbody를 동시에 제어하지 않도록,
    // BossFlightMovement가 이 값을 참조해서 공격 중엔 새로 비행을 시작하지 않는다.

    // BossBasicPattern이 공격 시작/종료 시 호출해서 IsBasicAttacking 상태를 갱신한다.
    public void SetBasicAttacking(bool attacking) => IsBasicAttacking = attacking;

    private int _nextPatternIndex;

    private SeasonPattern? _activePattern;

    // 계절 이름을 한글로 표시하기 위한 표다. SeasonPattern enum 순서(봄/여름/가을/겨울)와 맞춰둔다.
    private static readonly string[] PatternKoreanNames = { "봄", "여름", "가을", "겨울" };

    // Inspector에서 "봄 계절패턴 대기 중"처럼 한눈에 알아볼 수 있게 보여주기 위한 계산 값이다.
    // 실제 로직은 _nextPatternIndex를 그대로 쓰고, 이 프로퍼티는 표시 전용이다.
    [ReadOnly, ShowInInspector, LabelText("다음 계절 패턴")]
    private string NextPatternDisplay =>
        _nextPatternIndex < patternThresholds.Length
            ? $"{PatternKoreanNames[_nextPatternIndex]} 계절패턴 대기 중"
            : "모든 계절 패턴 소진";

    [ReadOnly, ShowInInspector, LabelText("현재 진행 중인 계절 패턴")]
    private string ActivePatternDisplay =>
        _activePattern != null
            ? $"{PatternKoreanNames[(int)_activePattern.Value]} 계절패턴 발동 중"
            : "없음";

    // 계절 패턴이 발동될 때 발행된다. 각 패턴 전용 컨트롤러가 구독해서 실제 연출/로직을 시작한다.
    public event Action<SeasonPattern> OnPatternTriggered;

    // 파훼 성공 후 무방비 시간이 끝나고 기본 패턴으로 돌아올 때 발행된다.
    public event Action OnPatternEnded;

    private float _vulnerableTimer;
    private bool _isInVulnerableWindow;

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
    }

    // 무적일 때는 데미지를 아예 받지 않는다. base.TakeDamage를 호출하지 않으므로
    // Enemy의 체력 감소/피격 연출/OnDamageTaken 이벤트 발행이 전부 일어나지 않는다.
    public override void TakeDamage(float amount)
    {
        if (IsInvincible) return;
        base.TakeDamage(amount);
    }

    // 계절 패턴 무적 중에도 예외적으로 통과시켜야 하는 데미지 전용 통로다.
    // 봄 패턴(꽃가루 트레일 폭발)처럼 "패턴을 직접 파훼했을 때"만 호출해야 하며,
    // IsInvincible 체크를 건너뛰고 곧바로 base.TakeDamage()를 호출한다.
    public void ApplyPatternDamage(float amount) => base.TakeDamage(amount);

    // BossFlightMovement가 비행을 시작/종료할 때 호출해서 IsFlying 상태를 갱신한다.
    // 비행 중엔 BossBasicPattern이 공격을 쉬도록 하기 위한 용도다.
    public void SetFlying(bool flying) => IsFlying = flying;

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
