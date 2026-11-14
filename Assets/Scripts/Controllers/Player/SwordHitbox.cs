using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 베개 공격의 전방 OverlapBox 판정, 피격 VFX, 사운드 및 Hit Stop 처리.
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

    [Title("명중 피드백")]
    [SerializeField, LabelText("Player의 PillowHitFeedback")]
    private PillowHitFeedback pillowHitFeedback;

    [Title("명중 VFX")]
    [SerializeField, LabelText("SwordAttack (자동 탐색, 선택)")]
    private SwordAttack swordAttack;
    [SerializeField, LabelText("VFX 타격 위치 높이 비율"), Range(0f, 1f)]
    private float hitVFXHeightRatio = 0.5f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("히트박스 활성 중")]
    private bool _isActive;

    private bool _hitSoundPlayed;
    private bool _hitFeedbackPlayed;
    private Collider _collider;
    private Transform _origin;
    private readonly HashSet<CharacterBase> _hitTargets = new HashSet<CharacterBase>();
    private readonly HashSet<IHittable> _hitHittables = new HashSet<IHittable>();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;

        PlayerController player = GetComponentInParent<PlayerController>();
        _origin = player != null ? player.transform : transform.root;
        if (swordAttack == null)
            swordAttack = GetComponentInParent<SwordAttack>();
        if (pillowHitFeedback == null)
            pillowHitFeedback = GetComponentInParent<PillowHitFeedback>();

        DisableHitbox();
    }

    // SwordAttack의 베개 내려찍기 시작 시 호출.
    public void EnableHitbox()
    {
        _hitTargets.Clear();
        _hitHittables.Clear();
        _hitSoundPlayed = false;
        _hitFeedbackPlayed = false;
        _isActive = true;
    }

    public void DisableHitbox()
    {
        _isActive = false;
    }

    private void Update()
    {
        if (_isActive)
            CheckFrontArea();
    }

    private void CheckFrontArea()
    {
        GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
        Collider[] hits = Physics.OverlapBox(
            center, halfExtents, rotation, hitLayers, QueryTriggerInteraction.Collide);

        foreach (Collider other in hits)
        {
            if (other != null)
                ApplyHit(other);
        }
    }

    private void ApplyHit(Collider other)
    {
        // 기존 코드처럼 CharacterBase가 콜라이더 오브젝트에 붙어 있는 경우를 우선 처리.
        if (other.TryGetComponent<CharacterBase>(out CharacterBase target))
        {
            if (transform.IsChildOf(target.transform)) return;
            if (!_hitTargets.Add(target)) return;

            // TakeDamage로 대상이 파괴되기 전에 접촉 위치를 구한다.
            Vector3 hitPoint = GetVFXHitPoint(other);
            Vector3 hitNormal = GetVFXHitNormal(hitPoint);
            float multiplier = pillowDurability != null
                ? pillowDurability.GetDamageMultiplier() : 1f;

            target.TakeDamage(damage * multiplier);
            PlayPillowHitVFX(hitPoint, hitNormal);
            PlayHitSound(hitPoint);
            TriggerHitFeedback();
            return;
        }

        if (other.TryGetComponent<IHittable>(out IHittable hittable)
            && _hitHittables.Add(hittable))
        {
            Vector3 hitPoint = GetVFXHitPoint(other);
            Vector3 hitNormal = GetVFXHitNormal(hitPoint);

            hittable.OnHit();
            PlayPillowHitVFX(hitPoint, hitNormal);
            PlayHitSound(hitPoint);
            TriggerHitFeedback();
        }
    }

    // 대상의 높이 비율을 적용해 타격 VFX를 띄울 대략적인 지점을 얻는다.
    // OverlapBox는 접촉점 정보를 주지 않으므로 Collider.ClosestPoint로 근사한다.
    private Vector3 GetVFXHitPoint(Collider other)
    {
        Bounds bounds = other.bounds;
        Vector3 candidate = bounds.center;
        candidate.y = Mathf.Lerp(bounds.min.y, bounds.max.y, hitVFXHeightRatio);

        Transform origin = _origin != null ? _origin : transform.root;
        Vector3 closestPoint = other.ClosestPoint(origin.position + Vector3.up *
            Mathf.Max(0.1f, height * hitVFXHeightRatio));

        // 아주 큰 콜라이더에서 과하게 떨어진 위치가 나오는 것을 막기 위한 높이 보정.
        closestPoint.y = Mathf.Clamp(candidate.y, bounds.min.y, bounds.max.y);
        return closestPoint;
    }

    // 타격 효과가 플레이어 쪽을 향하도록 하는 대략적인 법선 방향.
    private Vector3 GetVFXHitNormal(Vector3 hitPoint)
    {
        Transform origin = _origin != null ? _origin : transform.root;
        Vector3 direction = origin.position - hitPoint;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -origin.forward;
        return direction.normalized;
    }

    private void PlayPillowHitVFX(Vector3 hitPoint, Vector3 hitNormal)
    {
        // SwordAttack에 이미 존재하는 명중 VFX 메서드 사용.
        if (swordAttack != null)
            swordAttack.PlayPillowHitVFX(hitPoint, hitNormal);
    }

    // 공격 한 번에 여러 대상이 맞아도 Hit Stop / Camera Shake는 한 번만 발생.
    private void TriggerHitFeedback()
    {
        if (_hitFeedbackPlayed) return;
        _hitFeedbackPlayed = true;

        if (pillowHitFeedback != null)
            pillowHitFeedback.PlayHitFeedback();
    }

    private void PlayHitSound(Vector3 hitPosition)
    {
        if (_hitSoundPlayed || hitClip == null) return;
        _hitSoundPlayed = true;

        if (hitAudioSource != null)
        {
            hitAudioSource.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
            hitAudioSource.PlayOneShot(hitClip, hitVolume);
        }
        else
        {
            SfxPlayer.PlayAt(hitClip, hitPosition, hitVolume);
        }
    }

    // 기존 판정 범위와 동일한 OverlapBox 계산.
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
        Gizmos.color = _isActive
            ? new Color(1f, 0.2f, 0.2f, 0.8f)
            : new Color(1f, 0.85f, 0.2f, 0.6f);
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
        if (!Application.isPlaying) return;
        _hitSoundPlayed = false;
        PlayHitSound(transform.position);
    }

    [Button("전방 타격 테스트 (1회)")]
    private void TestFrontHit()
    {
        if (!Application.isPlaying) return;
        EnableHitbox();
        CheckFrontArea();
        DisableHitbox();
    }
}
