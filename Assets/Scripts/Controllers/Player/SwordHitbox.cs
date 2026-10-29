using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어 공격(베개 내려찍기)의 타격 판정을 담당하는 컴포넌트다.
// SwordAttack에서 내려찍기 타이밍에 맞춰 EnableHitbox / DisableHitbox를 호출한다.
// 예전에는 칼날 콜라이더가 휘두르는 궤적에 닿은 대상을 때렸지만, 이제는 판정이 켜진 동안
// 플레이어가 바라보는 앞쪽의 상자 영역(OverlapBox)에 들어온 대상을 때린다.
// CharacterBase를 가진 오브젝트에는 TakeDamage를, 고드름 같은 환경 오브젝트에는 IHittable.OnHit을 적용하며,
// 한 번의 공격에서 같은 대상을 중복 타격하지 않도록 HashSet으로 관리한다.
[RequireComponent(typeof(Collider))]
public class SwordHitbox : MonoBehaviour
{
    [Title("타격 설정")]
    [SerializeField, LabelText("공격 대미지")]
    private float damage = 20f;
    // 판정 영역에 들어온 대상에게 한 번 적용할 대미지 값이다.

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // 타격 판정이 적용될 레이어를 지정한다.
    // Inspector에서 Enemy 레이어 등 공격에 맞아야 하는 오브젝트의 레이어를 체크한다.

    [SerializeField, LabelText("PillowDurability (선택)")]
    private PillowDurability pillowDurability;
    // Inspector에서 Player의 PillowDurability를 연결하면, 내구도가 소진됐을 때 데미지가 줄어든다.
    // 비워두면 항상 기본 데미지 그대로 적용된다(배게가 아직 없는 다른 무기에도 재사용 가능).

    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("앞 거리 (m)"), Min(0.1f)]
    private float forwardReach = 2.2f;
    // 플레이어 중심에서 바라보는 방향으로 이 거리까지 맞는다. 값이 클수록 멀리 있는 적도 맞는다.

    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("뒤로 여유 (m)"), Min(0f)]
    private float backReach = 0.3f;
    // 플레이어 중심보다 이만큼 뒤에서부터 판정을 시작한다.
    // 바로 위에 매달린 고드름처럼 몸 중심 근처에 있는 대상을 놓치지 않게 하기 위한 값이다.

    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("높이 (발 기준, m)"), Min(0.1f)]
    private float height = 3f;
    // 발밑부터 이 높이까지 맞는다. 머리 위에 매달린 고드름을 치려면 3m 정도가 필요하다.

    [BoxGroup("전방 판정 범위")]
    [SerializeField, LabelText("깊이/폭 (m)"), Min(0.1f)]
    private float depth = 1.5f;
    // 바라보는 방향에 수직인 수평 폭이다. 사이드뷰에서는 화면 안쪽(Z) 두께, 탑다운에서는 좌우 폭이 된다.

    [Title("타격 사운드")]
    [SerializeField, LabelText("타격음 클립")]
    private AudioClip hitClip;
    // 베개가 대상(적, 고드름 등)을 실제로 맞힌 순간 재생할 "퍽" 소리다. 허공을 휘두를 때는 재생되지 않는다.
    // 비워두면 타격음 없이 판정만 동작한다.

    [SerializeField, LabelText("타격음 AudioSource")]
    private AudioSource hitAudioSource;
    // Player의 공격 전용 AudioSource를 연결한다(SwordAttack과 같이 써도 된다). 비워두면 맞은 위치에서 PlayClipAtPoint로 재생한다.

    [SerializeField, LabelText("타격음 음량"), Range(0f, 1f)]
    private float hitVolume = 0.9f;

    [SerializeField, LabelText("타격음 음높이 범위"), MinMaxSlider(0.5f, 1.5f, true)]
    private Vector2 hitPitchRange = new Vector2(0.9f, 1.1f);
    // 매번 이 범위 안에서 pitch를 골라 같은 타격음도 조금씩 다르게 들리게 한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("히트박스 활성 중")]
    private bool _isActive;

    private bool _hitSoundPlayed;
    // 한 번의 공격에서 타격음을 이미 냈는지 기록한다. 여러 대상을 한 번에 맞혀도 소리가 겹쳐 커지지 않게 한 번만 낸다.

    private Collider _collider;
    // 예전 칼날 판정용 콜라이더다. 이제 판정에 쓰지 않으므로 항상 꺼 둔다.

    private Transform _origin;
    // 판정 상자의 기준이 되는 플레이어 루트다. 발밑 위치와 몸이 바라보는 방향(forward)을 사용한다.
    // 이 컴포넌트는 플레이어 자식(SW08)에 붙어 있으므로 부모의 PlayerController를 찾아 자동으로 쓴다.

    // 한 번의 공격에서 이미 타격한 대상을 기록해 중복 타격을 방지한다.
    // 판정이 켜진 동안 매 프레임 영역을 검사하므로, 적뿐 아니라 고드름(IHittable)도 기록해야 한 번만 맞는다.
    private readonly HashSet<CharacterBase> _hitTargets = new();
    private readonly HashSet<IHittable> _hitHittables = new();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 칼날 콜라이더는 더 이상 판정에 쓰지 않는다. 트리거로 두고 꺼서 물리 충돌에도 끼어들지 않게 한다.
        _collider.isTrigger = true;
        _collider.enabled = false;

        var player = GetComponentInParent<PlayerController>();
        _origin = player != null ? player.transform : transform.root;

        DisableHitbox();
    }

    // SwordAttack에서 내려찍기가 시작될 때 호출된다.
    public void EnableHitbox()
    {
        // 새 공격이 시작될 때 이전 공격의 히트 기록을 초기화해 다시 타격 가능하게 한다.
        _hitTargets.Clear();
        _hitHittables.Clear();
        _hitSoundPlayed = false;
        _isActive = true;
    }

    // SwordAttack에서 내려찍기가 끝날 때 호출된다.
    public void DisableHitbox()
    {
        _isActive = false;
    }

    private void Update()
    {
        // 판정은 내려찍는 짧은 구간(3~4프레임)에만 켜지므로, 그동안만 매 프레임 영역을 검사한다.
        if (_isActive)
            CheckFrontArea();
    }

    // 플레이어 앞쪽 상자 영역 안의 대상을 찾아 타격한다.
    private void CheckFrontArea()
    {
        GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);

        // 녹기 전 얼음처럼 트리거 콜라이더를 가진 대상도 맞아야 하므로 트리거까지 감지한다.
        Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation, hitLayers, QueryTriggerInteraction.Collide);

        foreach (Collider other in hits)
            ApplyHit(other);
    }

    // 감지된 콜라이더 하나에 데미지 또는 타격 반응을 적용한다.
    private void ApplyHit(Collider other)
    {
        if (other.TryGetComponent<CharacterBase>(out var target))
        {
            // hitLayers에 Default를 넣은 씬(고드름이 Default 레이어)에서는 같은 Default 레이어인 플레이어 몸도 잡힌다.
            // 공격하는 캐릭터 자신은 맞지 않게 걸러낸다.
            if (transform.IsChildOf(target.transform)) return;

            // 한 번의 공격에서 같은 대상이 여러 프레임에 걸쳐 중복 타격되지 않도록 한다.
            if (!_hitTargets.Add(target)) return;

            float multiplier = pillowDurability != null ? pillowDurability.GetDamageMultiplier() : 1f;
            target.TakeDamage(damage * multiplier);
            PlayHitSound(other.transform.position);
            return;
        }

        // 체력이 없는 환경 오브젝트(고드름 등)는 IHittable로 처리한다.
        if (other.TryGetComponent<IHittable>(out var hittable) && _hitHittables.Add(hittable))
        {
            hittable.OnHit();
            PlayHitSound(other.transform.position);
        }
    }

    // 실제로 무언가를 맞혔을 때 베개 타격음을 공격 1회당 한 번만 재생한다.
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

    // 판정 상자의 중심, 절반 크기, 회전을 계산한다. Gizmo 표시와 실제 판정이 같은 값을 쓴다.
    private void GetBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        Transform origin = _origin != null ? _origin : transform.root;

        // 몸이 바라보는 방향에서 위아래 성분을 빼 수평 방향만 쓴다.
        // 사이드뷰는 ±진행 축, 탑다운은 8방향이 되어 두 시점 모두 자동으로 앞쪽을 가리킨다.
        Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        rotation = Quaternion.LookRotation(forward, Vector3.up);

        // 앞뒤 길이 = 뒤로 여유 + 앞 거리. 상자 중심은 그 구간의 가운데, 높이는 발밑~height의 가운데다.
        float length = backReach + forwardReach;
        float centerForward = (forwardReach - backReach) * 0.5f;
        center = origin.position + forward * centerForward + Vector3.up * (height * 0.5f);

        // 회전된 상자 기준으로 x = 폭(깊이), y = 높이, z = 앞뒤 길이다.
        halfExtents = new Vector3(depth * 0.5f, height * 0.5f, length * 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        // Scene 창에서 SW08을 선택하면 판정 범위를 보여준다. 판정이 켜진 순간에는 빨갛게 표시된다.
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
        // Play Mode에서 누르면 지금 판정 범위 안의 대상을 한 번 바로 타격한다.
        EnableHitbox();
        CheckFrontArea();
        DisableHitbox();
    }
}
