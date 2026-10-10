using System;
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

    [SerializeField, LabelText("검 공격")]
    private SwordAttack swordAttack;
    // Inspector에서 플레이어의 SwordAttack 컴포넌트를 연결한다.

    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다.

    [Title("연결")]
    [SerializeField, LabelText("웨폰 휠 UI")]
    private WeaponWheelUI weaponWheelUI;
    // Inspector에서 GameCanvas의 WeaponWheelUI 컴포넌트를 연결한다.

    [Title("도구 해금")]
    [SerializeField, LabelText("시작할 때 횃불 해금")]
    private bool torchUnlockedAtStart = true;
    // 봄·여름·가을 씬은 꺼 둔다(횃불은 가을 퍼즐②에서 얻는다). 겨울 이후 씬은 켜 둔다.
    // 꺼져 있어도 세이브에 "횃불 획득" 기록이 있으면 해금된 상태로 시작한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("횃불 해금됨")]
    public bool IsTorchUnlocked => _torchUnlocked;

    public event Action OnUnlockStateChanged;
    // 해금 상태가 바뀌면 알린다. WeaponWheelUI가 잠긴 슬롯 색을 다시 칠하는 데 사용한다.

    private bool _torchUnlocked;

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

    // SaveManager는 씬에 따라 이 컴포넌트보다 늦게 Awake될 수 있어서, 세이브 기록은 Start에서 읽는다.
    private void Start()
    {
        bool savedUnlock = SaveManager.Instance != null && SaveManager.Instance.TorchUnlocked;
        _torchUnlocked = torchUnlockedAtStart || savedUnlock;

        if (SaveManager.Instance != null)
            SaveManager.Instance.TorchUnlockLoaded += HandleTorchUnlockLoaded;
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.TorchUnlockLoaded -= HandleTorchUnlockLoaded;
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
                // 아직 얻지 못한 횃불은 장착하지 않는다. 휠에서도 회색으로 막혀 있지만 한 번 더 확인한다.
                if (_torchUnlocked)
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
            // 공격 모드: SW08 교번 대각선 베기를 실행한다.
            swordAttack?.TryAttack();
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

    // 웨폰 휠이 슬롯을 강조/선택할 수 있는지 묻는다. 지금 잠글 수 있는 도구는 횃불뿐이다.
    public bool IsSlotUnlocked(WeaponSlot slot) => slot != WeaponSlot.Torch || _torchUnlocked;

    // 횃불을 얻었을 때 호출한다(TorchUnlockItem). 세이브 매니저에도 기록해 두어
    // 다음 체크포인트 저장 때 함께 저장되고, 씬을 넘어가도 유지되게 한다.
    public void UnlockTorch()
    {
        if (_torchUnlocked) return;

        _torchUnlocked = true;
        if (SaveManager.Instance != null)
            SaveManager.Instance.SetTorchUnlocked(true);

        OnUnlockStateChanged?.Invoke();
    }

    // 세이브를 불러왔을 때(같은 씬에서 이어하기 포함) 저장된 해금 기록으로 상태를 다시 맞춘다.
    private void HandleTorchUnlockLoaded(bool savedUnlock)
    {
        SetTorchUnlockedInternal(torchUnlockedAtStart || savedUnlock);
    }

    private void SetTorchUnlockedInternal(bool unlocked)
    {
        _torchUnlocked = unlocked;

        // 잠기는데 횃불을 들고 있었다면 내려놓게 한다.
        if (!_torchUnlocked && _activeTool == torchTool)
            UnequipCurrent();

        OnUnlockStateChanged?.Invoke();
    }

    [Button("횃불 해금 테스트")]
    private void TestUnlockTorch() => UnlockTorch();

    [Button("횃불 잠금 테스트")]
    private void TestLockTorch()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.SetTorchUnlocked(false);
        SetTorchUnlockedInternal(false);
    }
}
