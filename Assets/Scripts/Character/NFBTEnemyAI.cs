using Sirenix.OdinInspector;
using UnityEngine;

// 적의 행동을 총괄하는 AI다. 2D 원작의 NFBT(퍼지클러스터링+강화학습 기반 전술 선택) 구조를
// 3D로 단계적으로 포팅했다.
// Phase 1: 시야로 감지하면 쫓아가서 사거리 안이면 공격
// Phase 2: Player를 못 볼 때는 정해진 범위 안에서 좌우로 순찰
// Phase 3: 후퇴(Evade/Recover) 중 벽/낭떠러지에 막히면 궁지몰림 발악(점프공격)
// Phase 4: Player의 공격이 막 끝나는 순간(빈틈)을 포착하는 반격(Counter)
// Phase 5(현재): NPCFuzzyRL(퍼지클러스터링+RBFNN 강화학습)이 Chase/Attack, Evade/Recover, Counter
// 세 전술 중 어느 것을 쓸지 실시간으로 학습해 선택한다. 궁지몰림만은 2D 원작과 동일하게
// 학습과 무관한 강제 상태로 남겨둔다(후퇴하다 막다른 곳에 몰리면 무조건 발악).
[RequireComponent(typeof(EnemyVision), typeof(EnemyMovement), typeof(EnemyCombat))]
[RequireComponent(typeof(Enemy), typeof(CombatStatsTracker))]
public class NFBTEnemyAI : MonoBehaviour
{
    [Title("추적 설정")]
    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;
    // Inspector에서 씬의 Player 오브젝트를 연결한다.

    [SerializeField, LabelText("Player Health")]
    private PlayerHealth playerHealth;
    // Player에게 입힌 피해를 감지해 학습기에 보상(+1.0)을 전달하기 위해 연결한다.

    [SerializeField, LabelText("추적 포기 거리")]
    private float chaseAbandonRange = 15f;
    // Player를 감지한 뒤에도 이 거리보다 멀어지면 추적을 포기하고 순찰로 돌아간다.

    [Title("순찰 설정")]
    [SerializeField, LabelText("순찰 범위 절반 너비")]
    private float patrolHalfWidth = 3f;
    // 시작 위치를 중심으로 좌우 이 거리만큼 왕복한다.

    [Title("전술 학습 설정")]
    [SerializeField, LabelText("전술 재계산 주기(초)")]
    private float tacticsUpdateInterval = 2f;
    // 이 시간마다 최근 전투 통계를 학습기에 반영하고 전술(ActiveBranch)을 다시 계산한다.

    [SerializeField, LabelText("거리 정규화 기준")]
    private float maxRelevantDistance = 10f;
    // 학습기 입력값(gameState)의 거리를 0~1로 정규화하는 기준 거리다.

    [Title("후퇴(Evade/Recover) 설정")]
    [SerializeField, LabelText("안전 거리")]
    private float safeDistance = 4f;
    // 이 거리 이상 Player와 떨어지면 후퇴를 멈추고 대기한다.

    [SerializeField, LabelText("최소 후퇴 시간")]
    private float minRetreatDuration = 1.5f;
    // 후퇴를 시작한 뒤 이 시간이 지나야 벽/낭떠러지를 궁지몰림 조건으로 판정한다
    // (후퇴 시작 직후 바로 궁지몰림으로 오판정되는 것을 막기 위함).

    [Title("궁지몰림 발악 설정")]
    [SerializeField, LabelText("발악 지속 시간")]
    private float corneredDuration = 4f;
    // 후퇴하다 벽/낭떠러지에 막히면 이 시간 동안 Player 쪽으로 돌진하며 점프공격을 반복한다.

    [Title("반격(Counter) 설정")]
    [SerializeField, LabelText("Player SwordAttack")]
    private SwordAttack playerSwordAttack;
    // Player의 공격 시작/종료 타이밍을 읽기 위해 연결한다.

    [SerializeField, LabelText("반격 돌진 속도 배율")]
    private float counterRushSpeedMultiplier = 1.6f;
    // 평소 추적 속도보다 빠르게 돌진해 반격다운 급박함을 준다.

    [Title("도약공격 (후퇴 대체)")]
    [InfoBox("켜면 학습기가 Evade/Recover 전술을 고를 때 도망 대신 앞으로 도약하며 공격한다. 비둘기처럼 도망가지 않는 적에서만 켠다.")]
    [SerializeField, LabelText("후퇴 대신 도약공격")]
    private bool replaceEvadeWithLeap = false;
    // 기본값은 꺼짐이라 이 옵션을 켜지 않은 다른 적은 기존 후퇴 행동을 그대로 유지한다.
    // 켜면 후퇴를 하지 않으므로 "후퇴하다 막히면 발동하는" 궁지몰림 발악도 발동하지 않는다.

    [SerializeField, LabelText("도약 최소 거리"), ShowIf(nameof(replaceEvadeWithLeap))]
    private float leapMinRange = 2f;
    // Player와의 X 거리가 이보다 가까우면 도약하지 않고 일반 추적/공격을 한다.

    [SerializeField, LabelText("도약 최대 거리"), ShowIf(nameof(replaceEvadeWithLeap))]
    private float leapMaxRange = 4.5f;
    // 이보다 멀면 걸어서 다가간 뒤 이 거리 안에 들어오면 도약한다.

    [SerializeField, LabelText("도약 수평 속도"), ShowIf(nameof(replaceEvadeWithLeap))]
    private float leapHorizontalSpeed = 5f;
    // 도약할 때 앞으로 나아가는 속도다. 체공 시간 × 이 값이 대략 도약 거리가 된다.

    [SerializeField, LabelText("도약 점프 힘"), ShowIf(nameof(replaceEvadeWithLeap))]
    private float leapUpForce = 4.5f;
    // 위로 튀어오르는 속도다. 클수록 높이, 오래 떠 있어서 더 멀리 날아간다.
    // 기본값(수평 5, 점프 4.5)이면 약 0.9초 동안 4.5m 정도 날아간다.

    [SerializeField, LabelText("도약 쿨다운"), ShowIf(nameof(replaceEvadeWithLeap))]
    private float leapCooldown = 3f;
    // 착지 후 바로 또 뛰지 않도록 두는 쉬는 시간이다. 착지한 순간부터 센다. 쿨다운 중에는 일반 추적/공격을 한다.
    // 값이 클수록 도약 빈도가 줄어든다.

    [Title("원거리 공격 (반격 대체)")]
    [InfoBox("켜면 학습기가 Counter 전술을 고를 때 빈틈을 기다리는 대신 뒤로 백스텝해 자리를 잡고 투사체를 쏜다. 비둘기처럼 먼저 공격해야 하는 적에서만 켠다.")]
    [SerializeField, LabelText("반격 대신 원거리 공격")]
    private bool replaceCounterWithRanged = false;
    // 기본값은 꺼짐이라 이 옵션을 켜지 않은 다른 적은 기존 반격 행동을 그대로 유지한다.

    [SerializeField, LabelText("투사체 프리팹"), ShowIf(nameof(replaceCounterWithRanged))]
    private BossProjectile rangedProjectilePrefab;
    // Inspector에서 Assets/Prefabs/Boss/BossProjectile.prefab처럼 BossProjectile이 달린 프리팹을 연결한다.

    [SerializeField, LabelText("발사 위치 오프셋"), ShowIf(nameof(replaceCounterWithRanged))]
    private Vector3 rangedSpawnOffset = new Vector3(0.8f, 0.1f, 0f);
    // 적 중심에서 발사 위치까지의 거리다. X는 "바라보는 쪽 앞" 기준이라 방향에 맞춰 자동으로 반전된다.
    // 몸통과 겹치면 바로 바닥에 닿아 사라질 수 있으니 몸 바깥으로 둔다.

    [SerializeField, LabelText("투사체 속도"), ShowIf(nameof(replaceCounterWithRanged))]
    private float rangedProjectileSpeed = 8f;
    // 값이 클수록 피하기 어려워진다.

    [SerializeField, LabelText("투사체 데미지"), ShowIf(nameof(replaceCounterWithRanged))]
    private float rangedProjectileDamage = 20f;
    // Player 체력 하트 1칸(20)에 맞춘 값이다.

    [SerializeField, LabelText("백스텝 수평 속도"), ShowIf(nameof(replaceCounterWithRanged))]
    private float backstepHorizontalSpeed = 4f;

    [SerializeField, LabelText("백스텝 점프 힘"), ShowIf(nameof(replaceCounterWithRanged))]
    private float backstepUpForce = 3f;
    // 기본값(수평 4, 점프 3)이면 약 0.6초 동안 뒤로 2m 정도 물러난다.

    [SerializeField, LabelText("조준 시간"), ShowIf(nameof(replaceCounterWithRanged))]
    private float rangedAimDuration = 0.4f;
    // 착지 후 발사하기 전까지 멈춰 있는 시간이다. Player가 공격을 눈치채고 피할 틈이 된다.

    [SerializeField, LabelText("원거리 공격 쿨다운"), ShowIf(nameof(replaceCounterWithRanged))]
    private float rangedCooldown = 4f;
    // 발사한 순간부터 센다. 쿨다운 중에는 일반 추적/공격을 해서 계속 뒤로만 물러나는 것을 막는다.

    [SerializeField, LabelText("원거리 최대 사거리"), ShowIf(nameof(replaceCounterWithRanged))]
    private float rangedMaxRange = 8f;
    // Player와의 X 거리가 이보다 멀면 걸어서 다가간 뒤 원거리 공격을 시작한다.

    [Title("알짱거림 원거리 공격")]
    [SerializeField, LabelText("알짱거림 원거리 사용")]
    private bool loiterRangedEnabled = true;
    // 켜면 Player가 멀리서(알짱거림 최소 거리 ~ 원거리 최대 사거리) 일정 시간 머물 때,
    // 학습이 고른 전술과 상관없이 원거리 공격을 한다. 느린 비둘기가 멀리서 걸어오기만 하는 것을 막기 위해서다.

    [SerializeField, LabelText("알짱거림 최소 거리"), ShowIf(nameof(loiterRangedEnabled))]
    private float loiterMinRange = 4.5f;
    // 이보다 가까우면 쪼기/도약 거리이므로 알짱거림으로 보지 않는다. 기본값은 도약 최대 거리와 같다.

    [SerializeField, LabelText("알짱거림 판정 시간(초)"), ShowIf(nameof(loiterRangedEnabled))]
    private float loiterDuration = 1.5f;
    // Player가 위 거리 범위 안에 이 시간 이상 계속 머물면 원거리 공격을 시작한다. 짧을수록 더 자주 쏜다.

    private float _loiterTimer;

    [ReadOnly, ShowInInspector, LabelText("알짱거림 시간")]
    private float LoiterTimer => _loiterTimer;

    [Title("패턴 사이 휴식")]
    [SerializeField, LabelText("패턴 후 휴식 시간")]
    private float postPatternRestTime = 1f;
    // 도약 착지 후, 원거리 발사 후 제자리에 서서 쉬는 시간이다. 클수록 Player가 반격할 틈이 길어진다. 0이면 휴식 없음.

    [SerializeField, LabelText("근접 공격 후 휴식 시간")]
    private float postMeleeRestTime = 0.5f;
    // 지상 근접 공격(쪼기)이 끝난 뒤(히트박스가 꺼진 뒤) 쉬는 시간이다.

    [Title("피격 경직")]
    [SerializeField, LabelText("피격 경직 시간")]
    private float hitStunDuration = 0.25f;
    // Enemy의 "진동 지속 시간"과 Pigeon.controller의 Hit→Idle 전환 시간(둘 다 0.25초)과 맞춰야
    // 움찔하는 연출이 끝나기 전에 걸어오지 않는다.
    // 맞은 순간부터 이 시간 동안 제자리에 멈춰 아무 행동도 하지 않는다. 연속으로 맞으면 다시 처음부터 센다.
    // 값이 클수록 Player가 연타로 몰아붙이기 쉬워진다. 0이면 경직 없음.

    private float _stunEndTime = -999f;
    // 이 시각(Time.time)이 될 때까지 경직 상태다.

    [ReadOnly, ShowInInspector, LabelText("경직 중")]
    private bool IsStunned => Time.time < _stunEndTime;

    [Title("디버그")]
    [SerializeField, LabelText("씬에 범위 표시")]
    private bool showRangeInScene = true;
    // 켜면 Scene 뷰에 도약 공격 거리(노랑)와 원거리 최대 사거리(파랑)를 발밑 원으로 그린다.
    // 해당 공격 옵션이 켜진 적만 그린다. 기즈모라 게임 화면과 빌드에는 나오지 않는다.

    private float _restEndTime = -999f;
    // 이 시각(Time.time)이 될 때까지 휴식한다. 초기값이 아주 과거라 시작 시엔 휴식 중이 아니다.

    private bool _pendingMeleeRest;
    // 근접 공격을 시작했고, 공격이 끝나면 휴식을 시작해야 한다는 표시다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("휴식 중")]
    private bool IsResting => Application.isPlaying && Time.time < _restEndTime;

    [ReadOnly, ShowInInspector, LabelText("추적 중")]
    private bool _isChasing;

    [ReadOnly, ShowInInspector, LabelText("궁지몰림 발악 중")]
    private bool _isCornered;

    [ReadOnly, ShowInInspector, LabelText("반격 중")]
    private bool _isCountering;

    [ReadOnly, ShowInInspector, LabelText("도약 중")]
    private bool _isLeaping;

    [ReadOnly, ShowInInspector, LabelText("도약 쿨다운 남은 시간"), ShowIf(nameof(replaceEvadeWithLeap))]
    private float LeapCooldownRemaining => Mathf.Max(0f, leapCooldown - (Time.time - _lastLeapLandTime));
    // Play Mode에서 다음 도약까지 얼마나 남았는지 확인하기 위한 값이다.

    [ReadOnly, ShowInInspector, LabelText("원거리 공격 중"), ShowIf(nameof(replaceCounterWithRanged))]
    private bool _isRangedAttacking;

    [ReadOnly, ShowInInspector, LabelText("원거리 쿨다운 남은 시간"), ShowIf(nameof(replaceCounterWithRanged))]
    private float RangedCooldownRemaining => Mathf.Max(0f, rangedCooldown - (Time.time - _lastRangedFireTime));

    [ReadOnly, ShowInInspector, LabelText("현재 전술 (학습 선택)")]
    private string _activeBranch = "Chase/Attack";

    [ReadOnly, ShowInInspector, LabelText("순찰 방향 (+1 오른쪽 / -1 왼쪽)")]
    private float _patrolDir = 1f;

    private float _patrolLeftX;
    private float _patrolRightX;
    private float _retreatTimer;
    private float _corneredTimer;
    private float _tacticsTimer;
    private bool _playerWasAttacking;
    private float _leapStartTime;
    private float _lastLeapLandTime = -999f;
    // 마지막으로 도약에서 착지한 시간이다. 도약 쿨다운은 뛴 순간이 아니라 이 시간부터 센다.
    // (뛴 순간부터 세면 체공 시간(약 0.9초)만큼 쿨다운이 깎여 착지하자마자 또 뛰게 된다.)

    private EnemyAnimationController enemyAnim;
    // EnemyAnimationController가 없는 적도 있으므로 null일 수 있다. 그래서 호출은 모두 enemyAnim?. 로 한다.

    private EnemyAttackSound attackSound;
    // 공격 사운드 컴포넌트다. 없는 적은 소리 없이 동작해야 하므로 null일 수 있고, 호출은 모두 attackSound?. 로 한다.

    private const float LeapMinAirTime = 0.15f;
    // 도약 직후에는 IsGrounded가 다음 물리 프레임까지 true로 남아 있어 바로 "착지"로 오판정된다.
    // 이 시간이 지나기 전에는 착지 판정을 하지 않는다.

    private const float LeapMaxDuration = 3f;
    // 어딘가에 걸려 착지 판정이 안 오더라도 도약 상태에 갇히지 않도록 하는 안전장치다.

    private float _rangedStartTime;
    private float _rangedAimStartTime = -1f;
    // 백스텝 후 착지해서 조준을 시작한 시간이다. -1이면 아직 백스텝 중(공중)이라는 뜻이다.
    private Vector3 _rangedDir;
    // 원거리 공격을 시작할 때의 Player 쪽 수평(XZ) 방향이다. 백스텝은 이 반대로 뛴다.
    private float _lastRangedFireTime = -999f;

    private EnemyVision _vision;
    private EnemyMovement _movement;
    private EnemyCombat _combat;
    private Enemy _enemy;
    private CombatStatsTracker _combatStats;
    private NPCFuzzyRL _fuzzyRL;
    private PlayerController _player;
    // Player가 탑다운 구역(ViewModeZone)에 들어갔는지 확인하기 위해 playerTransform에서 가져온다.

    private bool _rushAnimationStarted;

    private void Awake()
    {
        if (playerTransform != null)
            _player = playerTransform.GetComponent<PlayerController>();

        _vision = GetComponent<EnemyVision>();
        _movement = GetComponent<EnemyMovement>();
        _combat = GetComponent<EnemyCombat>();
        _enemy = GetComponent<Enemy>();
        enemyAnim = GetComponent<EnemyAnimationController>();
        attackSound = GetComponent<EnemyAttackSound>();
        _combatStats = GetComponent<CombatStatsTracker>();
        _fuzzyRL = new NPCFuzzyRL();

        float startX = transform.position.x;
        _patrolLeftX = startX - patrolHalfWidth;
        _patrolRightX = startX + patrolHalfWidth;
    }

    private void OnEnable()
    {
        _enemy.OnDamageTaken += HandleSelfDamaged;
        if (playerHealth != null)
            playerHealth.OnDamageTaken += HandlePlayerDamaged;
    }

    private void OnDisable()
    {
        _enemy.OnDamageTaken -= HandleSelfDamaged;
        if (playerHealth != null)
            playerHealth.OnDamageTaken -= HandlePlayerDamaged;
    }

    // 이 적이 맞으면 방금 선택했던 전술에 벌점(-0.5)을 준다.
    private void HandleSelfDamaged(float amount)
    {
        _fuzzyRL.OnRewardReceived(-0.5f);
        StartHitStun();
    }

    // 맞으면 짧게 경직시킨다. 도약 중(공중)이나 원거리 공격 중에는 걸지 않는다.
    // 그때 속도를 0으로 덮으면 공중에 멈춰 떠 있는 것처럼 보이기 때문이다.
    private void StartHitStun()
    {
        if (hitStunDuration <= 0f || _enemy.IsDead || _isLeaping || _isRangedAttacking) return;

        _stunEndTime = Time.time + hitStunDuration;

        // 쪼기 선딜 중에 맞으면 공격을 끊는다. 안 끊으면 경직 중에 히트박스가 켜져 Player가 맞는다.
        _combat.CancelAttack();
        _pendingMeleeRest = false;
        _isCountering = false;
        _rushAnimationStarted = false;
    }

    // 경직 중에는 제자리에 서 있기만 한다. 바라보는 방향은 맞은 순간 그대로 둔다.
    private void Stunned()
    {
        _movement.Move(Vector3.zero);
        enemyAnim?.SetMoving(false);
    }

    // 이 적이 공격 사거리 근처에서 Player에게 피해를 입혔다면, 방금 선택했던 전술에 보상(+1.0)을 준다.
    private void HandlePlayerDamaged(float amount)
    {
        if (HorizontalDistance() <= _combat.AttackRange * 1.5f)
            _fuzzyRL.OnRewardReceived(1f);
    }

    private void Update()
    {
        if (_enemy.IsDead || playerTransform == null) return;

        // Player가 탑다운 구역에 들어가면 추적을 포기하고 순찰로 돌아간다(CLAUDE.md: 탑다운 구역에는 적을 두지 않는다).
        // 도약/원거리 공격은 공중에 떠 있는 중일 수 있어 끝까지 마치게 두고, 그 뒤 프레임부터 순찰한다.
        if (IsPlayerInTopDown() && !_isLeaping && !_isRangedAttacking)
        {
            _isChasing = false;
            _isCornered = false;
            _isCountering = false;
            _pendingMeleeRest = false; // 탑다운으로 가면 휴식 상태도 풀어 복귀 후 이상한 정지를 막는다.
            _restEndTime = -999f;
            Patrol();
            return;
        }

        // 피격 경직은 발악·추적·공격보다 우선한다. (도약/원거리 중에는 StartHitStun이 경직을 걸지 않는다.)
        if (IsStunned)
        {
            Stunned();
            return;
        }

        if (_isCornered)
        {
            // 발악 중에는 학습된 전술과 무관하게 끝날 때까지 밀어붙인다 (2D 원작과 동일한 이유).
            CorneredAttack();
            return;
        }

        if (_isLeaping)
        {
            // 공중에 있는 동안은 전술이 바뀌어도 착지할 때까지 도약을 끝까지 진행한다.
            // 여기서 Move()를 부르면 도약 속도가 걷기 속도로 덮어써져 제자리 점프처럼 보이게 된다.
            UpdateLeap();
            return;
        }

        if (_isRangedAttacking)
        {
            // 도약과 같은 이유로, 백스텝~발사가 끝날 때까지는 전술이 바뀌어도 끊지 않는다.
            UpdateRangedAttack();
            return;
        }

        // 근접 공격의 히트박스가 끝나는 순간부터 휴식을 센다. (공격 시작부터 세면 선딜레이만큼 휴식이 깎인다.)
        if (_pendingMeleeRest && !_combat.IsAttacking)
        {
            _pendingMeleeRest = false;
            StartRest(postMeleeRestTime);
        }

        // 기본 공격(쪼기)이 진행되는 동안(선딜~히트박스 끝)에는 제자리에 서 있는다.
        // 이게 없으면 공격 도중 Player가 사거리 밖으로 살짝 벗어날 때 쪼는 모션 그대로 걸어와 버린다.
        // 바라보는 방향도 바꾸지 않아, 공격을 시작한 쪽으로 그대로 쫀다. (도약 중 공중 쪼기는 위에서 이미 처리된다)
        if (_combat.IsAttacking)
        {
            _movement.Move(Vector3.zero);
            enemyAnim?.SetMoving(false);
            return;
        }

        if (Time.time < _restEndTime)
        {
            Rest();
            return;
        }

        if (_isChasing && HorizontalDistance() > chaseAbandonRange)
            _isChasing = false;

        if (!_isChasing && _vision.CanSeePlayer(playerTransform))
            _isChasing = true;

        if (!_isChasing)
        {
            _loiterTimer = 0f;
            Patrol();
            return;
        }

        // Player가 멀리서 알짱거리면 전술과 상관없이 원거리 공격을 먼저 한다.
        if (TryLoiterRangedAttack()) return;

        UpdateTactics();

        switch (_activeBranch)
        {
            case "Evade/Recover":
                if (replaceEvadeWithLeap)
                    LeapAttack();
                else
                    Evade();
                break;
            case "Counter":
                if (replaceCounterWithRanged)
                    RangedAttack();
                else
                    HandleCounterBranch();
                break;
            default:
                ChaseAndAttack();
                break;
        }
    }

    private bool IsPlayerInTopDown()
    {
        return _player != null && _player.CurrentViewMode == ViewMode.TopDown;
    }

    // tacticsUpdateInterval마다 최근 전투 통계를 학습기에 반영하고 전술(ActiveBranch)을 다시 계산한다.
    private void UpdateTactics()
    {
        _tacticsTimer += Time.deltaTime;
        if (_tacticsTimer < tacticsUpdateInterval) return;
        _tacticsTimer = 0f;

        float[] log = _combatStats.GetFeatureVector();
        _fuzzyRL.OnPlayerActionLog(log);

        float distNorm = Mathf.Clamp01(HorizontalDistance() / maxRelevantDistance);
        float[] gameState = { distNorm, _enemy.HealthRatio };

        int action = _fuzzyRL.ComputeTactic(gameState);
        _activeBranch = NPCFuzzyRL.BranchNames[action];
    }

    // 시작 위치를 중심으로 patrolHalfWidth 범위를 좌우로 왕복한다.
    // 경계에 닿거나, 앞에 벽이 있거나, 앞이 낭떠러지면 방향을 반전한다.
    private void Patrol()
    {
        if (ShouldReversePatrol())
            _patrolDir = -_patrolDir;

        // 순찰은 X축 왕복만 하지만, 몸이 부드럽게 돌도록 360도 버전 API에 ±X 방향을 넘긴다.
        Vector3 patrolVector = new Vector3(_patrolDir, 0f, 0f);
        _movement.Move(patrolVector);
        _vision.SetFacingDirection(patrolVector);

        enemyAnim?.SetMoving(true);
    }

    private bool ShouldReversePatrol()
    {
        if (_patrolDir > 0f && transform.position.x >= _patrolRightX) return true;
        if (_patrolDir < 0f && transform.position.x <= _patrolLeftX) return true;

        if (_movement.HasWallAhead(_patrolDir)) return true;
        if (!_movement.HasGroundAhead(_patrolDir)) return true;

        return false;
    }

    // 사이드뷰에서도 플레이어가 앞뒤(Z)로 움직이므로 바닥 평면(XZ) 거리로 추적/공격 판정을 한다.
    // 높이(Y)는 빼서, 플레이어가 점프 중이어도 거리가 갑자기 멀어진 것처럼 판정되지 않게 한다.
    private float HorizontalDistance()
    {
        Vector3 offset = playerTransform.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // Player 쪽 수평(XZ) 방향 단위 벡터다. 같은 자리에 겹쳐 있으면 지금 바라보는 방향을 그대로 쓴다.
    private Vector3 DirectionToPlayer()
    {
        Vector3 offset = playerTransform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : _movement.FacingVector;
    }

    private void ChaseAndAttack()
    {
        Vector3 dir = DirectionToPlayer();

        if (HorizontalDistance() <= _combat.AttackRange)
        {
            _movement.Move(Vector3.zero);
            _movement.FaceDirection(dir);
            _vision.SetFacingDirection(dir);

            enemyAnim?.SetMoving(false);

            if (_combat.CanAttack)
            {
                _combat.StartAttack();
                enemyAnim?.PlayAttack();
                attackSound?.PlayMelee();
                _pendingMeleeRest = true; // 공격이 끝나면 휴식을 시작하도록 표시한다.
            }
        }
        else
        {
            _movement.Move(dir);
            _vision.SetFacingDirection(dir);

            enemyAnim?.SetMoving(true);
        }
    }

    // Player 반대 방향으로 후퇴한다. 안전 거리를 확보하면 멈추고,
    // 최소 후퇴 시간이 지난 뒤 벽/낭떠러지에 막히면 궁지몰림 발악에 들어간다.
    private void Evade()
    {
        Vector3 awayDir = -DirectionToPlayer();

        if (HorizontalDistance() >= safeDistance)
        {
            // 안전 거리를 확보했으면 멈춰서 기다린다. 다음 전술 재계산 때까지 이 상태를 유지한다.
            _movement.Move(Vector3.zero);
            enemyAnim?.SetMoving(false);

            _retreatTimer = 0f;
            return;
        }

        _movement.Move(awayDir);
        _vision.SetFacingDirection(awayDir);
        enemyAnim?.SetMoving(true);

        _retreatTimer += Time.deltaTime;
        if (_retreatTimer < minRetreatDuration) return;

        // 후퇴 방향 앞에 벽이 있거나 낭떠러지면 더 물러날 곳이 없다는 뜻이므로 궁지몰림으로 전환한다.
        if (_movement.HasWallAhead(awayDir) || !_movement.HasGroundAhead(awayDir))
            SetCornered();
    }

    // Evade/Recover 전술 대신 쓰는 도약공격이다(replaceEvadeWithLeap가 켜진 경우).
    // 도약 거리 안에 들어오면 앞으로 뛰어들고, 너무 가깝거나 쿨다운 중이면 일반 추적/공격을 한다.
    private void LeapAttack()
    {
        Vector3 dir = DirectionToPlayer();
        float distance = HorizontalDistance();

        bool leapReady = Time.time - _lastLeapLandTime >= leapCooldown;
        if (distance < leapMinRange || !leapReady)
        {
            ChaseAndAttack();
            return;
        }

        if (distance > leapMaxRange || _movement.HasWallAhead(dir))
        {
            // 아직 멀거나 바로 앞이 벽이면 걸어서 다가간다.
            _movement.Move(dir);
            _vision.SetFacingDirection(dir);
            return;
        }

        StartLeap(dir);
    }

    private void StartLeap(Vector3 dir)
    {
        if (!_movement.JumpMove(dir, leapHorizontalSpeed, leapUpForce)) return;

        _isLeaping = true;
        _leapStartTime = Time.time;
        _vision.SetFacingDirection(dir);

        enemyAnim?.SetMoving(false);
        // 도약 포즈(Rush)는 길이 0인 정지 포즈라 Exit Time으로는 바로 끝나 버리므로, 착지할 때까지 IsLeaping으로 붙잡아 둔다.
        enemyAnim?.SetLeaping(true);
        enemyAnim?.PlayRush();
        attackSound?.PlayRush();
    }

    // 패턴이 끝난 뒤 duration초 동안 쉬도록 종료 시각을 정한다. 0 이하면 휴식하지 않는다.
    private void StartRest(float duration)
    {
        if (duration <= 0f) return;
        _restEndTime = Time.time + duration;
    }

    // 휴식 중 매 프레임 호출된다. 제자리에 서서 Player 쪽만 바라보게 해 반격할 틈을 준다.
    private void Rest()
    {
        Vector3 dir = DirectionToPlayer();
        _movement.Move(Vector3.zero);
        _movement.FaceDirection(dir);
        _vision.SetFacingDirection(dir);
        enemyAnim?.SetMoving(false);
    }

    // 도약 중 매 프레임 호출된다. 날아가다 Player가 공격 사거리 안에 들어오는 순간 공격해서
    // 뛰어든 몸이 부딪히는 타이밍에 히트박스가 켜지게 한다(뛰는 순간 공격하면 닿기 전에 판정이 끝나 버린다).
    private void UpdateLeap()
    {
        if (HorizontalDistance() <= _combat.AttackRange && _combat.CanAttack)
        {
            _combat.StartAttack();
            enemyAnim?.PlayAttack(); // 공중에서 쪼기 모션으로 전환(Any State -> Attack)
            // 여기서는 일부러 attackSound?.PlayMelee()를 부르지 않는다. 도약 시작 때 돌진 울음(PlayRush)이 이미 났고, 겹치면 소리가 지저분해진다.
        }

        float airTime = Time.time - _leapStartTime;
        bool landed = airTime >= LeapMinAirTime && _movement.IsGrounded;
        if (landed || airTime >= LeapMaxDuration)
        {
            _isLeaping = false;
            enemyAnim?.SetLeaping(false);
            _lastLeapLandTime = Time.time;
            // 착지한 순간부터 쿨다운을 센다.
            StartRest(postPatternRestTime); // 착지 후 잠깐 서서 쉰다.
        }
    }

    [Button("도약공격 테스트")]
    private void TestLeap()
    {
        // Play Mode에서 Player 쪽으로 즉시 도약시켜 거리/높이/공격 타이밍을 확인한다.
        if (!Application.isPlaying || playerTransform == null) return;
        StartLeap(DirectionToPlayer());
    }

    // Counter 전술 대신 쓰는 원거리 공격이다(replaceCounterWithRanged가 켜진 경우).
    // 쿨다운 중이면 일반 추적/공격, 너무 멀면 걸어서 다가가고, 사거리 안이면 백스텝 후 발사한다.
    private void RangedAttack()
    {
        Vector3 dir = DirectionToPlayer();

        bool rangedReady = Time.time - _lastRangedFireTime >= rangedCooldown;
        if (!rangedReady)
        {
            ChaseAndAttack();
            return;
        }

        if (HorizontalDistance() > rangedMaxRange)
        {
            _movement.Move(dir);
            _vision.SetFacingDirection(dir);
            return;
        }

        StartRangedAttack(dir);
    }

    // Player가 [알짱거림 최소 거리 ~ 원거리 최대 사거리] 안에 알짱거림 판정 시간 이상 머물렀고
    // 원거리 쿨다운이 끝났으면 원거리 공격을 시작한다. 시작했으면 true를 반환한다.
    // (경직/도약/원거리/쪼기/휴식 중에는 Update가 여기까지 오지 않으므로 그 사이에는 끼어들지 않는다.)
    private bool TryLoiterRangedAttack()
    {
        if (!loiterRangedEnabled || rangedProjectilePrefab == null)
        {
            _loiterTimer = 0f;
            return false;
        }

        float distance = HorizontalDistance();
        if (distance >= loiterMinRange && distance <= rangedMaxRange)
            _loiterTimer += Time.deltaTime;
        else
            _loiterTimer = 0f;
        // 범위를 벗어나는 순간 0으로 돌려서, "계속" 머물렀을 때만 쏘게 한다.

        bool rangedReady = Time.time - _lastRangedFireTime >= rangedCooldown;
        if (_loiterTimer < loiterDuration || !rangedReady) return false;

        _loiterTimer = 0f;
        StartRangedAttack(DirectionToPlayer());
        return _isRangedAttacking;
        // 공중이라 시작하지 못했으면(StartRangedAttack이 바로 return) 이번 프레임은 평소 전술로 움직인다.
    }

    private void StartRangedAttack(Vector3 dir)
    {
        if (!_movement.IsGrounded) return;

        _isRangedAttacking = true;
        _rangedDir = dir;
        _rangedStartTime = Time.time;
        _rangedAimStartTime = -1f;

        // 뒤에 벽이 있거나 낭떠러지면 물러날 곳이 없으므로 백스텝 없이 바로 제자리 조준으로 넘어간다.
        bool canBackstep = !_movement.HasWallAhead(-dir) && _movement.HasGroundAhead(-dir);
        if (canBackstep)
        {
            _movement.JumpMove(-dir, backstepHorizontalSpeed, backstepUpForce);
            attackSound?.PlayBackstepJump();
        }
        else
        {
            _rangedAimStartTime = Time.time;
            // 조준 시작과 동시에 Shot(몸을 숙이는 모션)을 재생한다. Shot 클립의 숙이는 구간(0~0.4초)이
            // rangedAimDuration(0.4초)과 같아서, 숙이는 정점에 맞춰 발사된다.
            enemyAnim?.SetMoving(false);
            enemyAnim?.PlayShot();
        }

        // JumpMove가 뛰는 방향(뒤)을 바라보게 하므로, 몸은 다시 Player 쪽으로 돌려 "뒷걸음질"처럼 보이게 한다.
        _movement.FaceDirection(dir);
        _vision.SetFacingDirection(dir);
    }

    // 원거리 공격 중 매 프레임 호출된다. 백스텝 착지 → 조준(rangedAimDuration) → 발사 순서로 진행한다.
    private void UpdateRangedAttack()
    {
        float elapsed = Time.time - _rangedStartTime;

        if (_rangedAimStartTime < 0f)
        {
            // 도약과 같은 이유(점프 직후 IsGrounded가 잠깐 true로 남음)로 최소 체공 시간 뒤에만 착지 판정한다.
            bool landed = elapsed >= LeapMinAirTime && _movement.IsGrounded;
            if (!landed && elapsed < LeapMaxDuration) return;

            _movement.Move(Vector3.zero);
            _rangedAimStartTime = Time.time;
            attackSound?.PlayBackstepLand();
            // 조준 시작과 동시에 Shot(몸을 숙이는 모션)을 재생한다. Shot 클립의 숙이는 구간(0~0.4초)이
            // rangedAimDuration(0.4초)과 같아서, 숙이는 정점에 맞춰 발사된다.
            enemyAnim?.SetMoving(false);
            enemyAnim?.PlayShot();
        }

        // 조준 중에는 제자리에 서서 Player 쪽을 계속 바라본다. 조준 도중 Player가 움직여도 그쪽으로 쏜다.
        Vector3 dir = DirectionToPlayer();
        _movement.Move(Vector3.zero);
        _movement.FaceDirection(dir);
        _vision.SetFacingDirection(dir);
        _rangedDir = dir;

        if (Time.time - _rangedAimStartTime < rangedAimDuration) return;

        attackSound?.PlayRangedFire();
        FireRangedProjectile(_rangedDir);
        _lastRangedFireTime = Time.time;
        _isRangedAttacking = false;
        StartRest(postPatternRestTime); // 발사 후 잠깐 서서 쉰다.
    }

    // Player 쪽 수평(XZ) 방향으로 발사한다. 높이는 유지하므로 Player가 점프하면 피할 수 있다.
    private void FireRangedProjectile(Vector3 dir)
    {
        if (rangedProjectilePrefab == null) return;

        // 발사 위치 오프셋의 X는 "바라보는 쪽 앞" 거리, Z는 옆 방향 거리로 보고 dir 기준으로 다시 계산한다.
        // (몸이 360도로 도는 중이라 로컬 좌표를 쓰면 회전이 덜 끝난 방향으로 나갈 수 있어 dir로 직접 계산한다.)
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        Vector3 spawnPos = transform.position
            + dir * Mathf.Abs(rangedSpawnOffset.x)
            + Vector3.up * rangedSpawnOffset.y
            + side * rangedSpawnOffset.z;

        BossProjectile projectile = Instantiate(rangedProjectilePrefab, spawnPos, Quaternion.identity);
        projectile.Launch(dir, rangedProjectileSpeed, rangedProjectileDamage);
    }

    [Button("원거리 공격 테스트")]
    private void TestRangedAttack()
    {
        // Play Mode에서 쿨다운과 무관하게 즉시 백스텝 → 조준 → 발사를 실행해 거리/타이밍을 확인한다.
        if (!Application.isPlaying || playerTransform == null || _isRangedAttacking) return;
        StartRangedAttack(DirectionToPlayer());
    }

    private void SetCornered()
    {
        _isCornered = true;
        _corneredTimer = 0f;
    }

    // 궁지몰림 발악: corneredDuration 동안 Player 쪽으로 계속 돌진하며,
    // 공격 쿨다운이 돌아올 때마다 점프공격을 반복한다.
    private void CorneredAttack()
    {
        _corneredTimer += Time.deltaTime;
        if (_corneredTimer >= corneredDuration)
        {
            _isCornered = false;
            _retreatTimer = 0f;
            return;
        }

        Vector3 dir = DirectionToPlayer();

        _vision.SetFacingDirection(dir);

        if (_combat.CanAttack)
        {
            _movement.JumpMove(dir);
            _combat.StartAttack();
        }
        else
        {
            _movement.Move(dir);
        }
    }

    // Counter 전술이 선택된 동안의 행동이다. 반격 중이 아니면 제자리에서 Player를 주시하며
    // 공격이 막 끝나는 순간(빈틈)을 기다리고, 빈틈을 포착하면 반격 돌진으로 전환한다.
    private void HandleCounterBranch()
    {
        if (!_isCountering)
        {
            Vector3 dir = DirectionToPlayer();

            _movement.Move(Vector3.zero);
            _movement.FaceDirection(dir);
            _vision.SetFacingDirection(dir);

            DetectCounterOpening();
            return;
        }

        CounterAttack();
    }

    // Player의 공격이 막 끝나는 순간(빈틈)을 매 프레임 감지한다.
    private void DetectCounterOpening()
    {
        bool playerIsAttackingNow = playerSwordAttack != null && playerSwordAttack.IsAttacking;
        bool attackJustEnded = _playerWasAttacking && !playerIsAttackingNow;
        _playerWasAttacking = playerIsAttackingNow;

        if (attackJustEnded)
            _isCountering = true;
    }

    // 반격 돌진: 사거리 밖이면 평소보다 빠르게 돌진하고, 사거리 안이면 멈춰서 공격한다.
    // 공격을 실제로 시작하면(또는 Player를 놓치면) 반격 상태를 끝내고 다시 빈틈을 기다린다.
    private void CounterAttack()
    {
        if (HorizontalDistance() > chaseAbandonRange)
        {
            _isCountering = false;
            _rushAnimationStarted = false; // ① 추적 포기 시 초기화
            return;
        }

        Vector3 dir = DirectionToPlayer();

        if (HorizontalDistance() <= _combat.AttackRange)
        {
            _movement.Move(Vector3.zero);
            _movement.FaceDirection(dir);
            _vision.SetFacingDirection(dir);

            enemyAnim?.SetMoving(false);

            if (_combat.CanAttack)
            {
                _combat.StartAttack();
                enemyAnim?.PlayAttack();
                attackSound?.PlayMelee();
                _pendingMeleeRest = true; // 공격이 끝나면 휴식을 시작하도록 표시한다.

                _isCountering = false;
                _rushAnimationStarted = false; // ② 공격 시작 후 초기화
            }
        }
        else
        {
            _movement.Move(dir * counterRushSpeedMultiplier);
            _vision.SetFacingDirection(dir);

            enemyAnim?.SetMoving(false);

            if (!_rushAnimationStarted)
            {
                enemyAnim?.PlayRush();
                attackSound?.PlayRush();
                _rushAnimationStarted = true;
            }
        }
    }

#if UNITY_EDITOR
    // 선택하지 않아도 항상 보이게 OnDrawGizmos를 쓴다.
    // 거리 판정(HorizontalDistance)이 높이를 뺀 XZ 거리이므로, 바닥과 평행한 원(Y축 기준)으로 그린다.
    private void OnDrawGizmos()
    {
        if (!showRangeInScene) return;
        Vector3 center = transform.position;

        if (replaceEvadeWithLeap)
        {
            // 도약은 최소~최대 거리 사이에서만 나가므로 두 원 사이 띠가 도약 구간이다.
            UnityEditor.Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, leapMinRange);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, leapMaxRange);
        }

        if (replaceCounterWithRanged)
        {
            UnityEditor.Handles.color = new Color(0.25f, 0.6f, 1f, 0.9f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, rangedMaxRange);
        }
    }
#endif
}
