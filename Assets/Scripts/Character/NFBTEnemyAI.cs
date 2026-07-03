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

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("추적 중")]
    private bool _isChasing;

    [ReadOnly, ShowInInspector, LabelText("궁지몰림 발악 중")]
    private bool _isCornered;

    [ReadOnly, ShowInInspector, LabelText("반격 중")]
    private bool _isCountering;

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
                Evade();
                break;
            case "Counter":
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
