using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ESC(UI 액션맵의 Cancel 액션)로 열고 닫는 파우즈 메뉴다.
// 계속하기/저장하기/타이틀로 나가기/종료 네 가지 기능만 제공한다.
public class PauseMenuUI : MonoBehaviour
{
    // WeaponWheelUI 등 다른 스크립트가 GameObject.Find 없이 파우즈 상태를 확인할 수 있도록 정적으로 노출한다.
    public static bool IsPaused { get; private set; }

    [Title("버튼 연결")]
    [SerializeField, LabelText("계속하기 버튼")]
    private Button resumeButton;

    [SerializeField, LabelText("저장하기 버튼")]
    private Button saveButton;

    [SerializeField, LabelText("체크포인트로 이동 버튼")]
    private Button checkpointButton;
    // 지형에 끼는 등 진행이 막혔을 때 마지막 체크포인트로 돌아가기 위한 버튼이다. 비워두면 이 기능 없이 동작한다.

    [SerializeField, LabelText("사운드 설정 버튼")]
    private Button soundButton;

    [SerializeField, LabelText("타이틀로 나가기 버튼")]
    private Button exitToTitleButton;

    [SerializeField, LabelText("종료 버튼")]
    private Button quitButton;

    [Title("연결")]
    [SerializeField, LabelText("파우즈 패널 (CanvasGroup)")]
    private CanvasGroup panel;
    // ConfirmPopupUI와 같은 이유로 패널 오브젝트 자체를 SetActive로 끄지 않고 CanvasGroup으로만 보이기/막기를 제어한다.
    // 버튼 클릭과 같은 프레임에 부모(패널)를 비활성화하면 클릭 처리가 꼬일 수 있기 때문이다.

    [SerializeField, LabelText("사운드 설정 화면")]
    private SoundSettingsUI soundSettingsUI;
    // "사운드 설정" 버튼이 열어 주는 화면이다. 열려 있는 동안 파우즈 메뉴는 뒤에 그대로 남는다.

    [SerializeField, LabelText("나가기 확인 팝업")]
    private ConfirmPopupUI exitConfirmPopup;

    [SerializeField, LabelText("세이브 슬롯 선택 화면")]
    private SaveSlotSelectController saveSlotSelectController;
    // "저장하기" 버튼이 이 화면을 SaveInGame 모드로 열어서, 플레이어가 슬롯을 직접 골라 저장하게 한다.

    [SerializeField, LabelText("무기 휠 UI")]
    private WeaponWheelUI weaponWheelUI;
    // 휠이 열려 있는 동안에는 ESC를 무시하기 위해 상태 확인용으로만 참조한다.

    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다. UI 맵의 Cancel 액션을 사용한다.

    [SerializeField, LabelText("타이틀 씬 이름")]
    private string titleSceneName = "Title";

    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // 체크포인트로 순간이동한 직후 카메라를 바로 옮기기 위해 연결한다. 비워두면 카메라가 평소처럼 Lerp로 따라간다.

    [Title("체크포인트 이동 연출")]
    [SerializeField, LabelText("페이드 아웃 시간")]
    private float checkpointFadeOutDuration = 0.3f;

    [SerializeField, LabelText("페이드 인 시간")]
    private float checkpointFadeInDuration = 0.3f;
    // 화면이 완전히 어두워진 뒤 옮겨야 순간이동이 보이지 않는다. 값이 클수록 천천히 어두워지고 밝아진다.

    [Title("연출 설정")]
    [SerializeField, LabelText("열림 시간")]
    private float openDuration = 0.2f;

    [SerializeField, LabelText("닫힘 시간")]
    private float closeDuration = 0.15f;

    [SerializeField, LabelText("등장 Ease")]
    private Ease openEase = Ease.OutBack;

    [SerializeField, LabelText("사라짐 Ease")]
    private Ease closeEase = Ease.InBack;

    private InputAction _cancelAction;
    private InputActionMap _playerActionMap;
    // 파우즈 중 이동/공격/도구 입력을 통째로 막기 위해 Player 액션맵을 캐시해 둔다.
    // 이게 없으면 "계속하기" 버튼을 마우스로 클릭할 때 그 클릭이 공격(Attack) 입력으로도 같이 인식되어,
    // Time.timeScale이 0에서 1로 돌아오는 순간 밀려 있던 공격 애니메이션이 재생되는 문제가 있었다.
    private Tween _panelTween;

    private void Awake()
    {
        var uiMap = inputActionAsset.FindActionMap("UI", throwIfNotFound: true);
        _cancelAction = uiMap.FindAction("Cancel", throwIfNotFound: true);

        _playerActionMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);

        resumeButton.onClick.AddListener(Resume);
        saveButton.onClick.AddListener(HandleSaveClicked);
        if (checkpointButton != null)
            checkpointButton.onClick.AddListener(HandleCheckpointClicked);
        soundButton.onClick.AddListener(HandleSoundClicked);
        exitToTitleButton.onClick.AddListener(HandleExitToTitleClicked);
        quitButton.onClick.AddListener(HandleQuitClicked);

        panel.transform.localScale = Vector3.zero;
        SetVisible(false);
        IsPaused = false;
    }

    private void OnEnable()
    {
        _cancelAction.Enable();
        _cancelAction.performed += OnCancelPerformed;
    }

    private void OnDisable()
    {
        _cancelAction.performed -= OnCancelPerformed;
        _cancelAction.Disable();
    }

    private void OnDestroy()
    {
        _panelTween?.Kill();
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        // 가이드북과 파우즈는 같은 Esc(Cancel) 액션을 구독한다.
        // 가이드북이 열려 있거나 이번 프레임에 방금 닫혔다면 무시한다. 프레임 검사가 없으면
        // 가이드북이 먼저 닫힌 뒤 같은 입력으로 파우즈가 바로 열려 버린다.
        if (GuideBookUI.IsOpen || GuideBookUI.LastClosedFrame == Time.frameCount) return;

        // 사운드 설정 화면도 같은 Esc(Cancel)로 닫히므로 가이드북과 같은 방식으로 무시한다.
        if (SoundSettingsUI.IsOpen || SoundSettingsUI.LastClosedFrame == Time.frameCount) return;

        // 무기 휠이 열려 있는 동안에는 ESC를 무시한다.
        // 두 UI가 동시에 Time.timeScale을 제어하면 한쪽을 닫을 때 시간이 잘못 풀리는 문제가 생길 수 있다.
        if (weaponWheelUI != null && weaponWheelUI.IsOpen) return;

        // 세이브 슬롯 선택 화면이 열려 있는 동안에도 ESC를 무시한다.
        // 그렇지 않으면 슬롯 화면은 그대로 남은 채 파우즈 전체만 풀려버리는 상태가 될 수 있다.
        if (saveSlotSelectController != null && saveSlotSelectController.IsOpen) return;

        if (IsPaused)
            Resume();
        else
            Pause();
    }

    private void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        // 파우즈 중에는 이동/공격/도구 입력이 전부 멈춰야 하므로 게임 시간을 정지한다.

        _playerActionMap.Disable();
        // Time.timeScale만으로는 입력 "이벤트" 자체는 계속 들어와 밀려 있다가 재개 시 한꺼번에 실행될 수 있다.
        // Player 액션맵을 아예 꺼서 파우즈 중 클릭/키 입력이 게임 로직으로 전달되지 않게 막는다.

        SetVisible(true);

        _panelTween?.Kill();
        _panelTween = panel.transform
            .DOScale(Vector3.one, openDuration)
            .SetEase(openEase)
            .SetUpdate(true);
        // SetUpdate(true)로 Time.timeScale=0 상태에서도 열림 연출이 재생된다.
    }

    private void Resume()
    {
        IsPaused = false;

        _panelTween?.Kill();
        _panelTween = panel.transform
            .DOScale(Vector3.zero, closeDuration)
            .SetEase(closeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                SetVisible(false);
                Time.timeScale = 1f;
                _playerActionMap.Enable();
                // 닫힘 연출이 끝나 게임 시간이 실제로 다시 흐르는 시점에 맞춰 입력도 함께 복구한다.
            });
    }

    // 슬롯을 직접 골라서 저장할 수 있도록 세이브 슬롯 선택 화면을 연다.
    private void HandleSaveClicked()
    {
        saveSlotSelectController.Open(SaveSlotSelectController.Mode.SaveInGame);
    }

    // 확인 팝업을 한 번 거친 뒤 마지막 체크포인트로 이동한다. 실수로 눌러 먼 곳으로 돌아가는 일을 막기 위해서다.
    private void HandleCheckpointClicked()
    {
        exitConfirmPopup.Show("마지막 체크포인트로 이동하시겠습니까?", MoveToCheckpoint);
    }

    // 파우즈를 닫고, 화면을 어둡게 한 뒤 체크포인트로 옮기고 다시 밝힌다. 체력·디버프·게이지는 그대로 둔다.
    private void MoveToCheckpoint()
    {
        PlayerHealth health = PlayerHealth.Instance;
        if (health == null) return;

        PlayerController controller = health.GetComponent<PlayerController>();
        // 엘리베이터·포털 연출, 컷신, 사망 중에는 PlayerController가 꺼져 있다.
        // 그 도중에 순간이동하면 진행 중인 연출 코루틴과 위치가 꼬이므로 이동하지 않고 메뉴만 닫는다.
        if (controller == null || !controller.enabled)
        {
            Resume();
            return;
        }

        Resume();

        controller.enabled = false;
        // 화면이 어두워지는 동안 움직이지 않도록 잠시 조작을 끈다.

        System.Action teleport = () =>
        {
            // 순간이동은 반드시 Respawn()을 거쳐야 CharacterController, 튜브 탑승, 시점(사이드뷰/탑다운)이 함께 정리된다.
            health.Respawn(CheckpointManager.CurrentCheckpointPosition);
            if (sideViewCamera != null)
                sideViewCamera.SnapToTarget();

            controller.enabled = true;

            if (ScreenFader.Instance != null)
                ScreenFader.Instance.FadeIn(null, checkpointFadeInDuration);
        };

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(() => teleport(), checkpointFadeOutDuration);
        else
            teleport();
    }

    // 파우즈 메뉴 위에 사운드 설정 화면을 연다.
    private void HandleSoundClicked()
    {
        soundSettingsUI.Open();
    }

    // 되돌리기 어려운 동작이므로 확인 팝업을 한 번 거친 뒤에만 타이틀로 이동한다.
    private void HandleExitToTitleClicked()
    {
        exitConfirmPopup.Show("저장하지 않은 진행 상황이 있을 수 있습니다.\n타이틀로 나가시겠습니까?", GoToTitle);
    }

    private void GoToTitle()
    {
        // 타이틀 씬이 멈춰있는 채로 시작하지 않도록, 씬을 옮기기 전에 반드시 시간을 되돌린다.
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    private void HandleQuitClicked()
    {
#if UNITY_EDITOR
        // 에디터 Play Mode 중에는 Application.Quit()이 동작하지 않으므로 별도로 처리한다.
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetVisible(bool visible)
    {
        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }

    [Button("파우즈 열기 테스트")]
    private void TestPause() => Pause();

    [Button("파우즈 닫기 테스트")]
    private void TestResume() => Resume();

    [Button("체크포인트 이동 테스트")]
    private void TestMoveToCheckpoint()
    {
        if (!Application.isPlaying) return;
        MoveToCheckpoint();
    }
}
