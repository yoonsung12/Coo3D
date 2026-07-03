using Sirenix.OdinInspector;
using UnityEngine;

// 이 적이 독립적으로 소유하는 전투 통계 트래커다. 이 적과 Player 사이의 상호작용만 측정해
// NPCFuzzyRL의 학습 입력 벡터를 제공한다.
// 피처: [공격 빈도, 명중률, 초당 피해량]
// - 공격 빈도: 이 적의 시야 범위 안에서 Player가 공격을 시도한 빈도
// - 명중률: 이 적이 실제로 맞은 횟수 / 시야 범위 안 공격 시도 횟수
// - 초당 피해량: 이 적이 Player에게 입힌 초당 피해
// 2D 원작의 CombatStatsTracker를 3D로 포팅했다. Player.Instance 싱글턴이 없는 대신
// Inspector에서 직접 Player 관련 컴포넌트를 연결한다.
public class CombatStatsTracker : MonoBehaviour
{
    [Title("정규화 기준값")]
    [SerializeField, LabelText("최대 공격 빈도 (초당)")]
    private float maxAttackFrequency = 0.05f;
    // 실제 측정치가 이 값을 넘으면 1로 클램프된다. 게임 밸런스에 맞게 튜닝한다.

    [SerializeField, LabelText("최대 초당 피해량")]
    private float maxDamagePerSecond = 0.015f;

    [Title("연결")]
    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;

    [SerializeField, LabelText("Player SwordAttack")]
    private SwordAttack playerSwordAttack;

    [SerializeField, LabelText("Player Health")]
    private PlayerHealth playerHealth;

    private Enemy _enemy;
    private EnemyVision _vision;
    private EnemyCombat _combat;

    private float _sessionStart;
    private int _attackCount;   // 이 적 시야 범위 내 Player 공격 시도 횟수
    private int _hitCount;      // 이 적이 실제로 맞은 횟수
    private float _totalDamage; // 이 적이 Player에게 입힌 누적 피해량
    private bool _playerWasAttacking;

    private float SessionTime => Mathf.Max(1f, Time.time - _sessionStart);

    public float AttackFrequency => Mathf.Clamp01(_attackCount / (SessionTime * maxAttackFrequency));
    public float HitRate => _attackCount == 0 ? 0f : Mathf.Clamp01((float)_hitCount / _attackCount);
    public float DamagePerSec => Mathf.Clamp01(_totalDamage / (SessionTime * maxDamagePerSecond));

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _vision = GetComponent<EnemyVision>();
        _combat = GetComponent<EnemyCombat>();
        _sessionStart = Time.time;
    }

    private void OnEnable()
    {
        _enemy.OnDamageTaken += HandleThisEnemyHit;
        if (playerHealth != null)
            playerHealth.OnDamageTaken += HandlePlayerDamageTaken;
    }

    private void OnDisable()
    {
        _enemy.OnDamageTaken -= HandleThisEnemyHit;
        if (playerHealth != null)
            playerHealth.OnDamageTaken -= HandlePlayerDamageTaken;
    }

    private void Update()
    {
        DetectPlayerAttackStart();
    }

    // Player의 공격이 막 시작되는 순간(폴링)을 감지해, 이 적의 시야 범위 안일 때만 공격 횟수로 센다.
    private void DetectPlayerAttackStart()
    {
        bool isAttackingNow = playerSwordAttack != null && playerSwordAttack.IsAttacking;
        bool justStarted = isAttackingNow && !_playerWasAttacking;
        _playerWasAttacking = isAttackingNow;

        if (!justStarted || playerTransform == null) return;

        float dist = Mathf.Abs(transform.position.x - playerTransform.position.x);
        if (dist <= _vision.ViewDistance)
            _attackCount++;
    }

    private void HandleThisEnemyHit(float damage) => _hitCount++;

    // 이 적이 공격 사거리 근처에 있을 때 발생한 Player 피해만 이 적의 기여로 간주한다.
    private void HandlePlayerDamageTaken(float damage)
    {
        if (playerTransform == null) return;

        float dist = Mathf.Abs(transform.position.x - playerTransform.position.x);
        if (dist <= _combat.AttackRange * 1.5f)
            _totalDamage += damage;
    }

    // NPCFuzzyRL의 학습 입력으로 쓰이는 3차원 피처 벡터를 반환한다.
    public float[] GetFeatureVector() => new[] { AttackFrequency, HitRate, DamagePerSec };
}
