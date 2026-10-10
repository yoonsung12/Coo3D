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

    [Title("디버그")]
    [SerializeField, LabelText("씬에 범위 표시")]
    private bool showRangeInScene = true;
    // 켜면 Scene 뷰에 공격 범위를 그린다. 공격 판정이 살아있는 동안은 선택하지 않아도 빨간 박스로,
    // 평소에는 선택했을 때만 주황 테두리로 보인다. 기즈모라 빌드된 게임 화면에는 나오지 않는다.

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

        // attackEffect?.Play() 형태는 Inspector에서 비워둔(미할당) ParticleSystem 참조에 대해
        // UnassignedReferenceException을 던지는 Unity 고유 이슈가 있어, 명시적 null 비교로 우회한다.
        if (attackEffect != null)
            attackEffect.Play();
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

#if UNITY_EDITOR
    // 공격 판정이 켜져 있는 순간에는 선택 여부와 상관없이 Scene 뷰에 빨간 박스로 보여준다.
    private void OnDrawGizmos()
    {
        if (!showRangeInScene || !_isActive) return;
        DrawRangeGizmo(new Color(1f, 0.1f, 0.1f, 0.35f), Color.red);
    }

    // 평소에는 오브젝트를 선택했을 때만 범위 위치를 연한 테두리로 보여준다.
    private void OnDrawGizmosSelected()
    {
        if (!showRangeInScene || _isActive) return;
        DrawRangeGizmo(Color.clear, new Color(1f, 0.6f, 0.2f, 0.8f));
    }

    // 실제 판정 콜라이더의 모양/크기/회전을 그대로 그린다. fillColor가 투명이면 테두리만 그린다.
    private void DrawRangeGizmo(Color fillColor, Color wireColor)
    {
        Collider col = _collider != null ? _collider : GetComponent<Collider>();
        if (col == null) return;

        if (col is BoxCollider box)
        {
            // 로컬 좌표계로 그려야 오브젝트 회전/스케일이 콜라이더와 똑같이 반영된다.
            Gizmos.matrix = transform.localToWorldMatrix;
            if (fillColor.a > 0f) { Gizmos.color = fillColor; Gizmos.DrawCube(box.center, box.size); }
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Vector3 center = transform.TransformPoint(sphere.center);
            Vector3 scale = transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (fillColor.a > 0f) { Gizmos.color = fillColor; Gizmos.DrawSphere(center, radius); }
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(center, radius);
        }
        else
        {
            // 그 외 콜라이더는 월드 bounds 박스로 대신 표시한다. 꺼진 콜라이더는 bounds가 0이므로 공격 중에만 정확하다.
            Bounds b = col.bounds;
            if (fillColor.a > 0f) { Gizmos.color = fillColor; Gizmos.DrawCube(b.center, b.size); }
            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(b.center, b.size);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
