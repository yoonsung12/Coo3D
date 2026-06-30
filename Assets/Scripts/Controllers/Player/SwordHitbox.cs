using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// SW08 칼날의 타격 판정을 담당하는 컴포넌트다.
// SwordAttack에서 베기 모션 타이밍에 맞춰 EnableHitbox / DisableHitbox를 호출한다.
// CharacterBase를 가진 오브젝트에 TakeDamage를 적용하며,
// 한 번의 베기에서 같은 대상을 중복 타격하지 않도록 HashSet으로 관리한다.
[RequireComponent(typeof(Collider))]
public class SwordHitbox : MonoBehaviour
{
    [Title("타격 설정")]
    [SerializeField, LabelText("공격 대미지")]
    private float damage = 20f;
    // 칼에 맞은 대상에게 한 번 적용할 대미지 값이다.

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // 타격 판정이 적용될 레이어를 지정한다.
    // Inspector에서 Enemy 레이어 등 칼에 맞아야 하는 오브젝트의 레이어를 체크한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("히트박스 활성 중")]
    private bool _isActive;

    private Collider _collider;

    // 한 번의 베기에서 이미 타격한 대상을 기록해 중복 타격을 방지한다.
    private readonly HashSet<CharacterBase> _hitTargets = new();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 칼날 콜라이더는 물리 충돌이 아닌 감지 전용이므로 트리거로 설정한다.
        _collider.isTrigger = true;
        DisableHitbox();
    }

    // SwordAttack에서 베기 모션이 시작될 때 호출된다.
    public void EnableHitbox()
    {
        // 새 베기가 시작될 때 이전 베기의 히트 기록을 초기화해 다시 타격 가능하게 한다.
        _hitTargets.Clear();
        _collider.enabled = true;
        _isActive = true;
    }

    // SwordAttack에서 베기 모션이 끝날 때 호출된다.
    public void DisableHitbox()
    {
        _collider.enabled = false;
        _isActive = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // hitLayers에 지정된 레이어에 속하지 않으면 무시한다.
        // 비트 연산으로 레이어 마스크와 비교한다.
        if ((hitLayers & (1 << other.gameObject.layer)) == 0) return;

        if (!other.TryGetComponent<CharacterBase>(out var target)) return;

        // 한 번의 베기에서 같은 대상이 여러 프레임에 걸쳐 중복 감지되지 않도록 한다.
        if (_hitTargets.Contains(target)) return;

        _hitTargets.Add(target);
        target.TakeDamage(damage);
    }

    [Button("히트박스 활성 테스트")]
    private void TestEnable() => EnableHitbox();

    [Button("히트박스 비활성 테스트")]
    private void TestDisable() => DisableHitbox();
}
