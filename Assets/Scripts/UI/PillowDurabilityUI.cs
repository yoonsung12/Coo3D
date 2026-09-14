using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 화면 한쪽에 배게 내구도를 숫자로 표시한다.
// PillowDurability.OnDurabilityChanged 이벤트를 구독해 실시간으로 갱신한다.
public class PillowDurabilityUI : MonoBehaviour
{
    [Title("참조")]
    [SerializeField, LabelText("내구도 텍스트")]
    private Text durabilityText;

    [Title("색상 설정")]
    [SerializeField, LabelText("정상 색상")]
    private Color normalColor = Color.white;

    [SerializeField, LabelText("경고 색상 (50% 이하)")]
    private Color warningColor = new Color(1f, 0.6f, 0f);

    [SerializeField, LabelText("소진 색상")]
    private Color depletedColor = new Color(1f, 0.2f, 0.2f);

    [Title("연출 설정")]
    [SerializeField, LabelText("소진 시 흔들림 강도")]
    private float shakeStrength = 5f;

    [SerializeField, LabelText("소진 시 흔들림 시간")]
    private float shakeDuration = 0.4f;

    private Tween _shakeTween;
    private int _cachedMax;
    private bool _subscribed;
    // ScoreUI에서 겪은 것과 동일한 초기화 순서 문제(하이어라키상 매니저보다 UI가 먼저 있어
    // OnEnable 시점에 Instance가 아직 null일 수 있음)를 방지하기 위한 플래그다.

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

        PillowDurability.Instance.OnDurabilityChanged -= OnDurabilityChanged;
        _subscribed = false;
    }

    private void OnDestroy()
    {
        _shakeTween?.Kill();
    }

    private void TrySubscribe()
    {
        if (_subscribed || PillowDurability.Instance == null) return;

        PillowDurability.Instance.OnDurabilityChanged += OnDurabilityChanged;
        _subscribed = true;

        OnDurabilityChanged(
            PillowDurability.Instance.CurrentDurability,
            PillowDurability.Instance.MaxDurability,
            PillowDurability.Instance.IsDepleted
        );
    }

    private void OnDurabilityChanged(int current, int max, bool isDepleted)
    {
        if (durabilityText == null) return;

        _cachedMax = max;
        durabilityText.text = current + " / " + max;

        UpdateColor(current, max, isDepleted);

        if (isDepleted)
            PlayShakeEffect();
    }

    private void UpdateColor(int current, int max, bool isDepleted)
    {
        if (isDepleted)
        {
            durabilityText.color = depletedColor;
            return;
        }

        float ratio = max > 0 ? (float)current / max : 0f;
        durabilityText.color = ratio <= 0.5f ? warningColor : normalColor;
    }

    private void PlayShakeEffect()
    {
        _shakeTween?.Kill();
        _shakeTween = durabilityText.transform
            .DOShakePosition(shakeDuration, strength: shakeStrength, vibrato: 20, randomness: 90)
            .SetRelative(true);
    }

    [Button("내구도 소진 테스트")]
    private void TestDepleted()
    {
        if (!Application.isPlaying) return;
        OnDurabilityChanged(0, _cachedMax > 0 ? _cachedMax : 20, true);
    }

    [Button("내구도 절반 테스트")]
    private void TestHalf()
    {
        if (!Application.isPlaying) return;
        int max = _cachedMax > 0 ? _cachedMax : 20;
        OnDurabilityChanged(max / 2, max, false);
    }
}
