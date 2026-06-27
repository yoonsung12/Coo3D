using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// WeaponWheel의 4방향 슬롯을 정의한다.
public enum WeaponSlot
{
    None,       // 중심에서 뗐을 때 — 현재 상태 유지
    Attack,     // 북 (위) — 도구 없음 상태 (공격 모드)
    Fan,        // 서 (왼쪽) — 선풍기
    Umbrella,   // 남 (아래) — 우산
    Torch       // 동 (오른쪽) — 횃불
}

// Tab 키를 누르는 동안 시간을 정지하고 방사형 도구 선택 메뉴를 표시한다.
// 마우스를 원하는 방향으로 이동해 슬롯을 선택하고, Tab을 떼면 해당 도구로 전환된다.
public class WeaponWheelUI : MonoBehaviour
{
    [Title("슬롯 UI 연결")]
    [SerializeField, LabelText("북 슬롯 (공격)")]
    private GameObject northSlot;
    // Inspector에서 화면 위쪽에 배치할 슬롯 UI 오브젝트를 연결한다.

    [SerializeField, LabelText("서 슬롯 (선풍기)")]
    private GameObject westSlot;
    // Inspector에서 화면 왼쪽에 배치할 슬롯 UI 오브젝트를 연결한다.

    [SerializeField, LabelText("남 슬롯 (우산)")]
    private GameObject southSlot;
    // Inspector에서 화면 아래쪽에 배치할 슬롯 UI 오브젝트를 연결한다.

    [SerializeField, LabelText("동 슬롯 (횃불)")]
    private GameObject eastSlot;
    // Inspector에서 화면 오른쪽에 배치할 슬롯 UI 오브젝트를 연결한다.

    [Title("연결")]
    [SerializeField, LabelText("Tool Manager")]
    private ToolManager toolManager;
    // Inspector에서 플레이어의 ToolManager 컴포넌트를 연결한다.

    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다.

    [SerializeField, LabelText("휠 패널")]
    private GameObject wheelPanel;
    // 전체 휠 UI를 감싸는 루트 패널이다. 이 오브젝트에 DOTween 열림/닫힘 연출을 적용한다.

    [Title("판정 설정")]
    [SerializeField, LabelText("선택 최소 거리 (px)")]
    private float selectionThreshold = 80f;
    // 화면 중심에서 마우스가 이 거리(픽셀) 이상 벗어나야 슬롯이 선택된다.
    // 값이 작을수록 중심 가까이에서도 슬롯이 선택된다.

    [Title("연출 설정")]
    [SerializeField, LabelText("열림 시간")]
    private float openDuration = 0.15f;

    [SerializeField, LabelText("닫힘 시간")]
    private float closeDuration = 0.1f;

    [SerializeField, LabelText("하이라이트 색상")]
    private Color highlightColor = Color.yellow;
    // 마우스가 향하는 방향의 슬롯에 표시할 강조 색상이다.

    [SerializeField, LabelText("기본 색상")]
    private Color normalColor = Color.white;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 선택 슬롯")]
    private WeaponSlot _currentSlot = WeaponSlot.None;

    [ReadOnly, ShowInInspector, LabelText("휠 열림 여부")]
    private bool _isOpen;

    private InputAction _wheelAction;
    private Tween _panelTween;

    private void Awake()
    {
        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _wheelAction = playerMap.FindAction("WeaponWheel", throwIfNotFound: true);

        // 시작 시 휠 패널을 숨긴다.
        if (wheelPanel != null)
        {
            wheelPanel.transform.localScale = Vector3.zero;
            wheelPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        _wheelAction.Enable();
        _wheelAction.performed += OnWheelPerformed;
        _wheelAction.canceled  += OnWheelCanceled;
    }

    private void OnDisable()
    {
        _wheelAction.performed -= OnWheelPerformed;
        _wheelAction.canceled  -= OnWheelCanceled;
        _wheelAction.Disable();
    }

    // Update는 Time.timeScale=0에서도 매 프레임 호출된다.
    private void Update()
    {
        if (!_isOpen) return;

        UpdateHighlight();
    }

    // Tab 키를 눌렀을 때 휠을 열고 시간을 정지한다.
    private void OnWheelPerformed(InputAction.CallbackContext ctx)
    {
        OpenWheel();
    }

    // Tab 키를 뗐을 때 선택된 슬롯으로 전환하고 시간을 재개한다.
    private void OnWheelCanceled(InputAction.CallbackContext ctx)
    {
        CloseWheel();
    }

    private void OpenWheel()
    {
        _isOpen = true;
        Time.timeScale = 0f;
        // 게임 시간을 정지해 도구 선택 중 피격이나 이동이 발생하지 않도록 한다.

        if (wheelPanel != null)
        {
            wheelPanel.SetActive(true);
            _panelTween?.Kill();
            _panelTween = wheelPanel.transform
                .DOScale(Vector3.one, openDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            // SetUpdate(true)로 Time.timeScale=0에서도 열림 애니메이션이 재생된다.
        }
    }

    private void CloseWheel()
    {
        _isOpen = false;

        // 닫히기 전에 선택된 슬롯을 먼저 ToolManager에 전달한다.
        // 도구 장착 연출이 휠 닫힘 애니메이션과 동시에 시작되도록 하기 위해서다.
        toolManager?.SelectByWheel(_currentSlot);

        ResetHighlights();
        _currentSlot = WeaponSlot.None;

        _panelTween?.Kill();

        if (wheelPanel != null)
        {
            _panelTween = wheelPanel.transform
                .DOScale(Vector3.zero, closeDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    wheelPanel.SetActive(false);
                    Time.timeScale = 1f;
                    // 닫힘 애니메이션이 완전히 끝난 뒤 게임 시간을 재개한다.
                });
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    // 마우스 방향을 계산해 해당하는 슬롯을 하이라이트한다.
    private void UpdateHighlight()
    {
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = mousePos - center;

        if (dir.magnitude < selectionThreshold)
        {
            // 마우스가 중심 가까이 있으면 아무 슬롯도 선택하지 않는다.
            SetHighlight(WeaponSlot.None);
            return;
        }

        // Atan2로 마우스 방향 각도를 구하고 0~360 범위로 정규화한다.
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        // 각도를 4분할해 슬롯을 결정한다.
        // 동(0°) 기준: 시계 반대 방향으로 북(90°), 서(180°), 남(270°)
        WeaponSlot slot;
        if (angle >= 315f || angle < 45f)
            slot = WeaponSlot.Torch;      // 동 — 오른쪽
        else if (angle >= 45f && angle < 135f)
            slot = WeaponSlot.Attack;     // 북 — 위쪽
        else if (angle >= 135f && angle < 225f)
            slot = WeaponSlot.Fan;        // 서 — 왼쪽
        else
            slot = WeaponSlot.Umbrella;   // 남 — 아래쪽

        SetHighlight(slot);
    }

    // 지정된 슬롯을 하이라이트하고 나머지는 기본 색으로 되돌린다.
    private void SetHighlight(WeaponSlot slot)
    {
        if (_currentSlot == slot) return;
        _currentSlot = slot;

        // 모든 슬롯을 기본 색으로 초기화한 뒤 선택 슬롯만 강조한다.
        SetSlotColor(northSlot, normalColor);
        SetSlotColor(westSlot,  normalColor);
        SetSlotColor(southSlot, normalColor);
        SetSlotColor(eastSlot,  normalColor);

        GameObject targetSlot = slot switch
        {
            WeaponSlot.Attack   => northSlot,
            WeaponSlot.Fan      => westSlot,
            WeaponSlot.Umbrella => southSlot,
            WeaponSlot.Torch    => eastSlot,
            _                   => null
        };

        if (targetSlot != null)
            SetSlotColor(targetSlot, highlightColor);
    }

    // 슬롯 오브젝트의 Image 색상을 DOTween으로 변경한다.
    private void SetSlotColor(GameObject slotObj, Color color)
    {
        if (slotObj == null) return;

        Image img = slotObj.GetComponent<Image>();
        if (img != null)
            img.DOColor(color, 0.08f).SetUpdate(true);
        // SetUpdate(true)로 Time.timeScale=0에서도 색상 전환이 재생된다.
    }

    // 모든 슬롯을 기본 색으로 되돌린다.
    private void ResetHighlights()
    {
        SetSlotColor(northSlot, normalColor);
        SetSlotColor(westSlot,  normalColor);
        SetSlotColor(southSlot, normalColor);
        SetSlotColor(eastSlot,  normalColor);
    }

    [Button("휠 열기 테스트")]
    private void TestOpen() => OpenWheel();

    [Button("휠 닫기 테스트")]
    private void TestClose() => CloseWheel();
}
