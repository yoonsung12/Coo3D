using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 횃불 점화용 부싯돌 타이밍(QTE) 미니게임 UI다.
// TorchTool이 Begin()으로 시작시키고, 플레이어가 사용 버튼을 다시 누른 순간 TryJudge()를 호출해
// 그 시점의 인디케이터 위치가 성공 구간 안인지 판정한다.
public class TorchIgniteMinigameUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("패널 (CanvasGroup)")]
    private CanvasGroup panel;
    // 이 패널은 클릭을 받지 않는다. 판정은 TorchTool로 들어오는 Attack 입력으로만 이루어진다.

    [SerializeField, LabelText("게이지 바")]
    private RectTransform gaugeBar;
    // 인디케이터가 이 오브젝트의 가로 폭 안에서 왕복한다.

    [SerializeField, LabelText("인디케이터")]
    private RectTransform indicator;
    // 게이지 바의 자식이어야 하며, anchoredPosition.x로 왕복 위치를 표현한다.

    [SerializeField, LabelText("성공 구간")]
    private RectTransform successZone;
    // 게이지 바의 자식으로, anchorMin.x ~ anchorMax.x(0~1) 범위를 성공 구간으로 판정한다.

    [Title("타이밍 설정")]
    [SerializeField, LabelText("왕복 시간(초)")]
    private float sweepDuration = 0.6f;
    // 인디케이터가 게이지 한쪽 끝에서 반대쪽 끝까지 이동하는 데 걸리는 시간이다.
    // 값이 작을수록 더 빠르게 움직여서 타이밍을 맞추기 어려워진다.

    [Title("연출 설정")]
    [SerializeField, LabelText("등장/사라짐 시간")]
    private float popDuration = 0.15f;

    [SerializeField, LabelText("성공 색상")]
    private Color successColor = Color.green;

    [SerializeField, LabelText("실패 색상")]
    private Color failColor = Color.red;

    [SerializeField, LabelText("판정 피드백 유지 시간")]
    private float feedbackHoldDuration = 0.15f;
    // 판정 직후 성공/실패 색으로 잠깐 표시했다가 사라지는데, 이 시간만큼 색을 유지한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("진행 중 여부")]
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    private Image _indicatorImage;
    private Tween _sweepTween;
    private Tween _popTween;
    private Tween _flashTween;

    private void Awake()
    {
        _indicatorImage = indicator != null ? indicator.GetComponent<Image>() : null;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        _sweepTween?.Kill();
        _popTween?.Kill();
        _flashTween?.Kill();
    }

    // 미니게임을 시작한다. 패널을 띄우고 인디케이터가 게이지 좌우로 왕복하기 시작한다.
    public void Begin()
    {
        _isRunning = true;
        SetVisible(true);

        transform.localScale = Vector3.zero;
        _popTween?.Kill();
        _popTween = transform.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack).SetUpdate(true);

        if (_indicatorImage != null)
            _indicatorImage.color = Color.white;

        float halfWidth = gaugeBar.rect.width * 0.5f;
        indicator.anchoredPosition = new Vector2(-halfWidth, indicator.anchoredPosition.y);

        _sweepTween?.Kill();
        _sweepTween = indicator
            .DOAnchorPosX(halfWidth, sweepDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
        // SetUpdate(true)로 Time.timeScale과 무관하게 항상 재생된다.
    }

    // 현재 인디케이터 위치가 성공 구간 안인지 판정하고 미니게임을 종료한다.
    // 미니게임이 진행 중이 아니면 아무 일도 하지 않고 false를 반환한다.
    public bool TryJudge()
    {
        if (!_isRunning) return false;

        float normalizedPos = GetIndicatorNormalizedPosition();
        bool success = normalizedPos >= successZone.anchorMin.x && normalizedPos <= successZone.anchorMax.x;

        _isRunning = false;
        _sweepTween?.Kill();

        ShowResultFeedback(success);
        Hide(feedbackHoldDuration);

        return success;
    }

    // 도구 해제 등으로 미니게임을 결과 판정 없이 강제 종료할 때 호출한다.
    public void Cancel()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _sweepTween?.Kill();
        Hide(0f);
    }

    // 인디케이터의 현재 좌우 위치를 게이지 바 기준 0~1 값으로 변환한다.
    private float GetIndicatorNormalizedPosition()
    {
        float halfWidth = gaugeBar.rect.width * 0.5f;
        if (halfWidth <= 0f) return 0.5f;
        return Mathf.InverseLerp(-halfWidth, halfWidth, indicator.anchoredPosition.x);
    }

    // 판정 결과에 맞춰 인디케이터 색을 잠깐 바꾼다.
    private void ShowResultFeedback(bool success)
    {
        if (_indicatorImage == null) return;

        _flashTween?.Kill();
        _indicatorImage.color = success ? successColor : failColor;
        _flashTween = _indicatorImage.DOColor(Color.white, popDuration).SetUpdate(true);
    }

    private void Hide(float delay)
    {
        _popTween?.Kill();
        _popTween = transform.DOScale(Vector3.zero, popDuration)
            .SetDelay(delay)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() => SetVisible(false));
    }

    private void SetVisible(bool visible)
    {
        panel.alpha = visible ? 1f : 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
    }

    [Button("QTE 시작 테스트")]
    private void TestBegin() => Begin();

    [Button("QTE 판정 테스트")]
    private void TestJudge() => Debug.Log($"[TorchIgniteMinigameUI] 판정 결과: {TryJudge()}");
}
