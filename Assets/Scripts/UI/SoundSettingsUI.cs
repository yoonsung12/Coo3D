using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 카테고리별(전체/배경음악/효과음/UI) 볼륨을 조절하는 사운드 설정 화면이다.
// 파우즈 메뉴와 타이틀 화면 양쪽에서 같은 프리팹을 쓰며, 실제 값 저장/적용은 SoundSettings가 담당한다.
public class SoundSettingsUI : MonoBehaviour
{
    // PauseMenuUI가 같은 Esc 입력을 받을 때 설정 화면 상태를 확인할 수 있도록 정적으로 노출한다.
    public static bool IsOpen { get; private set; }

    // 설정 화면이 Esc로 닫힌 프레임 번호다. (GuideBookUI와 같은 이유)
    // Esc 하나로 설정 화면이 닫히고 같은 프레임에 파우즈까지 풀려 버리는 것을 막기 위해 기록한다.
    public static int LastClosedFrame { get; private set; } = -1;

    // 한 줄(슬라이더 + % 글자 + 선택 강조 배경)을 묶은 데이터다.
    [Serializable]
    private class Row
    {
        public SoundCategory category;
        public Slider slider;
        public Text valueText;
        public Image highlight;
    }

    [Title("화면 연결")]
    [SerializeField, LabelText("패널 그룹 (CanvasGroup)")]
    private CanvasGroup panelGroup;
    // 패널을 SetActive로 끄지 않고 CanvasGroup으로 보이기/막기를 제어한다. (PauseMenuUI와 같은 이유)

    [SerializeField, LabelText("패널 본체")]
    private RectTransform panelRoot;
    // 열림/닫힘 때 크기가 변하는 대상이다.

    [SerializeField, LabelText("항목 줄 (전체/배경음악/효과음/UI 순서)")]
    private Row[] rows;

    [SerializeField, LabelText("기본값 버튼")]
    private Button resetButton;

    [SerializeField, LabelText("닫기 버튼")]
    private Button closeButton;

    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Assets/InputSystem_Actions 에셋을 연결한다. UI 맵의 Navigate(방향키)/Cancel(Esc)을 사용한다.

    [SerializeField, LabelText("입력 임계값")]
    private float inputThreshold = 0.5f;
    // 방향키가 이 값을 넘는 순간 한 번만 반응한다. (GuideBookUI와 같은 방식)

    [SerializeField, LabelText("조절 단위")]
    private float step = 0.1f;
    // ←/→ 한 번에 바뀌는 볼륨 양이다. (0.1 = 10%)

    [Title("DOTween 연출 설정")]
    [SerializeField, LabelText("열림 시간")]
    private float openDuration = 0.2f;

    [SerializeField, LabelText("열림 Ease")]
    private Ease openEase = Ease.OutBack;

    [SerializeField, LabelText("열림 시작 크기")]
    private float openStartScale = 0.9f;

    [SerializeField, LabelText("닫힘 시간")]
    private float closeDuration = 0.15f;

    [SerializeField, LabelText("닫힘 Ease")]
    private Ease closeEase = Ease.InQuad;

    [SerializeField, LabelText("선택 강조 시간")]
    private float highlightDuration = 0.12f;

    [SerializeField, LabelText("선택 강조 투명도"), Range(0f, 1f)]
    private float highlightAlpha = 0.55f;
    // 선택된 줄 배경이 이 투명도까지 진해진다. 선택되지 않은 줄은 0이다.

    [Title("미리듣기")]
    [SerializeField, LabelText("미리듣기 오디오 소스")]
    private AudioSource previewSource;
    // 패널에 붙인 2D AudioSource를 연결한다. 재생 직전에 해당 카테고리의 믹서 그룹으로 바꿔 소리를 낸다.

    [SerializeField, LabelText("UI 미리듣기 소리")]
    private AudioClip uiPreviewClip;
    // Assets/Sound/UI/GuideBook/Book_PageFlip.wav

    [SerializeField, LabelText("효과음 미리듣기 소리")]
    private AudioClip sfxPreviewClip;
    // Assets/Sound/Attack/PillowHit_A.wav

    [SerializeField, LabelText("미리듣기 최소 간격(초)")]
    private float previewInterval = 0.12f;
    // 키를 누르고 있어도 소리가 연달아 겹치지 않게 막는 간격이다.

    private InputAction _cancelAction;
    private InputAction _navigateAction;
    private Action _onClosed;
    private int _selectedIndex;
    private int _prevDirX;
    private int _prevDirY;
    private float _lastPreviewTime = -10f;
    private bool _initialized;

    private int _pendingPreviewRow = -1;
    // 마우스로 슬라이더를 끄는 동안 바뀐 줄 번호다. 끄는 중에는 소리를 내지 않고, 버튼을 떼는 순간 이 줄의 미리듣기를 한 번 낸다.
    // (-1이면 대기 중인 미리듣기 없음)

    private Tween _fadeTween;
    private Tween _scaleTween;
    private Tween[] _highlightTweens;

    [ReadOnly, ShowInInspector, LabelText("전체")]
    private float MasterValue => SoundSettings.Get(SoundCategory.Master);

    [ReadOnly, ShowInInspector, LabelText("배경음악")]
    private float BgmValue => SoundSettings.Get(SoundCategory.BGM);

    [ReadOnly, ShowInInspector, LabelText("효과음")]
    private float SfxValue => SoundSettings.Get(SoundCategory.SFX);

    [ReadOnly, ShowInInspector, LabelText("UI")]
    private float UiValue => SoundSettings.Get(SoundCategory.UI);

    private void Awake()
    {
        Initialize();
    }

    // Open이 Awake보다 먼저 불려도 안전하도록 한 번만 초기화한다.
    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var uiMap = inputActionAsset.FindActionMap("UI", throwIfNotFound: true);
        _cancelAction = uiMap.FindAction("Cancel", throwIfNotFound: true);
        _navigateAction = uiMap.FindAction("Navigate", throwIfNotFound: true);

        _highlightTweens = new Tween[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            int index = i; // 람다가 반복 변수 대신 이 줄 번호를 기억하게 한다.
            rows[i].slider.onValueChanged.AddListener(v => OnSliderChanged(index, v));
            SetHighlightAlpha(i, 0f);
        }

        resetButton.onClick.AddListener(ResetDefaults);
        closeButton.onClick.AddListener(Close);

        IsOpen = false;
        SetVisible(false);
    }

    private void OnEnable()
    {
        Initialize();
        _cancelAction.Enable();
        _navigateAction.Enable();
        _cancelAction.performed += OnCancelPerformed;
    }

    private void OnDisable()
    {
        // 액션 자체는 GuideBookUI 등과 공유하므로 Disable하지 않고 구독만 해제한다.
        _cancelAction.performed -= OnCancelPerformed;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _scaleTween?.Kill();
        if (_highlightTweens != null)
        {
            foreach (Tween t in _highlightTweens) t?.Kill();
        }
        IsOpen = false;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 마우스로 슬라이더를 클릭하면 EventSystem이 슬라이더를 선택해 방향키를 자체 처리하므로,
        // 우리 코드와 이중으로 값이 바뀌지 않도록 선택을 해제한다. (드래그는 선택 없이도 동작한다)
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<Slider>() != null)
            es.SetSelectedGameObject(null);

        // 마우스 드래그로 바꾼 값의 미리듣기는 마우스 버튼을 뗀 순간에 한 번만 낸다.
        if (_pendingPreviewRow >= 0 && !IsMouseHeld())
        {
            int row = _pendingPreviewRow;
            _pendingPreviewRow = -1;
            _lastPreviewTime = -10f; // 뗀 순간의 소리는 간격 제한에 걸리지 않게 한다.
            PlayPreview(rows[row].category);
        }

        // Time.timeScale이 0이어도 입력은 계속 읽어야 하므로 타이머 없이 입력값 변화만 본다.
        Vector2 nav = _navigateAction.ReadValue<Vector2>();
        int dirX = nav.x > inputThreshold ? 1 : (nav.x < -inputThreshold ? -1 : 0);
        int dirY = nav.y > inputThreshold ? 1 : (nav.y < -inputThreshold ? -1 : 0);

        // 중립에서 임계값을 넘는 순간에만 한 번 반응한다.
        if (dirY != 0 && dirY != _prevDirY) MoveSelection(dirY > 0 ? -1 : 1);
        if (dirX != 0 && dirX != _prevDirX) AdjustSelected(dirX);

        _prevDirX = dirX;
        _prevDirY = dirY;
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        if (IsOpen) Close();
    }

    // 설정 화면을 연다. onClosed는 닫힌 뒤 한 번 호출된다. (필요 없으면 생략)
    public void Open(Action onClosed = null)
    {
        Initialize();
        if (IsOpen) return;

        _onClosed = onClosed;
        IsOpen = true;

        // 열 때 눌려 있던 방향키가 곧바로 반응하지 않도록 현재 상태를 기준으로 삼는다.
        Vector2 nav = _navigateAction.ReadValue<Vector2>();
        _prevDirX = nav.x > inputThreshold ? 1 : (nav.x < -inputThreshold ? -1 : 0);
        _prevDirY = nav.y > inputThreshold ? 1 : (nav.y < -inputThreshold ? -1 : 0);

        RefreshAll();
        SelectRow(0);
        SetVisible(true);

        _fadeTween?.Kill();
        panelGroup.alpha = 0f;
        _fadeTween = panelGroup.DOFade(1f, openDuration).SetUpdate(true);

        _scaleTween?.Kill();
        panelRoot.localScale = Vector3.one * openStartScale;
        _scaleTween = panelRoot.DOScale(Vector3.one, openDuration).SetEase(openEase).SetUpdate(true);
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        LastClosedFrame = Time.frameCount;
        _pendingPreviewRow = -1;

        // 닫히는 연출 중에 클릭이 뒤쪽 버튼으로 새거나 패널이 눌리지 않도록 즉시 상호작용만 막는다.
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        _fadeTween?.Kill();
        _fadeTween = panelGroup.DOFade(0f, closeDuration).SetUpdate(true);

        _scaleTween?.Kill();
        _scaleTween = panelRoot
            .DOScale(Vector3.one * openStartScale, closeDuration)
            .SetEase(closeEase)
            .SetUpdate(true)
            .OnComplete(() => SetVisible(false));

        Action callback = _onClosed;
        _onClosed = null;
        callback?.Invoke();
    }

    // 모든 줄의 슬라이더/글자를 SoundSettings의 현재 값으로 맞춘다. (이벤트 없이)
    private void RefreshAll()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            float v = SoundSettings.Get(rows[i].category);
            rows[i].slider.SetValueWithoutNotify(v);
            rows[i].valueText.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }

    private void OnSliderChanged(int index, float value)
    {
        SoundSettings.Set(rows[index].category, value);
        rows[index].valueText.text = Mathf.RoundToInt(value * 100f) + "%";

        // 마우스로 끄는 중이면 칸마다 소리가 나지 않게 미뤄 두고, 뗄 때 Update에서 한 번 낸다.
        // 방향키(←/→)로 바꿀 때는 마우스가 눌려 있지 않으므로 지금처럼 바로 소리가 난다.
        if (IsMouseHeld())
        {
            _pendingPreviewRow = index;
            return;
        }
        PlayPreview(rows[index].category);
    }

    // New Input System으로 마우스 왼쪽 버튼이 눌려 있는지 확인한다. 마우스가 없으면 눌리지 않은 것으로 본다.
    private static bool IsMouseHeld()
    {
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    // 위/아래 키: 선택 줄을 바꾼다. 양 끝에서는 멈춘다.
    private void MoveSelection(int delta)
    {
        int next = Mathf.Clamp(_selectedIndex + delta, 0, rows.Length - 1);
        if (next != _selectedIndex) SelectRow(next);
    }

    // 좌/우 키: 선택된 줄의 값을 step만큼 바꾼다. 슬라이더를 거치므로 저장/미리듣기도 함께 처리된다.
    private void AdjustSelected(int direction)
    {
        Slider slider = rows[_selectedIndex].slider;
        // 부동소수점 오차가 쌓이지 않도록 1% 단위로 반올림한다.
        slider.value = Mathf.Clamp01(Mathf.Round((slider.value + direction * step) * 100f) / 100f);
    }

    private void SelectRow(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < rows.Length; i++)
            FadeHighlight(i, i == index ? highlightAlpha : 0f);
    }

    private void FadeHighlight(int index, float targetAlpha)
    {
        _highlightTweens[index]?.Kill();
        _highlightTweens[index] = rows[index].highlight
            .DOFade(targetAlpha, highlightDuration)
            .SetUpdate(true);
    }

    private void SetHighlightAlpha(int index, float alpha)
    {
        Color c = rows[index].highlight.color;
        c.a = alpha;
        rows[index].highlight.color = c;
    }

    private void ResetDefaults()
    {
        SoundSettings.ResetDefaults();
        RefreshAll();
    }

    // 효과음/UI 값을 바꿀 때만 해당 그룹으로 짧은 소리를 들려줘 크기를 가늠하게 한다.
    private void PlayPreview(SoundCategory category)
    {
        if (previewSource == null) return;
        AudioClip clip = category == SoundCategory.SFX ? sfxPreviewClip
                       : category == SoundCategory.UI ? uiPreviewClip
                       : null;
        if (clip == null) return;
        if (Time.unscaledTime - _lastPreviewTime < previewInterval) return;

        _lastPreviewTime = Time.unscaledTime;
        previewSource.outputAudioMixerGroup = SoundSettings.MixerGroup(category);
        previewSource.PlayOneShot(clip);
    }

    private void SetVisible(bool visible)
    {
        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }

    [Button("기본값으로")]
    private void TestResetDefaults() => ResetDefaults();

    [Button("열기 테스트")]
    private void TestOpen() => Open();
}
