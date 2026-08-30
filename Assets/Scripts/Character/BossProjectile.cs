using Sirenix.OdinInspector;
using UnityEngine;

// 보스의 원거리 공격용 직선 투사체다. Boss.cs가 Instantiate 직후 Launch()로 방향/속도/데미지를 주입한다.
// RainDrop/GinkgoFruit(계절 낙하물)와 같은 방식으로 Rigidbody를 물리 이동에 사용하되,
// 중력 대신 일정한 속도로만 직선 이동한다는 점이 다르다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BossProjectile : MonoBehaviour
{
    [Title("충돌 설정")]
    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player가 속한 레이어(Default)를 체크한다.

    [SerializeField, LabelText("장애물 레이어")]
    private LayerMask obstacleLayers;
    // Inspector에서 발판/바닥 등 투사체가 막혀야 하는 레이어를 체크한다.

    [Title("수명 설정")]
    [SerializeField, LabelText("최대 생존 시간")]
    private float lifeTime = 5f;
    // 아무것도 맞지 않고 이 시간이 지나면 자동으로 사라진다. 아레나 밖으로 날아가 계속 남는 것을 막는다.

    private Rigidbody _rb;
    private Vector3 _velocity;
    private float _damage;
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

    // Boss가 발사 직후 호출해 이동 방향/속도/데미지를 설정한다.
    public void Launch(Vector3 direction, float speed, float damage)
    {
        _velocity = direction.normalized * speed;
        _damage = damage;
    }

    private void FixedUpdate()
    {
        _rb.MovePosition(_rb.position + _velocity * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        if (IsInLayerMask(other.gameObject.layer, hitLayers) &&
            other.TryGetComponent<CharacterBase>(out var target))
        {
            _hasHit = true;
            target.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }

        if (IsInLayerMask(other.gameObject.layer, obstacleLayers))
        {
            _hasHit = true;
            Destroy(gameObject);
        }
    }

    private static bool IsInLayerMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
}
