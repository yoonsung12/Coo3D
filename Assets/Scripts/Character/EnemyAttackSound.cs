using Sirenix.OdinInspector;
using UnityEngine;

// 적 공격 사운드 전용 컴포넌트다.
// NFBTEnemyAI가 공격 시점마다 PlayXxx()를 호출한다.
// 이 컴포넌트가 없는 적은 NFBTEnemyAI가 attackSound?. 로 호출하므로 소리 없이 그대로 동작한다.
public class EnemyAttackSound : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;
    // 소리를 실제로 내보낼 AudioSource다. 비워 두면 Awake에서 같은 오브젝트에서 찾아온다.

    [Title("클립")]
    [SerializeField, LabelText("근접 공격")]
    private AudioClip meleeClip;
    // 지상 근접 공격(쪼기)을 시작할 때 나는 소리다.

    [SerializeField, LabelText("돌진")]
    private AudioClip rushClip;
    // 도약/카운터 돌진을 시작할 때 나는 소리다.

    [SerializeField, LabelText("원거리 발사")]
    private AudioClip rangedFireClip;
    // 원거리 투사체를 발사하는 순간의 소리다.

    [SerializeField, LabelText("백스텝 점프")]
    private AudioClip backstepJumpClip;
    // 원거리 공격 전에 뒤로 뛰어오를 때의 소리다.

    [SerializeField, LabelText("백스텝 착지")]
    private AudioClip backstepLandClip;
    // 백스텝 후 바닥에 내려앉아 조준을 시작할 때의 소리다.

    [Title("재생 설정")]
    [SerializeField, LabelText("볼륨"), Range(0f, 1f)]
    private float volume = 1f;
    // 값이 클수록 소리가 커진다. 거리 감쇠는 AudioSource 설정이 따로 처리한다.

    [SerializeField, LabelText("최소 피치")]
    private float minPitch = 0.95f;

    [SerializeField, LabelText("최대 피치")]
    private float maxPitch = 1.05f;
    // 같은 소리가 반복돼도 덜 질리도록 매번 이 범위에서 음높이를 살짝 바꾼다.

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void PlayMelee() { PlayClip(meleeClip); }
    // 근접 공격 소리를 재생한다.

    public void PlayRush() { PlayClip(rushClip); }
    // 돌진 소리를 재생한다.

    public void PlayRangedFire() { PlayClip(rangedFireClip); }
    // 원거리 발사 소리를 재생한다.

    public void PlayBackstepJump() { PlayClip(backstepJumpClip); }
    // 백스텝 점프 소리를 재생한다.

    public void PlayBackstepLand() { PlayClip(backstepLandClip); }
    // 백스텝 착지 소리를 재생한다.

    private void PlayClip(AudioClip clip)
    {
        // 클립이나 소스가 비어 있어도 게임이 멈추지 않도록 조용히 넘어간다.
        if (clip == null || audioSource == null) return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        // PlayOneShot은 이전 소리를 끊지 않고 겹쳐 재생해서, 빠르게 연속 호출돼도 자연스럽다.
        audioSource.PlayOneShot(clip, volume);
    }

    [Button("근접 소리 테스트")]
    private void TestMelee()
    {
        if (!Application.isPlaying) return;
        PlayMelee();
    }

    [Button("돌진 소리 테스트")]
    private void TestRush()
    {
        if (!Application.isPlaying) return;
        PlayRush();
    }

    [Button("원거리 발사 소리 테스트")]
    private void TestRangedFire()
    {
        if (!Application.isPlaying) return;
        PlayRangedFire();
    }

    [Button("백스텝 점프 소리 테스트")]
    private void TestBackstepJump()
    {
        if (!Application.isPlaying) return;
        PlayBackstepJump();
    }

    [Button("백스텝 착지 소리 테스트")]
    private void TestBackstepLand()
    {
        if (!Application.isPlaying) return;
        PlayBackstepLand();
    }
}
