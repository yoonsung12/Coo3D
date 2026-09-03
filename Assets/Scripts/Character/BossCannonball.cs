using Sirenix.OdinInspector;
using UnityEngine;

// 여름 패턴 대포(BossCannon)에서 발사되는 포탄이다. 데미지 없이, 레이저를 쏘고 있는 보스를
// 저지하는 용도로 목표 지점(발사 시점의 보스 위치)까지 직선으로 날아가 명중하면
// BossSummerPattern에게 알리고 스스로 사라진다.
// 이동 방식은 BossProjectile과 같지만(Rigidbody Kinematic + MovePosition으로 일정 속도 직선 이동),
// 플레이어가 아니라 보스를 향해 날아가고 데미지 대신 저지 신호를 보낸다는 점이 다르다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BossCannonball : MonoBehaviour
{
    [Title("이동 설정")]
    [SerializeField, LabelText("이동 속도")]
    private float speed = 18f;

    [Title("충돌 설정")]
    [SerializeField, LabelText("보스 레이어")]
    private LayerMask bossLayer;
    // Inspector에서 Boss가 속한 레이어(Enemy)를 지정한다.

    [SerializeField, LabelText("최대 생존 시간")]
    private float lifeTime = 4f;
    // 아무것도 맞지 않고 이 시간이 지나면 자동으로 사라진다.

    private Rigidbody _rb;
    private Vector3 _velocity;
    private bool _hasHit;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;
        // 중력 없이 일정 속도로만 이동하고, 스스로 위치를 옮기므로 Kinematic으로 둔다.

        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    // BossCannon이 발사 직후 호출해 목표 지점(발사 시점의 보스 위치)을 향한 방향을 설정한다.
    public void Launch(Vector3 targetPosition)
    {
        Vector3 dir = (targetPosition - transform.position).normalized;
        _velocity = dir * speed;

        // 사이드뷰(X-Y 평면) 기준으로 날아가는 방향을 바라보도록 Z축 회전시킨다.
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FixedUpdate()
    {
        _rb.MovePosition(_rb.position + _velocity * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (!IsInLayerMask(other.gameObject.layer, bossLayer)) return;
        if (!other.TryGetComponent<BossSummerPattern>(out var summerPattern)) return;

        _hasHit = true;
        summerPattern.InterruptLaser();
        Destroy(gameObject);
    }

    private static bool IsInLayerMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
}
