using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

// 플레이어의 도구 전환과 사용을 관리한다.
// WeaponWheel(Tab키)로 도구를 선택하고, 마우스 좌클릭으로 사용한다.
// 도구가 없을 때 좌클릭은 공격으로 라우팅된다.
public class ToolManager : MonoBehaviour
{
    [Title("도구 연결")]
    [SerializeField, LabelText("선풍기")]
    private FanTool fanTool;
    // Inspector에서 플레이어의 FanTool 컴포넌트를 연결한다.

    [SerializeField, LabelText("우산")]
    private UmbrellaTool umbrellaTool;
    // Inspector에서 플레이어의 UmbrellaTool 컴포넌트를 연결한다.

    [SerializeField, LabelText("횃불")]
    private TorchTool torchTool;
    // Inspector에서 플레이어의 TorchTool 컴포넌트를 연결한다.

    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다.

    [Title("연결")]
    [SerializeField, LabelText("웨폰 휠 UI")]
    private WeaponWheelUI weaponWheelUI;
    // Inspector에서 GameCanvas의 WeaponWheelUI 컴포넌트를 연결한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 장착 도구")]
    private string _activeToolName => _activeTool != null ? _activeTool.GetType().Name : "없음 (공격 모드)";

    [ReadOnly, ShowInInspector, LabelText("사용 버튼 누름")]
    private bool _isAttackHeld;

    private BaseTool _activeTool;

    private InputAction _attackAction;
    // 도구 사용 또는 공격 키 (기본: 마우스 좌클릭)

    private InputAction _sprintAction;
    // 선풍기 충전 키 (기본: 왼쪽 Shift)

    private void Awake()
    {
        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
        _sprintAction = playerMap.FindAction("Sprint", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _attackAction.Enable();
        _sprintAction.Enable();

        _attackAction.performed += OnAttackPerformed;
        _attackAction.canceled  += OnAttackCanceled;
    }

    private void OnDisable()
    {
        _attackAction.performed -= OnAttackPerformed;
        _attackAction.canceled  -= OnAttackCanceled;

        _attackAction.Disable();
        _sprintAction.Disable();
    }

    private void Update()
    {
        HandleActiveTool();
    }

    // WeaponWheel에서 슬롯 선택 결과를 받아 도구를 전환한다.
    public void SelectByWheel(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.Fan:
                SelectTool(fanTool);
                break;
            case WeaponSlot.Umbrella:
                SelectTool(umbrellaTool);
                break;
            case WeaponSlot.Torch:
                SelectTool(torchTool);
                break;
            case WeaponSlot.Attack:
                // 공격 모드는 도구를 해제한 상태다.
                UnequipCurrent();
                break;
            case WeaponSlot.None:
                // 중심에서 뗐을 때: 현재 선택 유지, 변경 없음
                break;
        }
    }

    // 사용 버튼을 처음 눌렀을 때 호출된다.
    // 도구가 없으면 공격 모드, 도구가 있으면 도구 사용으로 라우팅한다.
    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        _isAttackHeld = true;

        if (_activeTool != null)
        {
            // 도구 장착 모드: 선풍기 외 도구는 누름 시 1회 발동한다.
            // 선풍기는 HandleActiveTool에서 매 프레임 처리한다.
            if (_activeTool is not FanTool)
                _activeTool.OnUsePerformed();
        }
        else
        {
            // 공격 모드: 추후 PlayerCombat.TryAttack() 연결 예정
        }
    }

    // 사용 버튼을 뗐을 때 호출된다.
    private void OnAttackCanceled(InputAction.CallbackContext ctx)
    {
        _isAttackHeld = false;

        if (_activeTool is FanTool fan)
            fan.OnBlowRelease();
        else
            _activeTool?.OnUseRelease();
    }

    // 누르는 동안 매 프레임 호출된다. 선풍기 바람 지속, 우산 글라이드 유지 등에 사용한다.
    private void HandleActiveTool()
    {
        if (!_isAttackHeld) return;

        if (_activeTool is FanTool fan)
        {
            // 선풍기만 Shift 충전 여부를 확인해 모드를 나눈다.
            if (_sprintAction.IsPressed())
                fan.OnChargeFrame();
            else
                fan.OnBlowFrame();
        }
        else
        {
            _activeTool?.OnUseFrame();
        }
    }

    // 도구를 전환한다. 같은 도구를 다시 선택하면 해제된다.
    private void SelectTool(BaseTool newTool)
    {
        if (_activeTool == newTool)
        {
            UnequipCurrent();
            return;
        }

        UnequipCurrent();

        _activeTool = newTool;
        _activeTool?.OnEquip();
        Debug.Log($"[ToolManager] 도구 장착: {_activeTool?.GetType().Name}");
    }

    // 현재 장착된 도구를 해제한다.
    private void UnequipCurrent()
    {
        if (_activeTool == null) return;

        // 사용 중이었다면 먼저 중단한다.
        if (_isAttackHeld)
        {
            if (_activeTool is FanTool fan)
                fan.OnBlowRelease();
            else
                _activeTool.OnUseRelease();

            _isAttackHeld = false;
        }

        _activeTool.StopUsing();
        _activeTool.OnUnequip();
        Debug.Log($"[ToolManager] 도구 해제: {_activeTool.GetType().Name}");
        _activeTool = null;
    }
}
