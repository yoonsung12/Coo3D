using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// "처음부터"/"이어하기" 양쪽에서 공유하는 세이브 슬롯 선택 화면이다.
// 모드(NewGame/Continue)에 따라 슬롯 클릭 시 동작이 달라진다.
public class SaveSlotSelectController : MonoBehaviour
{
    public enum Mode
    {
        NewGame,
        Continue
    }

    [Title("패널 연결")]
    [SerializeField, LabelText("슬롯 선택 패널 (CanvasGroup)")]
    private CanvasGroup panel;
    // 열림/닫힘 페이드 연출과 활성화 여부를 함께 제어하기 위해 CanvasGroup을 사용한다.

    [SerializeField, LabelText("타이틀 루트 패널 (CanvasGroup)")]
    private CanvasGroup titleRootPanel;
    // 슬롯 화면을 열 때 숨기고, 뒤로가기를 누르면 다시 보여줄 타이틀 화면이다.

    [SerializeField, LabelText("뒤로가기 버튼")]
    private Button backButton;

    [Title("슬롯 UI 연결")]
    [SerializeField, LabelText("슬롯 UI 목록 (슬롯 1, 2, 3 순서로 연결)")]
    private SaveSlotUIItem[] slotItems;
    // 배열 인덱스 i는 세이브 슬롯 번호 (i + 1)에 대응한다.

    [SerializeField, LabelText("확인 팝업")]
    private ConfirmPopupUI confirmPopup;

    [Title("설정")]
    [SerializeField, LabelText("게임플레이 씬 이름")]
    private string gameplaySceneName = "SampleScene";
    // 씬 이름이 바뀌거나 스테이지가 늘어날 상황을 대비해 하드코딩하지 않고 Inspector에서 조절 가능하게 한다.

    [SerializeField, LabelText("패널 페이드 시간")]
    private float fadeDuration = 0.2f;

    private Mode _currentMode;
    private Tween _fadeTween;

    private void Awake()
    {
        // 슬롯 버튼 클릭 리스너는 여기서 한 번만 연결한다. RefreshSlots는 텍스트/버튼 상태만 갱신하고
        // 리스너를 다시 등록하지 않아야, 화면을 여러 번 열어도 클릭 시 중복 호출되지 않는다.
        for (int i = 0; i < slotItems.Length; i++)
        {
            int slot = i + 1;
            // 로컬 변수로 복사하지 않으면 클로저가 마지막 i 값을 공유해 모든 버튼이 같은 슬롯을 가리키게 된다.
            slotItems[i].SelectButton.onClick.AddListener(() => OnSlotSelected(slot));
            slotItems[i].DeleteButton.onClick.AddListener(() => OnSlotDeleteClicked(slot));
        }

        backButton.onClick.AddListener(Close);

        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    // mode에 맞게 슬롯 목록을 새로고침하고 패널을 페이드 인으로 연다.
    // titleRootPanel/panel 모두 GameObject 자체는 항상 켜둔 채 CanvasGroup만 조절한다.
    // 이 메서드는 titleRootPanel의 자식인 "처음부터"/"이어하기" 버튼의 클릭 핸들러에서 호출되는데,
    // 예전처럼 titleRootPanel을 SetActive(false)로 끄면 지금 클릭된 버튼의 부모를 같은 프레임에
    // 비활성화하는 셈이라 Unity 이벤트 처리가 꼬여 슬롯 화면이 뜨지 않는 문제가 있었다.
    // GameObject를 항상 켜두면 이 문제가 사라지고, 타이틀 배경도 슬롯 화면에서 계속 보인다.
    public void Open(Mode mode)
    {
        _currentMode = mode;
        RefreshSlots();

        titleRootPanel.alpha = 0f;
        titleRootPanel.interactable = false;
        titleRootPanel.blocksRaycasts = false;

        panel.interactable = true;
        panel.blocksRaycasts = true;
        panel.alpha = 0f;
        _fadeTween?.Kill();
        _fadeTween = panel.DOFade(1f, fadeDuration);
    }

    private void Close()
    {
        panel.interactable = false;
        panel.blocksRaycasts = false;
        _fadeTween?.Kill();
        _fadeTween = panel.DOFade(0f, fadeDuration);

        titleRootPanel.alpha = 1f;
        titleRootPanel.interactable = true;
        titleRootPanel.blocksRaycasts = true;
    }

    // 슬롯마다 SaveManager.PeekSlotData로 저장 여부/시각을 읽어 텍스트를 갱신하고,
    // 이어하기 모드에서는 빈 슬롯을 선택할 수 없도록 선택 버튼을 비활성화한다.
    private void RefreshSlots()
    {
        for (int i = 0; i < slotItems.Length; i++)
        {
            int slot = i + 1;
            SaveData data = SaveManager.Instance.PeekSlotData(slot);
            bool hasSave = data != null;

            slotItems[i].SetState(hasSave, data?.savedAtIso);
            slotItems[i].SelectButton.interactable = hasSave || _currentMode == Mode.NewGame;
        }
    }

    private void OnSlotSelected(int slot)
    {
        if (_currentMode == Mode.Continue)
        {
            SaveManager.Instance.LoadGame(slot);
            // 씬 전환/위치 복원은 기존 LoadGame 로직이 전부 처리한다.
            // Title 씬과 저장된 씬 이름이 다르므로, LoadGame 내부의 "다른 씬" 분기가 자동으로 타서
            // gameplaySceneName을 불러오고 체크포인트 위치를 복원한다.
            return;
        }

        // NewGame 모드: 이미 세이브가 있는 슬롯이면 덮어쓰기 확인을 먼저 받는다.
        if (SaveManager.Instance.HasSave(slot))
            confirmPopup.Show("정말 덮어쓰시겠습니까?", () => StartNewGame(slot));
        else
            StartNewGame(slot);
    }

    // SceneManager.LoadScene은 이 프레임 안에서 즉시 활성 씬을 바꾸지 않으므로(다음 프레임에 반영),
    // NewGame(slot)만 호출하면 여전히 "Title"이 활성 씬으로 기록된다.
    // 옮겨갈 씬 이름을 이미 알고 있으므로 NewGame(slot, sceneName) 오버로드로 직접 전달한다.
    private void StartNewGame(int slot)
    {
        SceneManager.LoadScene(gameplaySceneName);
        SaveManager.Instance.NewGame(slot, gameplaySceneName);
    }

    private void OnSlotDeleteClicked(int slot)
    {
        confirmPopup.Show("이 세이브를 삭제하시겠습니까?", () =>
        {
            SaveManager.Instance.DeleteSlot(slot);
            RefreshSlots();
        });
    }
}
