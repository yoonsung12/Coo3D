using Sirenix.OdinInspector;
using UnityEngine;

// 발판(PressButton)을 밟으면 연결된 환풍기 출구(VentOut)에서 바람이 나오게 한다.
// VentIn(선풍기 바람 감지)과는 별개의 경로이며, 이 스위치로 제어하는 VentOut은
// VentIn에 연결하지 않는 것을 전제로 한다 (같은 VentOut을 두 소스가 동시에 제어하면
// 서로의 Activate/Deactivate 호출이 덮어써 꼬일 수 있다).
public class VentSwitchController : MonoBehaviour
{
    [Title("스위치 연결")]
    [InfoBox("발판을 밟는 동안만 바람이 나오고, 벗어나면 바로 꺼집니다.")]
    [SerializeField, LabelText("스위치 버튼")]
    private PressButton switchButton;
    // Inspector에서 이 환풍기를 켜고 끌 PressButton(발판)을 연결한다.

    [Title("연결 설정")]
    [SerializeField, LabelText("연결된 환풍기 출구 목록")]
    private VentOut[] connectedVentOuts;
    // Inspector에서 이 스위치로 켤 VentOut 컴포넌트를 모두 등록한다.

    private void Start()
    {
        if (switchButton != null)
            switchButton.OnStateChanged += OnSwitchStateChanged;
    }

    private void OnDestroy()
    {
        if (switchButton != null)
            switchButton.OnStateChanged -= OnSwitchStateChanged;
    }

    // 발판 상태가 바뀔 때마다 호출된다. Pressed(밟힘) 또는 Locked(영구 잠김) 상태면
    // 바람을 켜고, 다시 Active(밟히지 않음) 상태로 돌아가면 바람을 끈다.
    private void OnSwitchStateChanged(PressButton _)
    {
        bool shouldBlow = switchButton.State == PressButton.ButtonState.Pressed
                        || switchButton.State == PressButton.ButtonState.Locked;

        foreach (VentOut vent in connectedVentOuts)
        {
            if (vent == null) continue;

            if (shouldBlow)
                vent.Activate();
            else
                vent.Deactivate();
        }
    }
}
