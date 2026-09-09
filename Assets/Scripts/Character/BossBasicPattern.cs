using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

// 최종보스 "비둘킹"의 평범한 패턴(상시 공격)을 관리한다. 계절 패턴(무적)과 무관하게 항상 시도되는
// 사이클로, 거리에 따라 부리 쪼기/깃털 부채꼴 발사/날개 돌풍 중 하나를 골라 실행한다.
// 기존에는 Boss.cs가 거리만 보고 근접/원거리 이지선다로 직접 처리했지만(HandleBasicAttack),
// 공격 종류가 세 가지로 늘어나면서 스케줄링 책임을 이 컴포넌트로 분리했다.
[RequireComponent(typeof(Boss), typeof(EnemyMovement))]
public class BossBasicPattern : MonoBehaviour
{
    [Title("스케줄링 설정")]
    [SerializeField, LabelText("근접 판정 거리")]
    private float closeRange = 3f;
    // 이 거리 안이면 "근접 상황"으로 판단해 부리 쪼기/날개 돌풍 중 하나를 고른다.
    // 이 거리보다 멀면 항상 깃털 부채꼴 발사를 한다.

    [SerializeField, LabelText("공격 사이클 간격")]
    private float cycleInterval = 1.2f;
    // 한 공격이 끝난 뒤 다음 공격을 시작하기까지 대기하는 시간이다.

    [SerializeField, LabelText("근접 상황에서 날개 돌풍 확률"), Range(0f, 1f)]
    private float wingGustChance = 0.3f;
    // 근접 상황일 때 이 확률로 날개 돌풍을, 나머지는 부리 쪼기를 선택한다.

    [Title("연결")]
    [SerializeField, LabelText("부리 급강하 찍기")]
    private BossBeakDiveAttack beakDive;

    [SerializeField, LabelText("깃털 부채꼴 발사")]
    private BossFeatherFanAttack featherFan;

    [SerializeField, LabelText("날개 돌풍")]
    private BossWingGustAttack wingGust;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("공격 진행 중")]
    private bool _isAttacking;

    private Boss _boss;
    private EnemyMovement _movement;
    private float _timer;

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _movement = GetComponent<EnemyMovement>();
        _timer = cycleInterval;
    }

    private void Update()
    {
        if (_boss.IsDead) return;
        if (!_boss.CanBasicAttack) return;
        if (_isAttacking) return;
        if (_boss.PlayerTransform == null) return;

        float dx = _boss.PlayerTransform.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);
        float distance = Mathf.Abs(dx);

        _movement.FaceDirection(dir);

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = cycleInterval;
        ExecuteAttack(dir, distance);
    }

    private void ExecuteAttack(float dir, float distance)
    {
        if (distance <= closeRange)
        {
            bool useGust = Random.value < wingGustChance;
            StartAttack(useGust ? (IBossBasicAttack)wingGust : beakDive, dir);
        }
        else
        {
            StartAttack(featherFan, dir);
        }
    }

    private void StartAttack(IBossBasicAttack attack, float dir)
    {
        if (attack == null) return;

        _isAttacking = true;
        _boss.SetBasicAttacking(true);
        attack.Execute(dir, OnAttackFinished);
    }

    private void OnAttackFinished()
    {
        _isAttacking = false;
        _boss.SetBasicAttacking(false);
    }

    [Button("근접 공격 강제 실행 (테스트)")]
    private void TestForceCloseAttack() => ExecuteAttack(_movement.FacingDir, 0f);

    [Button("원거리 공격 강제 실행 (테스트)")]
    private void TestForceFarAttack() => ExecuteAttack(_movement.FacingDir, closeRange + 1f);
}
