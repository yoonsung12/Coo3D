using Sirenix.OdinInspector;
using UnityEngine;

// 공격 히트박스(EnemyAttackHitbox)와 별개로, 적의 몸통에 닿아 있기만 해도 일정 간격으로
// 데미지를 준다. 돌진하거나 몸으로 부딪혀오는 적(예: 비둘기)처럼 "몸통 자체가 위협"인 경우에 사용한다.
// EnemyAttackHitbox와 동일한 구조(hitLayers + CharacterBase)를 따르되, 한 번의 판정으로 끝나지 않고
// damageInterval마다 반복 적용된다는 점이 다르다.
[RequireComponent(typeof(Collider))]
public class EnemyContactDamage : MonoBehaviour
{
    [Title("접촉 데미지 설정")]
    [SerializeField, LabelText("접촉 데미지")]
    private float damage = 5f;
    // 몸통에 닿아 있는 동안 매 간격마다 깎이는 체력량이다.

    [SerializeField, LabelText("데미지 간격(초)")]
    private float damageInterval = 1f;
    // 값이 작을수록 몸에 닿아있는 동안 더 자주(더 아프게) 데미지를 준다.

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player가 속한 레이어를 체크한다.

    private Collider _collider;
    private float _nextDamageTime;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        // 몸통 접촉 판정은 트리거로만 감지한다. 실제 물리적 밀림/충돌은 Enemy 루트의
        // 별도 콜라이더(Rigidbody 물리용)가 담당하므로 이 콜라이더와 서로 간섭하지 않는다.
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other);

    private void OnTriggerStay(Collider other) => TryDamage(other);

    private void TryDamage(Collider other)
    {
        if (Time.time < _nextDamageTime) return;
        if ((hitLayers & (1 << other.gameObject.layer)) == 0) return;

        if (!other.TryGetComponent<CharacterBase>(out var target)) return;

        target.TakeDamage(damage);
        _nextDamageTime = Time.time + damageInterval;
    }
}
