using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 화면 한쪽에 보유 중인 깃털 수를 표시한다.
// PillowDurability.OnFeatherCountChanged 이벤트를 구독해 깃털을 줍거나 쓸 때마다 갱신한다.
// 2D 원본은 플레이어 머리 위 월드스페이스 텍스트였지만, 이 프로젝트의 다른 카운터(코인/점수)와
// 동일하게 화면 코너 UI로 배치한다.
public class FeatherCounterUI : MonoBehaviour
{
    [Title("참조")]
    [SerializeField, LabelText("깃털 수 텍스트")]
    private Text counterText;

    [Title("표시 설정")]
    [SerializeField, LabelText("텍스트 앞에 붙일 접두사")]
    private string prefix = "x";
    // 숫자 앞에 표시할 문자. 예: "x" → "x3"

    [Title("팝 애니메이션 설정")]
    [SerializeField, LabelText("팝 지속 시간")]
    private float popDuration = 0.25f;

    [SerializeField, LabelText("팝 강도")]
    private float popStrength = 0.4f;

    private Vector3 _originalScale;
    private Tween _popTween;
    private bool _subscribed;

    private void Awake()
    {
        if (counterText != null)
            _originalScale = counterText.transform.localScale;
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (!_subscribed || PillowDurability.Instance == null) return;

        PillowDurability.Instance.OnFeatherCountChanged -= OnFeatherCountChanged;
        _subscribed = false;
    }

    private void OnDestroy()
    {
        _popTween?.Kill();
    }

    private void TrySubscribe()
    {
        if (_subscribed || PillowDurability.Instance == null) return;

        PillowDurability.Instance.OnFeatherCountChanged += OnFeatherCountChanged;
        _subscribed = true;

        OnFeatherCountChanged(PillowDurability.Instance.HeldFeatherCount);
    }

    private void OnFeatherCountChanged(int count)
    {
        if (counterText == null) return;

        counterText.text = prefix + count;

        if (count > 0)
            PlayPopAnimation();
    }

    private void PlayPopAnimation()
    {
        if (counterText == null) return;

        _popTween?.Kill();
        counterText.transform.localScale = _originalScale;
        _popTween = counterText.transform.DOPunchScale(Vector3.one * popStrength, popDuration, vibrato: 1, elasticity: 0.5f);
    }

    [Button("카운트 +1 테스트")]
    private void TestIncrease()
    {
        if (!Application.isPlaying) return;
        PillowDurability.Instance?.CollectFeather();
    }
}
