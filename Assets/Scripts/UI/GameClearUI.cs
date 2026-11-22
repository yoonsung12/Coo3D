using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 최종보스(비둘킹)가 쓰러졌을 때 표시되는 게임 클리어 화면이다.
// Boss.OnDied 이벤트를 구독해 잠시 기다린 뒤(적 사망 연출이 먼저 끝나도록) 시간을 멈추고,
// 어두운 오버레이 -> GAME CLEAR 타이틀 -> 통계 -> 버튼(타이틀로/게임 종료) 순서로 연출한다.
// GameOverUI와 같이 CanvasGroup으로 보이기/숨기기를 제어한다.
// 주의: 보스와 플레이어가 동시에 죽어 게임오버 패널이 떠 있어도 클리어 화면이 이기도록,
// 하이어라키에서 GameClearPanel을 GameOverPanel보다 "뒤(아래)"에 두어 위에 그려지게 해야 한다.
public class GameClearUI : MonoBehaviour
{
    public static bool IsShowing { get; private set; }
    // 클리어 화면이 떠 있는 동안 true다. PauseMenuUI가 이 값을 보고 Esc를 무시한다.

    [Title("연결")]
    [SerializeField, LabelText("패널 (CanvasGroup)")]
    private CanvasGroup panel;
    // 항상 켜둔 채 CanvasGroup으로만 보이기/입력 차단을 제어한다(GameOverUI와 같은 이유).

    [SerializeField, LabelText("어두운 오버레이")]
    private Image darkOverlay;

    [SerializeField, LabelText("GAME CLEAR 타이틀")]
    private Text titleText;

    [SerializeField, LabelText("통계 그룹 (CanvasGroup)")]
    private CanvasGroup statsGroup;

    [SerializeField, LabelText("플레이 시간 값")]
    private Text playTimeValueText;

    [SerializeField, LabelText("모은 코인 값")]
    private Text coinValueText;

    [SerializeField, LabelText("쓰러진 횟수 값")]
    private Text deathValueText;

    [SerializeField, LabelText("타이틀로 버튼")]
    private Button titleButton;

    [SerializeField, LabelText("게임 종료 버튼")]
    private Button quitButton;

    [Title("클리어 판정 대상")]
    [SerializeField, LabelText("최종보스")]
    private Boss boss;
    // 이 보스의 OnDied 이벤트(Enemy 기반 클래스)를 구독해 자동으로 클리어 화면을 띄운다.

    [SerializeField, LabelText("타이틀 씬 이름")]
    private string titleSceneName = "Title";

    [Title("연출 설정")]
    [SerializeField, LabelText("보스 사망 후 표시 지연")]
    private float showDelay = 1.5f;
    // 보스 사망 축소 연출이 끝날 시간을 주기 위한 대기 시간이다.

    [SerializeField, LabelText("오버레이 목표 알파")]
    private float overlayTargetAlpha = 0.6f;

    [SerializeField, LabelText("오버레이 페이드 시간")]
    private float overlayFadeDuration = 0.6f;

    [SerializeField, LabelText("타이틀 등장 시간")]
    private float titleDuration = 0.5f;

    [SerializeField, LabelText("타이틀 시작 크기 배율")]
    private float titleStartScale = 1.4f;
    // 타이틀이 이 배율에서 1로 줄어들며 나타난다. 클수록 임팩트가 커진다.

    [SerializeField, LabelText("통계 등장 시간")]
    private float statsFadeDuration = 0.4f;

    [SerializeField, LabelText("Ease")]
    private Ease easeType = Ease.OutQuad;

    [Title("버튼 색")]
    [SerializeField, LabelText("선택됨 글자색")]
    private Color selectedTextColor = new Color(0.1f, 0.08f, 0.04f, 1f);

    [SerializeField, LabelText("일반 글자색")]
    private Color normalTextColor = new Color(0.9f, 0.9f, 0.95f, 1f);
    // 버튼 배경색은 Button의 ColorTint(Normal/Selected)로, 글자색은 아래 UpdateButtonHighlight에서 바꾼다.

    private Sequence _showSequence;
    // 등장 연출 Sequence다. 중복 실행 방지를 위해 저장해 둔다.

    private Tween _delayTween;
    // 보스 사망 후 표시 지연용 타이머다. timeScale과 무관하게 돌도록 SetUpdate(true)를 쓴다.

    private bool _shown;

    private void Awake()
    {
        SetVisible(false);

        if (titleButton != null)
            titleButton.onClick.AddListener(HandleTitleClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuitClicked);
    }

    private void OnEnable()
    {
        if (boss != null)
            boss.OnDied += HandleBossDied;
    }

    private void OnDisable()
    {
        if (boss != null)
            boss.OnDied -= HandleBossDied;
    }

    private void OnDestroy()
    {
        _showSequence?.Kill();
        _delayTween?.Kill();
        // 씬 전환 등으로 파괴될 때 남은 정적 상태가 다음 씬의 Esc를 막지 않도록 되돌린다.
        IsShowing = false;
    }

    private void Update()
    {
        if (_shown)
            UpdateButtonHighlight();
    }

    private void HandleBossDied()
    {
        if (_shown) return;

        _delayTween?.Kill();
        _delayTween = DOVirtual.DelayedCall(showDelay, Show).SetUpdate(true);
    }

    // 클리어 화면을 표시한다. 보스 사망 후 자동 호출되고, 테스트 버튼으로도 호출할 수 있다.
    public void Show()
    {
        if (_shown) return;
        _shown = true;
        IsShowing = true;

        // 버튼을 누르기 전까지 게임 진행이 멈춰야 하므로 시간을 정지시킨다.
        Time.timeScale = 0f;

        FillStats();
        SetVisible(true);

        // 연출 시작 전 초기 상태로 되돌린다.
        if (darkOverlay != null)
        {
            Color c = darkOverlay.color;
            darkOverlay.color = new Color(c.r, c.g, c.b, 0f);
        }

        if (titleText != null)
        {
            Color c = titleText.color;
            titleText.color = new Color(c.r, c.g, c.b, 0f);
            titleText.rectTransform.localScale = Vector3.one * titleStartScale;
        }

        if (statsGroup != null)
            statsGroup.alpha = 0f;

        SetButtonsActive(false);

        _showSequence?.Kill();
        _showSequence = DOTween.Sequence();
        // Time.timeScale = 0 상태이므로 Unscaled Time 기준으로 재생한다.
        _showSequence.SetUpdate(true);

        if (darkOverlay != null)
            _showSequence.Append(darkOverlay.DOFade(overlayTargetAlpha, overlayFadeDuration).SetEase(Ease.InQuad));

        if (titleText != null)
        {
            _showSequence.Append(titleText.DOFade(1f, titleDuration).SetEase(easeType));
            _showSequence.Join(titleText.rectTransform.DOScale(1f, titleDuration).SetEase(easeType));
        }

        if (statsGroup != null)
            _showSequence.Append(statsGroup.DOFade(1f, statsFadeDuration).SetEase(easeType));

        _showSequence.AppendCallback(() =>
        {
            SetButtonsActive(true);
            // 방향키 좌/우 + Enter로 조작할 수 있도록 "타이틀로" 버튼을 기본 선택한다.
            if (EventSystem.current != null && titleButton != null)
                EventSystem.current.SetSelectedGameObject(titleButton.gameObject);
        });
    }

    // 저장 매니저/코인 매니저의 값을 읽어 통계 텍스트를 채운다. 매니저가 없는 에디터 실행에서는 기본값을 보여준다.
    private void FillStats()
    {
        if (playTimeValueText != null)
        {
            if (SaveManager.Instance != null)
            {
                TimeSpan t = TimeSpan.FromSeconds(SaveManager.Instance.PlayTimeSeconds);
                // 24시간을 넘겨도 시간이 이어지도록 TotalHours를 쓴다.
                playTimeValueText.text = string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            }
            else
            {
                playTimeValueText.text = "--:--:--";
            }
        }

        if (coinValueText != null)
            coinValueText.text = (CoinManager.Instance != null ? CoinManager.Instance.CoinCount : 0).ToString();

        if (deathValueText != null)
            deathValueText.text = (SaveManager.Instance != null ? SaveManager.Instance.DeathCount : 0).ToString();
    }

    private void SetButtonsActive(bool active)
    {
        if (titleButton != null) titleButton.gameObject.SetActive(active);
        if (quitButton != null) quitButton.gameObject.SetActive(active);
    }

    // 현재 선택된 버튼의 글자색만 어둡게 바꾼다(배경 금색은 Button ColorTint가 처리).
    private void UpdateButtonHighlight()
    {
        if (EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        ApplyTextColor(titleButton, selected);
        ApplyTextColor(quitButton, selected);
    }

    private void ApplyTextColor(Button button, GameObject selected)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null)
            label.color = (selected == button.gameObject) ? selectedTextColor : normalTextColor;
    }

    private void HandleTitleClicked()
    {
        // 타이틀 씬이 멈춘 채 시작하지 않도록 씬을 옮기기 전에 시간을 되돌린다.
        IsShowing = false;
        _shown = false;
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
        if (panel == null) return;

        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }

    [Button("클리어 화면 테스트 (Play Mode)")]
    private void TestShow()
    {
        if (!Application.isPlaying) return;
        Show();
    }
}
