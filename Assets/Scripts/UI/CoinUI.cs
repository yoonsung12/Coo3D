using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// CoinManager의 보유 코인 수를 아이콘 + 숫자로 표시한다.
// 코인이 늘거나 줄 때마다 숫자 텍스트에 펀치 스케일 연출을 재생한다.
public class CoinUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("코인 아이콘")]
    private Image coinIcon;
    // Inspector에서 Assets/Coin.png 스프라이트를 연결한다.

    [SerializeField, LabelText("코인 수 텍스트")]
    private Text countText;

    [Title("펀치 연출 설정")]
    [SerializeField, LabelText("펀치 스케일 강도")]
    private float punchScale = 0.3f;

    [SerializeField, LabelText("펀치 지속 시간")]
    private float punchDuration = 0.25f;

    [SerializeField, LabelText("아이콘 펀치 스케일 강도")]
    private float iconPunchScale = 0.2f;
    // 숫자보다 아이콘은 살짝 더 작게 튀도록 별도 값으로 조절한다.

    private Tween _punchTween;
    private Tween _iconPunchTween;

    private void OnEnable()
    {
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.OnCoinChanged += HandleCoinChanged;
            // 코인 UI가 활성화된 시점의 코인 수를 바로 반영한다.
            HandleCoinChanged(CoinManager.Instance.CoinCount);
        }
    }

    private void OnDisable()
    {
        if (CoinManager.Instance != null)
            CoinManager.Instance.OnCoinChanged -= HandleCoinChanged;
    }

    private void OnDestroy()
    {
        _punchTween?.Kill();
        _iconPunchTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    // 코인 수가 바뀔 때마다 CoinManager가 호출한다.
    private void HandleCoinChanged(int coinCount)
    {
        if (countText != null)
            countText.text = coinCount.ToString();

        PlayPunchEffect();
    }

    private void PlayPunchEffect()
    {
        if (countText != null)
        {
            _punchTween?.Kill();
            countText.transform.localScale = Vector3.one;
            _punchTween = countText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 8, elasticity: 0.6f);
        }

        if (coinIcon != null)
        {
            // 숫자와 함께 아이콘도 살짝 통통 튀어서 코인이 들어온 느낌을 강조한다.
            _iconPunchTween?.Kill();
            coinIcon.transform.localScale = Vector3.one;
            _iconPunchTween = coinIcon.transform.DOPunchScale(Vector3.one * iconPunchScale, punchDuration, vibrato: 8, elasticity: 0.6f);
        }
    }
}
