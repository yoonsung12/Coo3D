using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 제단 복구 트리거다. 구슬 조각을 모두 모은 상태로 제단에 다가가면 복구 연출을 재생하고,
// 부족하면 화면에 안내 문구를 띄운다.
// 2D 원본은 Sprite Mask로 원이 커지며 회색 막을 걷어내는 연출이었지만, 3D에는 대응하는
// 렌더링 기능이 없어 펀치 스케일 + 파티클로 대체했다(사용자 승인됨).
[RequireComponent(typeof(Collider))]
public class AltarRestoreEffect : MonoBehaviour
{
    [Title("제단 오브젝트")]
    [SerializeField, LabelText("복구 전 제단")]
    private GameObject altarBefore;

    [SerializeField, LabelText("복구 후 제단")]
    private GameObject altarAfter;
    // 평소엔 altarBefore만 보이고, 복구되는 순간 altarAfter로 교체해 표시한다.

    [Title("복구 연출 설정")]
    [SerializeField, LabelText("등장 시간")]
    private float revealDuration = 0.5f;
    // altarAfter가 크기 0에서 원래 크기로 커지는 데 걸리는 시간이다.

    [SerializeField, LabelText("등장 Ease")]
    private Ease revealEase = Ease.OutBack;

    [SerializeField, LabelText("복구 파티클")]
    private ParticleSystem revealParticle;
    // Inspector에서 복구되는 순간 터질 파티클을 연결한다. 비워두면 파티클 없이 동작한다.

    [Title("조각 부족 안내")]
    [SerializeField, LabelText("안내 텍스트")]
    private Text noticeText;

    [SerializeField, LabelText("안내 페이드인 시간")]
    private float noticeFadeIn = 0.3f;

    [SerializeField, LabelText("안내 유지 시간")]
    private float noticeHold = 1.5f;

    [SerializeField, LabelText("안내 페이드아웃 시간")]
    private float noticeFadeOut = 0.4f;

    [Title("런타임 상태")]
    [ShowInInspector, ReadOnly, LabelText("복구 완료 여부")]
    private bool _restored;

    private Tween _noticeTween;
    private Tween _revealTween;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (altarBefore != null) altarBefore.SetActive(true);
        if (altarAfter != null) altarAfter.SetActive(false);

        if (noticeText != null)
        {
            Color c = noticeText.color;
            noticeText.color = new Color(c.r, c.g, c.b, 0f);
        }
    }

    private void OnDestroy()
    {
        _noticeTween?.Kill();
        _revealTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_restored) return;
        if (!other.CompareTag("Player")) return;

        if (MarbleInventory.Instance == null || !MarbleInventory.Instance.IsMarbleComplete)
        {
            var (collected, required) = MarbleInventory.Instance != null
                ? MarbleInventory.Instance.GetFragmentStatus()
                : (0, 3);
            ShowNotice($"구슬 조각이 부족합니다. ({collected}/{required})");
            return;
        }

        _restored = true;
        PlayRestoreEffect();
    }

    // 제단을 복구 후 모습으로 교체하고 등장 연출을 재생한다.
    private void PlayRestoreEffect()
    {
        if (altarBefore != null)
            altarBefore.SetActive(false);

        if (altarAfter != null)
        {
            altarAfter.SetActive(true);
            altarAfter.transform.localScale = Vector3.zero;

            _revealTween?.Kill();
            _revealTween = altarAfter.transform
                .DOScale(Vector3.one, revealDuration)
                .SetEase(revealEase);
        }

        if (revealParticle != null)
            revealParticle.Play();
    }

    // 조각이 부족할 때 화면에 안내 문구를 잠깐 띄운다.
    private void ShowNotice(string message)
    {
        if (noticeText == null) return;

        noticeText.text = message;

        _noticeTween?.Kill();
        Color c = noticeText.color;
        noticeText.color = new Color(c.r, c.g, c.b, 0f);

        _noticeTween = DOTween.Sequence()
            .Append(noticeText.DOFade(1f, noticeFadeIn))
            .AppendInterval(noticeHold)
            .Append(noticeText.DOFade(0f, noticeFadeOut));
    }

    [Button("복구 테스트 (Play Mode)")]
    private void TestRestore()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[AltarRestoreEffect] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        _restored = false;
        PlayRestoreEffect();
    }

    [Button("부족 안내 테스트 (Play Mode)")]
    private void TestNotice()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[AltarRestoreEffect] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        ShowNotice("구슬 조각이 부족합니다. (0/3)");
    }
}
