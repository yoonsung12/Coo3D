using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 덮어쓰기/삭제처럼 되돌리기 어려운 동작을 실행하기 전에 예/아니오로 한 번 더 확인받는 공용 팝업이다.
// SaveSlotSelectController가 상황에 맞는 메시지와 확인 시 실행할 동작을 넘겨서 재사용한다.
public class ConfirmPopupUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("패널 (CanvasGroup)")]
    private CanvasGroup panel;
    // 예/아니오 버튼 클릭 시 이 오브젝트 자체를 SetActive(false)로 끄면, 지금 클릭된 버튼의 부모를
    // 같은 프레임에 비활성화하는 셈이라 클릭 처리가 꼬일 수 있다(SaveSlotSelectController.Open()에서
    // 겪은 것과 같은 문제). 그래서 오브젝트는 항상 켜둔 채 CanvasGroup으로만 보이기/막기를 제어한다.

    [SerializeField, LabelText("메시지 텍스트")]
    private Text messageText;

    [SerializeField, LabelText("확인 버튼")]
    private Button yesButton;

    [SerializeField, LabelText("취소 버튼")]
    private Button noButton;

    [Title("등장 연출 설정")]
    [SerializeField, LabelText("등장 시간")]
    private float popInDuration = 0.2f;

    [SerializeField, LabelText("등장 Ease")]
    private Ease popInEase = Ease.OutBack;

    private Tween _popTween;
    private Action _pendingConfirmAction;
    // Show()를 호출할 때마다 새로 채워지며, 확인 버튼을 눌렀을 때 실행할 동작을 잠시 기억해 둔다.

    private void Awake()
    {
        yesButton.onClick.AddListener(HandleYesClicked);
        noButton.onClick.AddListener(HandleNoClicked);

        SetVisible(false);
    }

    private void OnDestroy()
    {
        _popTween?.Kill();
    }

    // message를 표시하며 팝업을 연다. 확인 버튼을 누르면 onConfirm이 실행된다.
    public void Show(string message, Action onConfirm)
    {
        messageText.text = message;
        _pendingConfirmAction = onConfirm;

        SetVisible(true);
        transform.localScale = Vector3.zero;

        _popTween?.Kill();
        _popTween = transform.DOScale(Vector3.one, popInDuration).SetEase(popInEase);
    }

    private void HandleYesClicked()
    {
        SetVisible(false);
        _pendingConfirmAction?.Invoke();
        _pendingConfirmAction = null;
    }

    private void HandleNoClicked()
    {
        SetVisible(false);
        _pendingConfirmAction = null;
    }

    private void SetVisible(bool visible)
    {
        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }
}
