using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 타이틀 화면의 루트 메뉴(처음부터/이어하기/종료)를 담당한다.
// 실제 슬롯 목록/새게임/이어하기 로직은 SaveSlotSelectController가 처리하고,
// 이 스크립트는 버튼 클릭을 그쪽으로 넘기는 역할만 한다.
public class TitleMenuController : MonoBehaviour
{
    [Title("버튼 연결")]
    [SerializeField, LabelText("처음부터 버튼")]
    private Button newGameButton;

    [SerializeField, LabelText("이어하기 버튼")]
    private Button continueButton;

    [SerializeField, LabelText("종료 버튼")]
    private Button quitButton;

    [Title("연결")]
    [SerializeField, LabelText("슬롯 선택 컨트롤러")]
    private SaveSlotSelectController slotSelectController;

    [SerializeField, LabelText("세이브 슬롯 개수")]
    private int slotCount = 3;
    // SaveSlotSelectController의 슬롯 UI 목록 개수와 일치해야 한다 (Inspector에서 함께 맞춰준다).

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGameClicked);
        continueButton.onClick.AddListener(OnContinueClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    // 세이브 슬롯이 하나도 없으면 이어하기 버튼을 눌러도 할 게 없으므로 아예 비활성화한다.
    private void Start()
    {
        bool anySaveExists = false;
        for (int slot = 1; slot <= slotCount; slot++)
        {
            if (SaveManager.Instance.HasSave(slot))
            {
                anySaveExists = true;
                break;
            }
        }

        continueButton.interactable = anySaveExists;
    }

    private void OnNewGameClicked() => slotSelectController.Open(SaveSlotSelectController.Mode.NewGame);

    private void OnContinueClicked() => slotSelectController.Open(SaveSlotSelectController.Mode.Continue);

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        // 에디터에서 Play Mode 중에는 Application.Quit()이 동작하지 않으므로 별도로 처리한다.
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
