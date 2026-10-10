using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 연결된 FlammableObject(덩굴 아치 등)가 다 타서 사라지면, 거기에 묶여 세워져 있던 다리가
// 힌지를 축으로 쓰러지며 낭떠러지를 잇는 다리가 되는 퍼즐 장치다.
// 다리는 물리로 넘어뜨리지 않고 DOTween 회전 연출로 쓰러뜨린다 — 쓰러지는 동안 플레이어는
// 낭떠러지 건너편에 있으므로 충돌이 꼬일 일이 없고, 다 쓰러진 뒤에는 그냥 고정된 바닥이 된다.
public class Drawbridge : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("묶여 있는 대상(타면 풀림)")]
    private FlammableObject bindingObject;
    // 다리를 붙잡고 있는 덩굴 아치를 연결한다. 이 오브젝트의 OnBurnedOut 이벤트가 오면 다리가 쓰러진다.

    [SerializeField, LabelText("다리 힌지")]
    private Transform bridgeHinge;
    // 다리 판의 부모 Transform이다. 이 오브젝트의 위치가 회전 축(다리 밑동)이 되도록 배치한다.

    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // 다리가 땅에 닿을 때 카메라를 흔들기 위해 연결한다. 비워두면 흔들림을 생략한다.

    [Title("쓰러짐 연출 (DOTween)")]
    [SerializeField, LabelText("쓰러진 회전값(로컬)")]
    private Vector3 loweredEuler = new Vector3(0f, 0f, 90f);
    // 다리가 다 쓰러졌을 때 힌지의 로컬 회전값이다. 사이드뷰에서 서쪽(-X)으로 눕히려면 Z +90을 쓴다.

    [SerializeField, LabelText("풀린 뒤 대기 시간")]
    private float fallDelay = 0.3f;
    // 아치가 다 탄 직후 바로 쓰러지지 않고 잠깐 기다리는 시간이다. 클수록 "버티다 넘어가는" 느낌이 난다.

    [SerializeField, LabelText("쓰러지는 시간")]
    private float fallDuration = 1.2f;
    // 세워진 상태에서 완전히 누울 때까지 걸리는 시간이다.

    [SerializeField, LabelText("쓰러지는 Ease")]
    private Ease fallEase = Ease.OutBounce;
    // OutBounce는 땅에 닿은 뒤 살짝 튀었다 멈추는 느낌을 준다.

    [SerializeField, LabelText("착지 흔들림 시간")]
    private float shakeDuration = 0.3f;

    [SerializeField, LabelText("착지 흔들림 세기")]
    private float shakeStrength = 0.3f;

    [Title("사운드")]
    [SerializeField, LabelText("쓰러지는 소리")]
    private AudioClip fallSound;
    // 다리가 넘어가기 시작할 때 한 번 재생한다. 비워두면 소리 없이 쓰러진다.

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("내려감")]
    private bool _isLowered;

    private Sequence _fallSequence;
    // 쓰러짐 연출(대기 → 회전 → 흔들림)을 묶은 Sequence다. 파괴 시 정리하기 위해 저장한다.

    private void Start()
    {
        if (bindingObject != null)
            bindingObject.OnBurnedOut += Lower;
    }

    private void OnDestroy()
    {
        if (bindingObject != null)
            bindingObject.OnBurnedOut -= Lower;

        _fallSequence?.Kill();
    }

    // 묶은 대상이 다 타면 호출된다. 한 번 내려간 다리는 다시 세우지 않는다.
    public void Lower()
    {
        if (_isLowered || bridgeHinge == null) return;
        _isLowered = true;

        _fallSequence?.Kill();
        _fallSequence = DOTween.Sequence()
            .AppendInterval(fallDelay)
            .AppendCallback(PlayFallSound)
            .Append(bridgeHinge.DOLocalRotate(loweredEuler, fallDuration).SetEase(fallEase))
            .AppendCallback(ShakeCamera)
            .SetLink(gameObject);
    }

    private void PlayFallSound()
    {
        if (audioSource != null && fallSound != null)
            audioSource.PlayOneShot(fallSound);
    }

    private void ShakeCamera()
    {
        if (sideViewCamera != null)
            sideViewCamera.Shake(shakeDuration, shakeStrength);
    }

    [Button("다리 내리기 테스트 (Play Mode)")]
    private void TestLower()
    {
        if (Application.isPlaying)
            Lower();
    }
}
