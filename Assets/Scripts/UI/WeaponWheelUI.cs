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

// Z 키를 누르는 동안 시간을 정지하고 방사형 도구 선택 메뉴를 표시한다.
// Z를 누른 채 화살표로 슬롯을 선택하고(↑ 공격, ← 선풍기, ↓ 우산, → 횃불), Z를 떼면 해당 도구로 전환된다.
// 화살표를 뗀 뒤에도 마지막으로 고른 슬롯이 유지되므로, 화살표를 먼저 떼고 Z를 떼도 선택이 확정된다.
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
    [SerializeField, LabelText("방향 입력 판정 기준"), Range(0.1f, 1f)]
    private float inputThreshold = 0.5f;
    // 화살표(Move 입력)의 세기가 이 값 이상이어야 슬롯이 선택된다. 키보드 화살표는 항상 1이라 기본값이면 충분하다.
    // 게임패드 스틱을 쓸 때 값이 작을수록 살짝만 기울여도 선택된다.

    [Title("연출 설정")]
    [SerializeField, LabelText("열림 시간")]
    private float openDuration = 0.15f;

    [SerializeField, LabelText("닫힘 시간")]
    private float closeDuration = 0.1f;

    [SerializeField, LabelText("하이라이트 색상")]
    private Color highlightColor = Color.yellow;
    // 화살표로 고른 슬롯에 표시할 강조 색상이다.

    [SerializeField, LabelText("기본 색상")]
    private Color normalColor = Color.white;

    [SerializeField, LabelText("잠김 색상")]
    private Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
    // 아직 얻지 못한 도구(가을 퍼즐② 전의 횃불) 슬롯에 표시할 색이다. 화살표로 골라도 강조되지 않는다.

    [Title("사운드 설정")]
    [SerializeField, LabelText("호버 사운드")]
    private AudioClip hoverSound;
    // 화살표로 다른 슬롯을 고를 때 재생할 오디오 클립이다. Inspector에서 UIHoverSound를 연결한다.

    [SerializeField, LabelText("선택 확정 사운드")]
    private AudioClip selectSound;
    // Z를 떼서 도구 선택이 확정될 때 재생할 오디오 클립이다. Inspector에서 ToolSelectSound를 연결한다.

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;
    // Inspector에서 이 오브젝트의 AudioSource 컴포넌트를 연결한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 선택 슬롯")]
    private WeaponSlot _currentSlot = WeaponSlot.None;

    [ReadOnly, ShowInInspector, LabelText("휠 열림 여부")]
    private bool _isOpen;

    // PauseMenuUI가 파우즈 중에 휠이 열리는 것을 막기 위해 상태를 확인하는 용도로 사용한다.
    public bool IsOpen => _isOpen;

    private InputAction _wheelAction;
    private InputAction _moveAction;
    // 휠이 열려 있는 동안 화살표 방향으로 슬롯을 고르기 위해 이동(Move) 액션을 함께 읽는다.
    // PlayerController와 같은 InputActionAsset을 쓰므로 활성화는 PlayerController가 담당한다.
    private Tween _panelTween;

    private void Awake()
    {
        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _wheelAction = playerMap.FindAction("WeaponWheel", throwIfNotFound: true);
        _moveAction = playerMap.FindAction("Move", throwIfNotFound: true);

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

        if (toolManager != null)
            toolManager.OnUnlockStateChanged += ResetHighlights;
        // 횃불을 얻거나 잠기면 슬롯 색(잠김/기본)을 바로 다시 칠한다.
    }

    private void OnDisable()
    {
        _wheelAction.performed -= OnWheelPerformed;
        _wheelAction.canceled  -= OnWheelCanceled;
        _wheelAction.Disable();

        if (toolManager != null)
            toolManager.OnUnlockStateChanged -= ResetHighlights;
    }

    // Update는 Time.timeScale=0에서도 매 프레임 호출된다.
    private void Update()
    {
        if (!_isOpen) return;

        UpdateHighlight();
    }

    // Z 키를 눌렀을 때 휠을 열고 시간을 정지한다.
    private void OnWheelPerformed(InputAction.CallbackContext ctx)
    {
        // 파우즈 메뉴가 열려 있는 동안에는 휠을 열지 않는다.
        // 두 UI가 동시에 Time.timeScale을 제어하면 어느 한쪽을 닫을 때 시간이 잘못 풀릴 수 있기 때문이다.
        if (PauseMenuUI.IsPaused) return;

        OpenWheel();
    }

    // Z 키를 뗐을 때 선택된 슬롯으로 전환하고 시간을 재개한다.
    private void OnWheelCanceled(InputAction.CallbackContext ctx)
    {
        CloseWheel();
    }

    private void OpenWheel()
    {
        _isOpen = true;
        Time.timeScale = 0f;
        // 게임 시간을 정지해 도구 선택 중 피격이나 이동이 발생하지 않도록 한다.

        // 열 때마다 잠긴 슬롯이 회색으로 보이도록 색을 맞춘다.
        ResetHighlights();

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

        // 실제로 방향을 선택했을 때만(None=중심에서 뗀 취소가 아닐 때) 확정 사운드를 재생한다.
        if (_currentSlot != WeaponSlot.None && audioSource != null && selectSound != null)
            audioSource.PlayOneShot(selectSound);

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

    // 화살표 입력 방향으로 해당하는 슬롯을 하이라이트한다.
    private void UpdateHighlight()
    {
        Vector2 dir = _moveAction.ReadValue<Vector2>();

        // 화살표를 누르지 않으면 선택을 바꾸지 않는다.
        // 화살표를 먼저 떼고 Z를 떼도 마지막으로 고른 슬롯이 확정되게 하기 위해서다.
        if (dir.magnitude < inputThreshold) return;

        // 가로/세로 중 더 크게 누른 축으로 슬롯을 정한다. 대각선(두 화살표 동시)이면 가로를 우선한다.
        WeaponSlot slot;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            slot = dir.x > 0f ? WeaponSlot.Torch : WeaponSlot.Fan;        // → 횃불 / ← 선풍기
        else
            slot = dir.y > 0f ? WeaponSlot.Attack : WeaponSlot.Umbrella;  // ↑ 공격 / ↓ 우산

        // 잠긴 도구 방향이면 아무것도 고르지 않은 것으로 본다. 휠을 닫아도 현재 도구가 유지된다.
        if (!IsUnlocked(slot))
            slot = WeaponSlot.None;

        SetHighlight(slot);
    }

    // 지정된 슬롯을 하이라이트하고 나머지는 기본 색으로 되돌린다.
    private void SetHighlight(WeaponSlot slot)
    {
        if (_currentSlot == slot) return;
        _currentSlot = slot;

        // 중심(None)으로 돌아올 때는 재생하지 않고, 실제로 다른 방향 슬롯으로 넘어갈 때만 재생한다.
        if (slot != WeaponSlot.None && audioSource != null && hoverSound != null)
            audioSource.PlayOneShot(hoverSound);

        // 모든 슬롯을 기본(또는 잠김) 색으로 초기화한 뒤 선택 슬롯만 강조한다.
        ResetHighlights();

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

    // 모든 슬롯을 기본 색으로 되돌린다. 잠긴 도구 슬롯은 잠김 색으로 칠한다.
    private void ResetHighlights()
    {
        SetSlotColor(northSlot, RestColor(WeaponSlot.Attack));
        SetSlotColor(westSlot,  RestColor(WeaponSlot.Fan));
        SetSlotColor(southSlot, RestColor(WeaponSlot.Umbrella));
        SetSlotColor(eastSlot,  RestColor(WeaponSlot.Torch));
    }

    private Color RestColor(WeaponSlot slot) => IsUnlocked(slot) ? normalColor : lockedColor;

    // ToolManager가 연결되지 않은 씬에서는 잠금 없이 기존처럼 모두 선택 가능하게 둔다.
    private bool IsUnlocked(WeaponSlot slot) => toolManager == null || toolManager.IsSlotUnlocked(slot);

    [Button("휠 열기 테스트")]
    private void TestOpen() => OpenWheel();

    [Button("휠 닫기 테스트")]
    private void TestClose() => CloseWheel();
}
