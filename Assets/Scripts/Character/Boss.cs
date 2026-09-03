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
// 기본 패턴(근접/원거리 공격)은 계절 패턴 진행 중(무적 상태)에도 계속 시도한다 — 스펙 문서 기준
// "패턴 진행 중에도 회피 압박을 유지하기 위해 공격을 멈추지 않는다".
[RequireComponent(typeof(EnemyMovement), typeof(EnemyCombat))]
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

    [Title("원거리 공격 설정")]
    [SerializeField, LabelText("투사체 프리팹")]
    private BossProjectile projectilePrefab;
    // Inspector에서 Assets/Prefabs/Boss/BossProjectile.prefab을 연결한다.

    [SerializeField, LabelText("발사 위치")]
    private Transform projectileSpawnPoint;
    // Inspector에서 Boss 자식의 ProjectileSpawnPoint를 연결한다.

    [SerializeField, LabelText("투사체 속도")]
    private float projectileSpeed = 10f;

    [SerializeField, LabelText("투사체 데미지")]
    private float projectileDamage = 20f;
    // Player 체력 하트(5칸, 하트 1개=20)를 기준으로 정확히 1칸이 깎이도록 맞춘 값이다.

    [SerializeField, LabelText("원거리 공격 쿨다운")]
    private float rangedAttackCooldown = 2f;

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
    // BossFlightMovement가 SetFlying()으로 갱신한다. 비행 중엔 HandleBasicAttack이 공격을 쉰다.

    [ReadOnly, ShowInInspector, LabelText("다음에 발동할 계절 패턴 순번")]
    private int _nextPatternIndex;

    [ReadOnly, ShowInInspector, LabelText("현재 진행 중인 계절 패턴")]
    private SeasonPattern? _activePattern;

    // 계절 패턴이 발동될 때 발행된다. 각 패턴 전용 컨트롤러가 구독해서 실제 연출/로직을 시작한다.
    public event Action<SeasonPattern> OnPatternTriggered;

    // 파훼 성공 후 무방비 시간이 끝나고 기본 패턴으로 돌아올 때 발행된다.
    public event Action OnPatternEnded;

    private EnemyMovement _movement;
    private EnemyCombat _combat;
    private float _vulnerableTimer;
    private bool _isInVulnerableWindow;
    private float _lastRangedAttackTime = -999f;

    // Enemy.Awake()는 private이라 여기서 재정의할 수 없으므로, Boss 전용 초기화는 Start()에서 한다.
    private void Start()
    {
        _movement = GetComponent<EnemyMovement>();
        _combat = GetComponent<EnemyCombat>();
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
        HandleBasicAttack();
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
    // 비행 중엔 HandleBasicAttack이 공격을 쉬도록 하기 위한 용도다.
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

    // 기본 패턴(근접/원거리 공격)이다. 계절 패턴 진행 중(무적 상태)에도 계속 시도해서
    // 회피 압박을 유지하지만, 파훼 직후 무방비 시간과 비행 이동 중에는 공격하지 않는다.
    private void HandleBasicAttack()
    {
        if (_isInVulnerableWindow) return;
        if (IsFlying) return;
        if (playerTransform == null) return;

        float dx = playerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);
        float distance = Mathf.Abs(dx);

        _movement.FaceDirection(dir);

        if (distance <= _combat.AttackRange)
        {
            if (_combat.CanAttack)
                _combat.StartAttack();
        }
        else
        {
            TryFireProjectile(dir);
        }
    }

    // 원거리 공격 쿨다운이 지났으면 투사체를 발사한다.
    private void TryFireProjectile(float dir)
    {
        if (projectilePrefab == null || projectileSpawnPoint == null) return;
        if (Time.time - _lastRangedAttackTime < rangedAttackCooldown) return;

        _lastRangedAttackTime = Time.time;

        // projectileSpawnPoint는 EnemyAttackHitbox(AttackPoint)와 똑같은 이유로 회전에 영향을 받는다:
        // Boss가 좌우로 방향을 바꿀 때 FaceDirection이 오브젝트를 Y축으로 회전시키는데,
        // 그러면 자식의 로컬 X 오프셋이 월드 Z축으로 밀려버린다(EnemyMovement 버그 수정과 동일 원인).
        // 그래서 회전된 world position을 그대로 쓰지 않고, 로컬 오프셋 크기 + 현재 방향(dir)으로
        // 발사 위치를 직접 계산한다.
        Vector3 baseOffset = projectileSpawnPoint.localPosition;
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Abs(baseOffset.x) * dir, baseOffset.y, baseOffset.z);

        BossProjectile projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        projectile.Launch(new Vector3(dir, 0f, 0f), projectileSpeed, projectileDamage);
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

    [Button("원거리 공격 테스트")]
    private void TestFireProjectile() => TryFireProjectile(_movement != null ? _movement.FacingDir : 1f);
}
