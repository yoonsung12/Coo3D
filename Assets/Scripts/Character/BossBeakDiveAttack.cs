using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 평범한 패턴 중 근접 공격 "부리 급강하 찍기"를 담당한다. 별도의 경고 표시(빨간 범위, 깜빡임) 없이
// 보스가 뒤로 살짝 물러났다가(예비 동작) 그보다 빠른 속도로 플레이어 위치를 향해 급강하하는
// 움직임 자체가 예고 역할을 한다. 이동 중에는 몸통 히트박스(bodyHitbox)가 켜져서 스치기만 해도
// 데미지를 주고, 착지 순간에는 별도로 판정 반경 안의 플레이어에게 데미지를 준다.
// 화려한 폭발 대신 옅은 먼지가 살짝 이는 정도의 간단한 이펙트만 재생한다.
[RequireComponent(typeof(Rigidbody))]
public class BossBeakDiveAttack : MonoBehaviour, IBossBasicAttack
{
    [Title("예비 동작(대각선 위로 물러나기) 설정")]
    [SerializeField, LabelText("물러나는 거리 (수평)")]
    private float backHopDistance = 2f;

    [SerializeField, LabelText("물러나는 높이 (수직)")]
    private float backHopHeight = 2f;
    // 목표 반대쪽으로 물러나면서 이 높이만큼 위로도 튀어 오른다 — 이후 급강하가
    // 대각선 아래로 내리찍는 모양이 되도록 만드는 값이다.

    [SerializeField, LabelText("물러나는 속도")]
    private float backHopSpeed = 3f;
    // 값이 작을수록 천천히 물러난다 — 이 속도가 급강하 예고 역할을 한다.

    [Title("급강하 설정")]
    [SerializeField, LabelText("급강하 속도")]
    private float diveSpeed = 25f;
    // 물러나는 속도(backHopSpeed)보다 확실히 빠르게 맞춰야 "물러섰다가 확 달려든다"는 느낌이 난다.

    [SerializeField, LabelText("착지 지점 지면 여유 높이")]
    private float groundClearance = 1f;
    // 급강하 목표 지점의 Y좌표는 플레이어 위치(발밑 기준)에 이 값을 더한 높이다.
    // 보스의 BoxCollider 반높이(현재 1)만큼 띄워야 보스 몸체 중심이 땅 위에 정확히 걸쳐서
    // 착지하고, 이 값이 0이면 보스 몸체 절반이 바닥 아래로 파묻히듯 관통해 보인다.

    [SerializeField, LabelText("도착 판정 거리")]
    private float arriveThreshold = 0.1f;

    [Title("착지 판정 설정")]
    [SerializeField, LabelText("판정 반경")]
    private float impactRadius = 2f;

    [SerializeField, LabelText("데미지")]
    private float damage = 20f;
    // Player 체력 하트 1칸(20)에 맞춘 값이다.

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player가 속한 레이어(Default)를 체크한다.

    [SerializeField, LabelText("착지 후 정지 시간")]
    private float postImpactStunDuration = 0.5f;
    // 착지 직후 잠깐 멈춰서 반격 타이밍을 준다.

    [Title("몸통 접촉 판정")]
    [SerializeField, LabelText("이동 중 접촉 히트박스")]
    private EnemyAttackHitbox bodyHitbox;
    // Inspector에서 Boss 자식의 DiveHitbox(EnemyAttackHitbox, BossSpringPattern의 DashHitbox와 동일 구조)를
    // 연결한다. 물러나기+급강하 이동 중에만 켜져서, 착지 판정과 별개로 보스 몸에 스쳐도 데미지를 준다.

    [Title("연결")]
    [SerializeField, LabelText("플레이어 Transform")]
    private Transform playerTransform;

    [SerializeField, LabelText("착지 먼지 이펙트")]
    private ParticleSystem dustEffect;
    // Inspector에서 보스 발밑에 둔 먼지 파티클(Sphere/Hemisphere Shape 권장)을 연결한다.
    // Simulation Space를 World로 해두면 보스가 좌우로 회전해도 먼지 모양이 뒤틀리지 않는다.

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Execute(float dir, Action onFinished)
    {
        StartCoroutine(DiveRoutine(onFinished));
    }

    private IEnumerator DiveRoutine(Action onFinished)
    {
        // 플레이어 위치는 발밑 기준이라, 그대로 목표로 쓰면 보스 몸체(중심 기준) 절반이
        // 땅속에 파묻히듯 관통해 보인다. groundClearance만큼 위로 띄운 지점을 목표로 삼는다.
        Vector3 target = playerTransform.position + Vector3.up * groundClearance;

        // 목표 반대 방향(플레이어에게서 멀어지는 쪽) + 위쪽으로 대각선 점프한다.
        float towardTargetDir = Mathf.Sign(target.x - transform.position.x);
        Vector3 backPoint = transform.position + new Vector3(-towardTargetDir * backHopDistance, backHopHeight, 0f);

        SetPhysicsException(true);
        bodyHitbox?.EnableHitbox();

        yield return MoveTo(backPoint, backHopSpeed);
        yield return MoveTo(target, diveSpeed);

        bodyHitbox?.DisableHitbox();
        SetPhysicsException(false);

        JudgeImpact(target);
        dustEffect?.Play();

        yield return new WaitForSeconds(postImpactStunDuration);

        onFinished?.Invoke();
    }

    // 봄 패턴 돌진(BossSpringPattern)/평상시 비행(BossFlightMovement)과 동일한 물리 예외로,
    // 발판에 막히지 않고 목표 지점까지 직선으로 이동한다.
    // Kinematic인 상태에서 linearVelocity를 건드리면 경고가 뜨므로, isKinematic을 먼저 풀고 나서
    // 속도를 초기화하는 순서를 지킨다(켤 때는 반대로 속도부터 정리한 뒤 Kinematic으로 전환한다).
    private void SetPhysicsException(bool enable)
    {
        if (enable)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.useGravity = false;
            _rb.isKinematic = true;
        }
        else
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = Vector3.zero;
        }
    }

    private IEnumerator MoveTo(Vector3 destination, float speed)
    {
        while (Vector3.Distance(_rb.position, destination) > arriveThreshold)
        {
            Vector3 next = Vector3.MoveTowards(_rb.position, destination, speed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }
    }

    // 착지 지점 반경 안의 CharacterBase(플레이어)에게 데미지를 준다.
    private void JudgeImpact(Vector3 position)
    {
        Collider[] cols = Physics.OverlapSphere(position, impactRadius, hitLayers);
        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<CharacterBase>(out var target))
                target.TakeDamage(damage);
        }
    }

    [Button("부리 급강하 찍기 테스트")]
    private void TestDive() => Execute(1f, null);
}
