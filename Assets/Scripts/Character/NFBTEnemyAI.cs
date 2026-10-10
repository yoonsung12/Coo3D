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

    [Title("런타임 상태 (읽기 전용)")]
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

    private const float LeapMinAirTime = 0.15f;
    // 도약 직후에는 IsGrounded가 다음 물리 프레임까지 true로 남아 있어 바로 "착지"로 오판정된다.
    // 이 시간이 지나기 전에는 착지 판정을 하지 않는다.

    private const float LeapMaxDuration = 3f;
    // 어딘가에 걸려 착지 판정이 안 오더라도 도약 상태에 갇히지 않도록 하는 안전장치다.

    private float _rangedStartTime;
    private float _rangedAimStartTime = -1f;
    // 백스텝 후 착지해서 조준을 시작한 시간이다. -1이면 아직 백스텝 중(공중)이라는 뜻이다.
    private float _rangedDir;
    // 원거리 공격을 시작할 때의 Player 방향(+1/-1)이다. 백스텝은 이 반대로 뛴다.
    private float _lastRangedFireTime = -999f;

    private EnemyVision _vision;
    private EnemyMovement _movement;
    private EnemyCombat _combat;
    private Enemy _enemy;
    private CombatStatsTracker _combatStats;
    private NPCFuzzyRL _fuzzyRL;

    private void Awake()
    {
        _vision = GetComponent<EnemyVision>();
        _movement = GetComponent<EnemyMovement>();
        _combat = GetComponent<EnemyCombat>();
        _enemy = GetComponent<Enemy>();
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
    private void HandleSelfDamaged(float amount) => _fuzzyRL.OnRewardReceived(-0.5f);

    // 이 적이 공격 사거리 근처에서 Player에게 피해를 입혔다면, 방금 선택했던 전술에 보상(+1.0)을 준다.
    private void HandlePlayerDamaged(float amount)
    {
        if (HorizontalDistance() <= _combat.AttackRange * 1.5f)
            _fuzzyRL.OnRewardReceived(1f);
    }

    private void Update()
    {
        if (_enemy.IsDead || playerTransform == null) return;

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

        if (_isChasing && HorizontalDistance() > chaseAbandonRange)
            _isChasing = false;

        if (!_isChasing && _vision.CanSeePlayer(playerTransform))
            _isChasing = true;

        if (!_isChasing)
        {
            Patrol();
            return;
        }

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

        _movement.Move(_patrolDir);
        _vision.SetFacingDirection(_patrolDir);
    }

    private bool ShouldReversePatrol()
    {
        if (_patrolDir > 0f && transform.position.x >= _patrolRightX) return true;
        if (_patrolDir < 0f && transform.position.x <= _patrolLeftX) return true;

        if (_movement.HasWallAhead(_patrolDir)) return true;
        if (!_movement.HasGroundAhead(_patrolDir)) return true;

        return false;
    }

    // 사이드뷰(X-Y 평면)라 좌우 거리(X)만으로 추적/공격 판정을 한다.
    private float HorizontalDistance()
    {
        return Mathf.Abs(playerTransform.position.x - transform.position.x);
    }

    private void ChaseAndAttack()
    {
        float dx = playerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);

        if (Mathf.Abs(dx) <= _combat.AttackRange)
        {
            // 사거리 안이면 멈추고 Player 쪽을 바라보며 공격을 시도한다.
            _movement.Move(0f);
            _movement.FaceDirection(dir);
            _vision.SetFacingDirection(dir);

            if (_combat.CanAttack)
                _combat.StartAttack();
        }
        else
        {
            _movement.Move(dir);
            _vision.SetFacingDirection(dir);
        }
    }

    // Player 반대 방향으로 후퇴한다. 안전 거리를 확보하면 멈추고,
    // 최소 후퇴 시간이 지난 뒤 벽/낭떠러지에 막히면 궁지몰림 발악에 들어간다.
    private void Evade()
    {
        float dx = playerTransform.position.x - transform.position.x;
        float awayDir = -Mathf.Sign(dx);

        if (Mathf.Abs(dx) >= safeDistance)
        {
            // 안전 거리를 확보했으면 멈춰서 기다린다. 다음 전술 재계산 때까지 이 상태를 유지한다.
            _movement.Move(0f);
            _retreatTimer = 0f;
            return;
        }

        _movement.Move(awayDir);
        _vision.SetFacingDirection(awayDir);

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
        float dx = playerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);
        float distance = Mathf.Abs(dx);

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

    private void StartLeap(float dir)
    {
        // 바닥에 서 있을 때만 뛴다. 공중이면 JumpMove가 false를 돌려주고 아무 일도 하지 않는다.
        if (!_movement.JumpMove(dir, leapHorizontalSpeed, leapUpForce)) return;

        _isLeaping = true;
        _leapStartTime = Time.time;
        _vision.SetFacingDirection(dir);
    }

    // 도약 중 매 프레임 호출된다. 날아가다 Player가 공격 사거리 안에 들어오는 순간 공격해서
    // 뛰어든 몸이 부딪히는 타이밍에 히트박스가 켜지게 한다(뛰는 순간 공격하면 닿기 전에 판정이 끝나 버린다).
    private void UpdateLeap()
    {
        float dx = playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(dx) <= _combat.AttackRange && _combat.CanAttack)
            _combat.StartAttack();

        float airTime = Time.time - _leapStartTime;
        bool landed = airTime >= LeapMinAirTime && _movement.IsGrounded;
        if (landed || airTime >= LeapMaxDuration)
        {
            _isLeaping = false;
            _lastLeapLandTime = Time.time;
            // 착지한 순간부터 쿨다운을 센다.
        }
    }

    [Button("도약공격 테스트")]
    private void TestLeap()
    {
        // Play Mode에서 Player 쪽으로 즉시 도약시켜 거리/높이/공격 타이밍을 확인한다.
        if (!Application.isPlaying || playerTransform == null) return;
        StartLeap(Mathf.Sign(playerTransform.position.x - transform.position.x));
    }

    // Counter 전술 대신 쓰는 원거리 공격이다(replaceCounterWithRanged가 켜진 경우).
    // 쿨다운 중이면 일반 추적/공격, 너무 멀면 걸어서 다가가고, 사거리 안이면 백스텝 후 발사한다.
    private void RangedAttack()
    {
        float dx = playerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);

        bool rangedReady = Time.time - _lastRangedFireTime >= rangedCooldown;
        if (!rangedReady)
        {
            ChaseAndAttack();
            return;
        }

        if (Mathf.Abs(dx) > rangedMaxRange)
        {
            _movement.Move(dir);
            _vision.SetFacingDirection(dir);
            return;
        }

        StartRangedAttack(dir);
    }

    private void StartRangedAttack(float dir)
    {
        if (!_movement.IsGrounded) return;

        _isRangedAttacking = true;
        _rangedDir = dir;
        _rangedStartTime = Time.time;
        _rangedAimStartTime = -1f;

        // 뒤에 벽이 있거나 낭떠러지면 물러날 곳이 없으므로 백스텝 없이 바로 제자리 조준으로 넘어간다.
        bool canBackstep = !_movement.HasWallAhead(-dir) && _movement.HasGroundAhead(-dir);
        if (canBackstep)
            _movement.JumpMove(-dir, backstepHorizontalSpeed, backstepUpForce);
        else
            _rangedAimStartTime = Time.time;

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

            _movement.Move(0f);
            _rangedAimStartTime = Time.time;
        }

        // 조준 중에는 제자리에 서서 Player 쪽을 계속 바라본다. 조준 도중 Player가 반대편으로 넘어가도 그쪽으로 쏜다.
        float dir = Mathf.Sign(playerTransform.position.x - transform.position.x);
        _movement.Move(0f);
        _movement.FaceDirection(dir);
        _vision.SetFacingDirection(dir);
        _rangedDir = dir;

        if (Time.time - _rangedAimStartTime < rangedAimDuration) return;

        FireRangedProjectile(_rangedDir);
        _lastRangedFireTime = Time.time;
        _isRangedAttacking = false;
    }

    // 사이드뷰라 X축 방향(±1)으로만 수평 발사한다. Player가 점프하면 피할 수 있다.
    private void FireRangedProjectile(float dir)
    {
        if (rangedProjectilePrefab == null) return;

        // 적 오브젝트는 Y축으로 ±90도 회전해 있으므로, 회전된 로컬 좌표 대신 월드 X 기준으로 직접 계산한다.
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Abs(rangedSpawnOffset.x) * dir, rangedSpawnOffset.y, rangedSpawnOffset.z);

        BossProjectile projectile = Instantiate(rangedProjectilePrefab, spawnPos, Quaternion.identity);
        projectile.Launch(new Vector3(dir, 0f, 0f), rangedProjectileSpeed, rangedProjectileDamage);
    }

    [Button("원거리 공격 테스트")]
    private void TestRangedAttack()
    {
        // Play Mode에서 쿨다운과 무관하게 즉시 백스텝 → 조준 → 발사를 실행해 거리/타이밍을 확인한다.
        if (!Application.isPlaying || playerTransform == null || _isRangedAttacking) return;
        StartRangedAttack(Mathf.Sign(playerTransform.position.x - transform.position.x));
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

        float dx = playerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);

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
            float dx = playerTransform.position.x - transform.position.x;
            float dir = Mathf.Sign(dx);

            _movement.Move(0f);
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
            return;
        }

        float dx = playerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);

        if (Mathf.Abs(dx) <= _combat.AttackRange)
        {
            _movement.Move(0f);
            _movement.FaceDirection(dir);
            _vision.SetFacingDirection(dir);

            if (_combat.CanAttack)
            {
                _combat.StartAttack();
                _isCountering = false;
            }
        }
        else
        {
            _movement.Move(dir * counterRushSpeedMultiplier);
            _vision.SetFacingDirection(dir);
        }
    }
}
