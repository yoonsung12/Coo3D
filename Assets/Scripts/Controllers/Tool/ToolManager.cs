using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

// 플레이어의 입력을 받아 각 도구를 제어하는 관리자다.
// 현재는 선풍기(FanTool)만 처리한다.
public class ToolManager : MonoBehaviour
{
    [Title("도구 연결")]
    [SerializeField, LabelText("선풍기 도구")]
    private FanTool fanTool;
    // Inspector에서 플레이어 오브젝트의 FanTool 컴포넌트를 연결한다.

    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("바람 버튼 누름")]
    private bool _isFanHeld;

    private InputAction _attackAction;
    // 선풍기 발사 키 (기본: 마우스 좌클릭)

    private InputAction _sprintAction;
    // 충전 모드 키 (기본: 왼쪽 Shift). Attack과 함께 누르면 충전 시작

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

        // performed: 버튼이 눌렸을 때 한 번 발동
        _attackAction.performed += OnAttackPerformed;
        // canceled: 버튼이 떼어졌을 때 한 번 발동
        _attackAction.canceled += OnAttackCanceled;
    }

    private void OnDisable()
    {
        _attackAction.performed -= OnAttackPerformed;
        _attackAction.canceled -= OnAttackCanceled;

        _attackAction.Disable();
        _sprintAction.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        _isFanHeld = true;
    }

    private void OnAttackCanceled(InputAction.CallbackContext ctx)
    {
        _isFanHeld = false;
        // 버튼을 뗄 때 FanTool에 릴리스 신호를 보낸다 (블라스트 발동 여부 결정).
        fanTool?.OnBlowRelease();
    }

    private void Update()
    {
        HandleFan();
    }

    private void HandleFan()
    {
        if (!_isFanHeld) return;

        // Shift가 함께 눌려 있으면 충전 모드, 아니면 일반 바람 모드
        bool isCharging = _sprintAction.IsPressed();

        if (isCharging)
            fanTool?.OnChargeFrame();
        else
            fanTool?.OnBlowFrame();
    }
}
