using UnityEngine;
using Sirenix.OdinInspector;

// 플레이어가 땅 위를 걸을 때 발소리를 재생한다.
// 걷기 애니메이션(Player_Run)에서 발이 앞으로 나가 바닥에 닿는 프레임에 Animation Event가 있고,
// 모델 쪽 PlayerFootstepAnimationEvent가 그 이벤트를 받아 PlayFootstepFromAnimation()을 호출한다.
// 그래서 소리가 화면 속 발 디딤과 정확히 맞는다.
[RequireComponent(typeof(CharacterController))]
public class PlayerFootstep : MonoBehaviour
{
    [Title("오디오 연결")]
    [SerializeField, LabelText("발소리 전용 AudioSource")]
    private AudioSource footstepAudioSource;
    // Player에는 선풍기 등 다른 도구용 AudioSource가 이미 붙어 있으므로, 발소리만 쓰는 AudioSource를 따로 연결한다.
    // 재생할 때마다 pitch를 바꾸기 때문에 다른 소리와 같이 쓰면 그 소리의 음높이까지 바뀐다.

    [Title("발소리 클립")]
    [SerializeField, LabelText("발소리 클립 목록")]
    private AudioClip[] footstepClips;
    // 여러 개를 넣으면 매번 무작위로 골라 반복감이 줄어든다. 직전에 재생한 클립은 연속으로 고르지 않는다.
    // 바닥 종류가 기본(벽돌, 보도블록 등)이거나 아래 설정이 비어 있을 때 쓰는 기본 발소리다.

    [Title("바닥별 발소리")]
    [SerializeField, LabelText("바닥별 발소리 설정")]
    private FootstepSoundSet soundSet;
    // Assets의 FootstepSoundSet 에셋을 연결한다. 비워두면 바닥과 상관없이 위의 기본 발소리만 난다.

    [SerializeField, LabelText("바닥 검사 레이어")]
    private LayerMask groundMask = ~0;
    // 발밑 바닥을 찾을 때 검사할 레이어다. 기본값은 전체다.

    [SerializeField, LabelText("바닥 검사 거리")]
    private float groundProbeDistance = 0.6f;
    // 발 위치에서 아래로 이 거리 안에 있는 바닥을 밟은 바닥으로 본다. 경사로에서 놓치면 조금 늘린다.

    [ReadOnly, ShowInInspector, LabelText("마지막으로 밟은 바닥")]
    private FootstepSurfaceType _currentSurface;

    [Title("재생 설정")]
    [SerializeField, LabelText("걷기 판정 최소 속도")]
    private float minMoveSpeed = 0.1f;
    // 수평 속도가 이 값보다 느리면 멈춘 것으로 보고 소리를 내지 않는다. PlayerAnimatorController의 Walk 임계값과 맞춘다.
    // Idle↔Run 전환이 섞이는 짧은 순간에 이벤트가 불려도, 실제로 멈춰 있으면 이 값으로 걸러진다.

    [SerializeField, LabelText("블라스트 중 무시 속도")]
    private float blastIgnoreSpeed = 0.5f;
    // 선풍기 반동 속도가 이 값보다 크면 날아가는 중이므로 발소리를 내지 않는다.

    [SerializeField, LabelText("착지 시 발소리"), ToggleLeft]
    private bool playOnLand = true;
    // 켜면 공중에서 땅에 닿는 순간 발소리를 한 번 낸다.

    [Title("음량 / 음높이 변화")]
    [SerializeField, LabelText("음량"), Range(0f, 1f)]
    private float volume = 0.6f;

    [SerializeField, LabelText("음량 흔들림"), Range(0f, 0.5f)]
    private float volumeVariation = 0.1f;
    // 매번 음량을 ±이 값만큼 무작위로 바꿔 기계적으로 들리지 않게 한다.

    [SerializeField, LabelText("음높이 범위"), MinMaxSlider(0.5f, 1.5f, true)]
    private Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    // 매번 이 범위 안에서 pitch를 골라 같은 클립도 조금씩 다르게 들리게 한다.

    private CharacterController _cc;
    private PlayerController _playerController;
    private bool _wasGrounded = true;
    private int _lastClipIndex = -1;
    private AudioClip[] _lastClips;
    // 직전에 쓴 클립 목록이다. 바닥이 바뀌어 목록이 달라지면 "직전 클립 피하기" 기록을 초기화한다.

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();

        if (footstepAudioSource == null)
            Debug.LogError("PlayerFootstep에 발소리 전용 AudioSource를 연결해야 합니다.");
    }

    private void Update()
    {
        if (_playerController == null)
            return;

        bool isGrounded = _playerController.IsGrounded;

        // 공중에서 막 착지한 순간에는 걷기 애니메이션 이벤트가 없으므로 여기서 착지음을 직접 낸다.
        if (isGrounded && !_wasGrounded && playOnLand)
            PlayFootstep();
        _wasGrounded = isGrounded;
    }

    // 걷기 애니메이션의 발 디딤 프레임에서 PlayerFootstepAnimationEvent를 거쳐 호출된다.
    // 애니메이션은 공중/블라스트/전환 중에도 잠깐 재생될 수 있으므로, 실제로 땅 위를 걷는 중일 때만 소리를 낸다.
    public void PlayFootstepFromAnimation()
    {
        if (_playerController == null || !_playerController.IsGrounded)
            return;

        if (!_playerController.enabled)
            return;
        // 컷신·포털 연출은 PlayerController를 꺼서 조작을 잠근다. 이때 _cc.velocity는 마지막 Move() 값이
        // 그대로 남아 있어서, 이 검사가 없으면 멈춰 있는데도 발소리가 계속 난다.

        if (_playerController.BlastRecoilSpeed > blastIgnoreSpeed)
            return;

        Vector3 horizontal = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        // 사이드뷰는 X, 탑다운은 XZ로 움직이므로 Y를 뺀 수평 속도만 본다.
        if (horizontal.magnitude < minMoveSpeed)
            return;

        PlayFootstep();
    }

    // 클립 목록에서 직전과 다른 클립을 골라 음량/음높이를 살짝 흔들어 재생한다.
    private void PlayFootstep()
    {
        _currentSurface = DetectSurface();
        PlayFootstep(_currentSurface);
    }

    // 바닥 종류에 맞는 클립 목록에서 하나를 골라 재생한다. 그 종류의 클립이 없으면 기본 발소리를 쓴다.
    private void PlayFootstep(FootstepSurfaceType surface)
    {
        AudioClip[] clips = soundSet != null ? soundSet.GetClips(surface) : null;
        if (clips == null) clips = footstepClips;

        if (footstepAudioSource == null || clips == null || clips.Length == 0)
            return;

        if (clips != _lastClips)
        {
            _lastClips = clips;
            _lastClipIndex = -1;
        }

        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == _lastClipIndex)
            index = (index + 1) % clips.Length;
        _lastClipIndex = index;

        AudioClip clip = clips[index];
        if (clip == null)
            return;

        footstepAudioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        // PlayOneShot은 클립별 pitch를 받지 않으므로, 재생 직전에 전용 AudioSource의 pitch를 바꿔 쓴다.
        float finalVolume = Mathf.Clamp01(volume + Random.Range(-volumeVariation, volumeVariation));
        footstepAudioSource.PlayOneShot(clip, finalVolume);
    }

    // 발밑 바닥을 짧게 레이캐스트해 바닥 종류를 정한다.
    // 순서: 영역 판정 바닥(눈 덮개 등) → FootstepSurface 직접 지정 → IcePlatform(얼음) → 재질(눈/풀 목록) → 기본.
    // 발소리를 낼 때만 한 번 검사하므로 매 프레임 비용이 들지 않는다.
    private FootstepSurfaceType DetectSurface()
    {
        // 콜라이더 없이 바닥 위에 얹힌 장식(눈 덮개)은 레이캐스트에 잡히지 않으므로 발 위치로 먼저 확인한다.
        if (FootstepSurface.TryGetAreaSurface(transform.position, out FootstepSurfaceType areaType))
            return areaType;

        // 발(피벗)보다 조금 위에서 쏴서, 경사로에 발이 살짝 묻혀 있어도 바닥을 놓치지 않게 한다.
        // 시작점이 플레이어 CharacterController 안이라 자기 자신은 맞지 않는다.
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.3f + groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
            return FootstepSurfaceType.Default;

        Collider ground = hit.collider;

        FootstepSurface marker = ground.GetComponentInParent<FootstepSurface>();
        if (marker != null) return marker.SurfaceType;

        // 횃불로 녹아 미끄럽지 않은 상태여도 얼음 위이므로 얼음 소리를 낸다.
        if (ground.GetComponentInParent<IcePlatform>() != null) return FootstepSurfaceType.Ice;

        if (soundSet == null) return FootstepSurfaceType.Default;

        // 콜라이더와 렌더러가 다른 오브젝트에 있는 모델(FBX 프리팹 등)도 있으므로 자신 → 부모 → 자식 순서로 찾는다.
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer == null) renderer = ground.GetComponentInParent<Renderer>();
        if (renderer == null) renderer = ground.GetComponentInChildren<Renderer>();
        if (renderer == null) return FootstepSurfaceType.Default;

        foreach (Material material in renderer.sharedMaterials)
        {
            FootstepSurfaceType type = soundSet.GetTypeFromMaterial(material);
            if (type != FootstepSurfaceType.Default) return type;
        }
        return FootstepSurfaceType.Default;
    }

    [Button("발소리 테스트")]
    private void TestFootstep()
    {
        PlayFootstep();
    }

    [Button("바닥별 발소리 미리듣기 (Play Mode)")]
    private void TestSurfaceFootstep(FootstepSurfaceType surface)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[PlayerFootstep] Play Mode에서만 테스트할 수 있습니다."); return; }
        PlayFootstep(surface);
    }
}
