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
        if (footstepAudioSource == null || footstepClips == null || footstepClips.Length == 0)
            return;

        int index = Random.Range(0, footstepClips.Length);
        if (footstepClips.Length > 1 && index == _lastClipIndex)
            index = (index + 1) % footstepClips.Length;
        _lastClipIndex = index;

        AudioClip clip = footstepClips[index];
        if (clip == null)
            return;

        footstepAudioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        // PlayOneShot은 클립별 pitch를 받지 않으므로, 재생 직전에 전용 AudioSource의 pitch를 바꿔 쓴다.
        float finalVolume = Mathf.Clamp01(volume + Random.Range(-volumeVariation, volumeVariation));
        footstepAudioSource.PlayOneShot(clip, finalVolume);
    }

    [Button("발소리 테스트")]
    private void TestFootstep()
    {
        PlayFootstep();
    }
}
