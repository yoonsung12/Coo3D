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

    [Title("연출")]
    [SerializeField, LabelText("공격 이펙트")]
    private ParticleSystem attackEffect;
    // 히트박스가 켜지는 순간(=실제로 판정이 살아있는 타이밍) 재생해서 "지금 공격 중"임을 눈에 보이게 한다.
    // 비워두면 기존처럼 이펙트 없이 판정만 동작한다(하위 호환).

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
        if (_collider != null) _collider.enabled = true;
        // _collider는 Awake()에서 캐싱되는데, 다른 오브젝트에 있는 스크립트가 이 메서드를
        // 자신의 Awake()에서 호출하면 실행 순서가 보장되지 않아 아직 null일 수 있다(예: BossSpringPattern).
        // 그 경우 조용히 무시하고, 실제 Awake()가 실행되면 DisableHitbox()가 다시 호출되어 정상 상태가 된다.
        _isActive = true;

        attackEffect?.Play();
    }

    public void DisableHitbox()
    {
        if (_collider != null) _collider.enabled = false;
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
