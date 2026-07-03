using Sirenix.OdinInspector;
using UnityEngine;

// 적의 공격 판정을 담당한다. SwordHitbox와 동일한 구조로,
// EnemyCombat이 공격 모션 타이밍에 맞춰 EnableHitbox / DisableHitbox를 호출한다.
[RequireComponent(typeof(Collider))]
public class EnemyAttackHitbox : MonoBehaviour
{
    [Title("타격 설정")]
    [SerializeField, LabelText("공격 대미지")]
    private float damage = 10f;

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player 레이어를 체크한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("히트박스 활성 중")]
    private bool _isActive;

    private Collider _collider;
    private bool _hasHitThisSwing;
    // 한 번의 공격 모션에서 같은 대상을 중복 타격하지 않도록 막는다.

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        DisableHitbox();
    }

    public void EnableHitbox()
    {
        _hasHitThisSwing = false;
        _collider.enabled = true;
        _isActive = true;
    }

    public void DisableHitbox()
    {
        _collider.enabled = false;
        _isActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHitThisSwing) return;
        if ((hitLayers & (1 << other.gameObject.layer)) == 0) return;

        if (!other.TryGetComponent<CharacterBase>(out var target)) return;

        _hasHitThisSwing = true;
        target.TakeDamage(damage);
    }
}
