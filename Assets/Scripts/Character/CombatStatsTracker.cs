using System.Collections.Generic;
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

    [Title("최근 성향 피처 (공격 선택 학습용)")]
    [SerializeField, LabelText("최근 시간 창 (초)")]
    private float recentWindow = 10f;
    // 공격성/선호 거리를 계산할 "최근" 구간 길이다.
    // 값이 커지면 오래된 행동까지 반영해 느리고 안정적으로, 작아지면 최근 행동에 민감하게 변한다.

    [SerializeField, LabelText("공격성 기준 (초당 공격 수)")]
    private float aggressionReference = 1f;
    // 초당 이만큼 공격하면 공격성이 1이 된다. (10초 창에서 10번 = 1)
    // 값이 커지면 같은 횟수여도 공격성이 낮게, 작아지면 높게 나온다.

    [SerializeField, LabelText("거리 정규화 기준")]
    private float distanceReference = 10f;
    // 이 거리 이상이면 선호 거리가 1이 된다. NFBTEnemyAI의 "거리 정규화 기준"과 같게 맞춘다.
    // 값이 커지면 같은 거리도 가깝게(0에 가깝게) 평가된다.

    [SerializeField, LabelText("거리 기록 간격 (초)")]
    private float distanceSampleInterval = 0.25f;
    // Player와의 거리를 기록하는 주기다. 작을수록 정밀하지만 샘플이 많아진다.

    [SerializeField, LabelText("회피율 계산 결과 개수")]
    private int dodgeResultCount = 8;
    // 회피율에 반영할 최근 공격 결과 개수다. 클수록 안정적, 작을수록 최근 결과에 민감하다.

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

    // 최근 성향 피처용 기록. Awake에서 한 번만 만들어 매 프레임 할당을 피한다.
    private struct DistanceSample
    {
        public float time;
        public float distance;
    }

    private Queue<float> _recentAttackTimes;          // 시야 안 Player 공격 시작 시각
    private Queue<DistanceSample> _recentDistances;   // 시야 안 Player 거리 샘플
    private Queue<bool> _recentAttackResults;         // 최근 공격 결과 (true=명중)
    private float _nextDistanceSampleTime;

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
        _recentAttackTimes = new Queue<float>();
        _recentDistances = new Queue<DistanceSample>();
        _recentAttackResults = new Queue<bool>();
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
        SampleDistance();
    }

    // 일정 간격으로 Player와의 거리를 기록한다. 시야 밖(전투와 무관한 먼 거리)은 평균을 왜곡하므로 기록하지 않는다.
    private void SampleDistance()
    {
        if (Time.time < _nextDistanceSampleTime) return;
        _nextDistanceSampleTime = Time.time + distanceSampleInterval;

        if (playerTransform == null || _vision == null) return;

        Vector3 offset = playerTransform.position - transform.position;
        offset.y = 0f;
        float dist = offset.magnitude;
        if (dist > _vision.ViewDistance) return;

        _recentDistances.Enqueue(new DistanceSample { time = Time.time, distance = dist });
    }

    // Player의 공격이 막 시작되는 순간(폴링)을 감지해, 이 적의 시야 범위 안일 때만 공격 횟수로 센다.
    private void DetectPlayerAttackStart()
    {
        bool isAttackingNow = playerSwordAttack != null && playerSwordAttack.IsAttacking;
        bool justStarted = isAttackingNow && !_playerWasAttacking;
        _playerWasAttacking = isAttackingNow;

        if (!justStarted || playerTransform == null) return;

        Vector3 offset = playerTransform.position - transform.position;
        offset.y = 0f;
        float dist = offset.magnitude;
        // 사이드뷰에서도 앞뒤(Z) 이동이 생겨 NFBTEnemyAI와 같은 바닥 평면(XZ) 거리로 판정한다.
        if (dist <= _vision.ViewDistance)
        {
            _attackCount++;
            _recentAttackTimes.Enqueue(Time.time);
        }
    }

    private void HandleThisEnemyHit(float damage) => _hitCount++;

    // 이 적이 공격 사거리 근처에 있을 때 발생한 Player 피해만 이 적의 기여로 간주한다.
    private void HandlePlayerDamageTaken(float damage)
    {
        if (playerTransform == null) return;

        Vector3 offset = playerTransform.position - transform.position;
        offset.y = 0f;
        float dist = offset.magnitude;
        // 사이드뷰에서도 앞뒤(Z) 이동이 생겨 NFBTEnemyAI와 같은 바닥 평면(XZ) 거리로 판정한다.
        if (dist <= _combat.AttackRange * 1.5f)
            _totalDamage += damage;
    }

    // NPCFuzzyRL의 학습 입력으로 쓰이는 3차원 피처 벡터를 반환한다.
    public float[] GetFeatureVector() => new[] { AttackFrequency, HitRate, DamagePerSec };

    // ───── 최근 성향 피처 (공격 선택용, 모두 0~1) ─────

    // 최근 recentWindow초 동안 시야 안에서 Player가 공격을 시작한 빈도.
    [ShowInInspector, ReadOnly, LabelText("최근 공격성")]
    public float RecentAggression
    {
        get
        {
            if (!Application.isPlaying || _recentAttackTimes == null) return 0f;
            float oldest = Time.time - recentWindow;
            while (_recentAttackTimes.Count > 0 && _recentAttackTimes.Peek() < oldest)
                _recentAttackTimes.Dequeue();
            return Mathf.Clamp01(_recentAttackTimes.Count / (recentWindow * aggressionReference));
        }
    }

    // 최근 시야 안 Player와의 평균 거리 (0=붙어 있음, 1=멀리 있음).
    [ShowInInspector, ReadOnly, LabelText("최근 선호 거리")]
    public float RecentDistance
    {
        get
        {
            // 샘플이 없으면 Player가 시야 안에 없었던 것이므로 "멀리 있음(1)"으로 본다.
            if (!Application.isPlaying || _recentDistances == null) return 1f;
            float oldest = Time.time - recentWindow;
            while (_recentDistances.Count > 0 && _recentDistances.Peek().time < oldest)
                _recentDistances.Dequeue();
            if (_recentDistances.Count == 0) return 1f;

            float sum = 0f;
            foreach (DistanceSample sample in _recentDistances)
                sum += sample.distance;
            return Mathf.Clamp01(sum / _recentDistances.Count / distanceReference);
        }
    }

    // 최근 공격 결과 중 빗나간 비율.
    [ShowInInspector, ReadOnly, LabelText("최근 회피율")]
    public float RecentDodgeRate
    {
        get
        {
            // 결과가 없으면 아직 모르므로 어느 쪽에도 치우치지 않는 중립값 0.5를 쓴다.
            if (_recentAttackResults == null || _recentAttackResults.Count == 0) return 0.5f;
            int misses = 0;
            foreach (bool hit in _recentAttackResults)
                if (!hit) misses++;
            return (float)misses / _recentAttackResults.Count;
        }
    }

    // AI가 공격 한 번의 결과(명중 여부)를 알려 준다. 최근 dodgeResultCount개만 유지한다.
    public void RecordAttackResult(bool hit)
    {
        if (_recentAttackResults == null) _recentAttackResults = new Queue<bool>();
        _recentAttackResults.Enqueue(hit);
        while (_recentAttackResults.Count > Mathf.Max(1, dodgeResultCount))
            _recentAttackResults.Dequeue();
    }

    // 공격 결정 시점에 퍼지 입력으로 쓰는 { 공격성, 선호 거리, 회피율 }. 호출 빈도가 낮아 배열 할당은 허용한다.
    public float[] GetRecentFeatureVector() => new[] { RecentAggression, RecentDistance, RecentDodgeRate };

    [Button("최근 피처 로그")]
    private void LogRecentFeatures()
    {
        Debug.Log($"[CombatStatsTracker] 공격성 {RecentAggression:F2} / 선호 거리 {RecentDistance:F2} / 회피율 {RecentDodgeRate:F2}");
    }
}
