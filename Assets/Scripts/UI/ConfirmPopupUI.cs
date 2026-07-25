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
        gameObject.SetActive(false);

        yesButton.onClick.AddListener(HandleYesClicked);
        noButton.onClick.AddListener(HandleNoClicked);
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

        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;

        _popTween?.Kill();
        _popTween = transform.DOScale(Vector3.one, popInDuration).SetEase(popInEase);
    }

    private void HandleYesClicked()
    {
        gameObject.SetActive(false);
        _pendingConfirmAction?.Invoke();
        _pendingConfirmAction = null;
    }

    private void HandleNoClicked()
    {
        gameObject.SetActive(false);
        _pendingConfirmAction = null;
    }
}
