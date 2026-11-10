using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 베개 공격의 전방 OverlapBox 타격 판정 및 실제 명중 VFX.
[RequireComponent(typeof(Collider))]
public class SwordHitbox : MonoBehaviour
{
    [Title("타격 설정")]
    [SerializeField, LabelText("공격 대미지")]
    private float damage = 20f;
    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    [SerializeField, LabelText("PillowDurability (선택)")]
    private PillowDurability pillowDurability;

    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("앞 거리 (m)"), Min(0.1f)]
    private float forwardReach = 2.2f;
    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("뒤로 여유 (m)"), Min(0f)]
    private float backReach = 0.3f;
    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("높이 (발 기준, m)"), Min(0.1f)]
    private float height = 3f;
    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("깊이/폭 (m)"), Min(0.1f)]
    private float depth = 1.5f;

    [Title("타격 사운드")]
    [SerializeField, LabelText("타격음 클립")]
    private AudioClip hitClip;
    [SerializeField, LabelText("타격음 AudioSource")]
    private AudioSource hitAudioSource;
    [SerializeField, LabelText("타격음 음량"), Range(0f, 1f)]
    private float hitVolume = 0.9f;
    [SerializeField, LabelText("타격음 음높이 범위"), MinMaxSlider(0.5f, 1.5f, true)]
    private Vector2 hitPitchRange = new Vector2(0.9f, 1.1f);

    [Title("명중 VFX")]
    [SerializeField, LabelText("SwordAttack (자동 탐색, 선택)")]
    private SwordAttack swordAttack;
    [SerializeField, LabelText("VFX 타격 위치 높이 비율"), Range(0f, 1f)]
    private float hitVFXHeightRatio = 0.5f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("히트박스 활성 중")]
    private bool _isActive;
    private bool _hitSoundPlayed;
    private Collider _collider;
    private Transform _origin;
    private readonly HashSet<CharacterBase> _hitTargets = new();
    private readonly HashSet<IHittable> _hitHittables = new();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
        var player = GetComponentInParent<PlayerController>();
        _origin = player != null ? player.transform : transform.root;
        if (swordAttack == null)
            swordAttack = GetComponentInParent<SwordAttack>();
        DisableHitbox();
    }

    public void EnableHitbox()
    {
        _hitTargets.Clear();
        _hitHittables.Clear();
        _hitSoundPlayed = false;
        _isActive = true;
    }

    public void DisableHitbox() => _isActive = false;

    private void Update()
    {
        if (_isActive) CheckFrontArea();
    }

    private void CheckFrontArea()
    {
        GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
        Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation, hitLayers, QueryTriggerInteraction.Collide);
        foreach (Collider other in hits)
            ApplyHit(other);
    }

    private void ApplyHit(Collider other)
    {
        if (other.TryGetComponent<CharacterBase>(out var target))
        {
            if (transform.IsChildOf(target.transform)) return;
            if (!_hitTargets.Add(target)) return;

            // TakeDamage에서 피격 대상이 파괴될 수도 있으므로 먼저 타격 좌표를 계산한다.
            Vector3 hitPoint = GetVFXHitPoint(other);
            Vector3 hitNormal = GetVFXHitNormal(hitPoint);
            float multiplier = pillowDurability != null ? pillowDurability.GetDamageMultiplier() : 1f;
            target.TakeDamage(damage * multiplier);
            swordAttack?.PlayPillowHitVFX(hitPoint, hitNormal);
            PlayHitSound(hitPoint);
            return;
        }

        if (other.TryGetComponent<IHittable>(out var hittable) && _hitHittables.Add(hittable))
        {
            Vector3 hitPoint = GetVFXHitPoint(other);
            Vector3 hitNormal = GetVFXHitNormal(hitPoint);
            hittable.OnHit();
            swordAttack?.PlayPillowHitVFX(hitPoint, hitNormal);
            PlayHitSound(hitPoint);
        }
    }

    private Vector3 GetVFXHitPoint(Collider other)
    {
        Transform origin = _origin != null ? _origin : transform.root;
        Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

        // 박스 중심이 아니라 베개가 닿을 법한 앞쪽의 중간 높이에서 표면 최근접점을 찾는다.
        Vector3 approach = origin.position + Vector3.up * (height * hitVFXHeightRatio)
                           + forward * (forwardReach * 0.5f);
        return other.ClosestPoint(approach);
    }

    private Vector3 GetVFXHitNormal(Vector3 hitPoint)
    {
        Transform origin = _origin != null ? _origin : transform.root;
        Vector3 normal = origin.position + Vector3.up * (height * hitVFXHeightRatio) - hitPoint;
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f)
            normal = -Vector3.ProjectOnPlane(origin.forward, Vector3.up);
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
    }

    private void PlayHitSound(Vector3 hitPosition)
    {
        if (hitClip == null || _hitSoundPlayed) return;
        _hitSoundPlayed = true;
        if (hitAudioSource != null)
        {
            hitAudioSource.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
            hitAudioSource.PlayOneShot(hitClip, hitVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(hitClip, hitPosition, hitVolume);
        }
    }

    private void GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Transform origin = _origin != null ? _origin : transform.root;
        Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        rotation = Quaternion.LookRotation(forward, Vector3.up);
        float length = backReach + forwardReach;
        float centerForward = (forwardReach - backReach) * 0.5f;
        center = origin.position + forward * centerForward + Vector3.up * (height * 0.5f);
        halfExtents = new Vector3(depth * 0.5f, height * 0.5f, length * 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
        Gizmos.color = _isActive ? new Color(1f, 0.2f, 0.2f, 0.8f) : new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }

    [Button("히트박스 활성 테스트")]
    private void TestEnable() => EnableHitbox();
    [Button("히트박스 비활성 테스트")]
    private void TestDisable() => DisableHitbox();
    [Button("타격음 미리듣기 (Play Mode)")]
    private void TestHitSound()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[SwordHitbox] Play Mode에서만 테스트할 수 있습니다."); return; }
        _hitSoundPlayed = false;
        PlayHitSound(transform.position);
    }
    [Button("전방 타격 테스트 (1회)")]
    private void TestFrontHit()
    {
        EnableHitbox();
        CheckFrontArea();
        DisableHitbox();
    }
}
