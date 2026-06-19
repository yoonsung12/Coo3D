using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

// 연결된 모든 PressButton이 동시에 눌리면 문을 열고, 버튼들을 영구 잠근다.
// 문이 한 번 열리면 다시 닫히지 않는다.
public class DoorController : MonoBehaviour
{
    [Title("연결된 버튼")]
    [SerializeField, LabelText("버튼 목록")]
    private List<PressButton> buttons = new List<PressButton>();
    // Inspector에서 이 문을 여는 데 필요한 PressButton을 모두 연결한다.

    [Title("문 연출 설정")]
    [SerializeField, LabelText("열림 각도 (Y축)")]
    private float openAngle = 90f;
    // DoorHinge 오브젝트가 Y축으로 회전하는 각도다.

    [SerializeField, LabelText("열림 시간")]
    private float openDuration = 0.8f;

    [SerializeField, LabelText("열림 Ease")]
    private Ease openEase = Ease.OutQuad;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("문 열림 여부")]
    private bool _isOpen;

    private Vector3 _closedEuler;
    private Tween _doorTween;

    private void Start()
    {
        // 씬 시작 시 닫힌 상태의 회전값을 저장한다.
        _closedEuler = transform.eulerAngles;

        foreach (var btn in buttons)
        {
            if (btn != null)
                btn.OnStateChanged += OnButtonStateChanged;
        }
    }

    private void OnButtonStateChanged(PressButton _)
    {
        // 이미 열렸으면 더 이상 처리하지 않는다.
        if (_isOpen) return;

        // 연결된 모든 버튼이 Pressed 또는 Locked 상태인지 확인한다.
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            bool isActivated = btn.State == PressButton.ButtonState.Pressed
                            || btn.State == PressButton.ButtonState.Locked;
            if (!isActivated) return;
        }

        // 모든 버튼이 동시에 눌린 경우 문을 열고 버튼을 영구 잠근다.
        OpenDoor();
        LockAllButtons();
    }

    private void OpenDoor()
    {
        _isOpen = true;
        _doorTween?.Kill();
        // DoorHinge를 Y축으로 회전시켜 문이 열리는 연출을 한다.
        _doorTween = transform
            .DORotate(_closedEuler + new Vector3(0f, openAngle, 0f), openDuration)
            .SetEase(openEase);
    }

    private void LockAllButtons()
    {
        // 문이 열린 순간 모든 버튼을 영구 잠금 상태로 만든다.
        foreach (var btn in buttons)
        {
            if (btn != null)
                btn.Lock();
        }
    }

    private void OnDestroy()
    {
        _doorTween?.Kill();
    }

    [Button("문 열기 테스트")]
    private void TestOpen() => OpenDoor();
}
