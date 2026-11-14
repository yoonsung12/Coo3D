using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// F1로 열고 닫는 책 모양 가이드북(도시 생존 가이드북) UI다.
// 내용은 GuideBookData 에셋에서 읽고, 이 클래스는 열기/닫기·페이지 넘김·입력만 담당한다.
public class GuideBookUI : MonoBehaviour
{
    // PauseMenuUI가 같은 Esc 입력을 받을 때 가이드북 상태를 확인할 수 있도록 정적으로 노출한다.
    public static bool IsOpen { get; private set; }

    // 가이드북이 Esc로 닫힌 프레임 번호다.
    // PauseMenuUI와 가이드북은 같은 Cancel 액션을 구독하므로, 구독 순서에 따라 가이드북이 먼저 닫힌 뒤
    // 같은 프레임에 파우즈가 열리는 일이 생길 수 있다. 그 프레임을 비교해서 막기 위해 기록한다.
    public static int LastClosedFrame { get; private set; } = -1;

    [Title("데이터 / 입력 연결")]
    [SerializeField, LabelText("가이드북 데이터")]
    private GuideBookData data;
    // Assets/Data/UI/GuideBook 에셋을 연결한다.

    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Assets/InputSystem_Actions 에셋을 연결한다. UI 맵의 Guide(F1)/Cancel(Esc)/Navigate(방향키)를 사용한다.

    [SerializeField, LabelText("무기 휠 UI")]
    private WeaponWheelUI weaponWheelUI;
    // 휠이 열려 있을 때는 가이드북을 열지 않기 위해 상태 확인용으로만 참조한다.

    [Title("화면 연결")]
    [SerializeField, LabelText("책 그룹 (CanvasGroup)")]
    private CanvasGroup bookGroup;
    // 패널 루트를 SetActive로 끄지 않고 CanvasGroup으로 보이기/막기를 제어한다. (PauseMenuUI와 같은 이유)

    [SerializeField, LabelText("책 본체")]
    private RectTransform bookRoot;

    [SerializeField, LabelText("쪽 컨테이너 (넘김 연출용)")]
    private RectTransform pagesRoot;

    [SerializeField, LabelText("왼쪽 쪽")]
    private GuidePageView leftPage;

    [SerializeField, LabelText("오른쪽 쪽")]
    private GuidePageView rightPage;

    [SerializeField, LabelText("왼쪽 쪽 번호")]
    private Text leftPageNumber;

    [SerializeField, LabelText("오른쪽 쪽 번호")]
    private Text rightPageNumber;

    [SerializeField, LabelText("이전(◀) 화살표")]
    private GameObject prevArrow;

    [SerializeField, LabelText("다음(▶) 화살표")]
    private GameObject nextArrow;

    [SerializeField, LabelText("힌트 바 글자")]
    private Text hintText;

    [SerializeField, LabelText("우측 상단 F1 힌트")]
    private GameObject hudHint;
    // 책이 닫혀 있는 동안에만 보인다.

    [SerializeField, LabelText("탭 템플릿")]
    private RectTransform tabTemplate;
    // 데이터의 탭 개수만큼 복제해서 쓰는 견본이다. 자기 자신은 항상 꺼 둔다.

    [Title("탭 모양")]
    [SerializeField, LabelText("탭 시작 X")]
    private float tabStartX = -635f;

    [SerializeField, LabelText("탭 간격")]
    private float tabSpacing = 164f;

    [SerializeField, LabelText("선택 탭 높이")]
    private float activeTabHeight = 78f;

    [SerializeField, LabelText("비선택 탭 높이")]
    private float inactiveTabHeight = 62f;

    [SerializeField, LabelText("비선택 탭 밝기")]
    private float inactiveTabBrightness = 0.72f;
    // 값이 작을수록 선택되지 않은 탭이 더 어둡게 보인다.

    [Title("힌트 문구")]
    [SerializeField, LabelText("일반 힌트")]
    private string normalHint = "←/→  페이지 넘기기      ↑/↓  탭 바꾸기      F1 · Esc  닫기";

    [SerializeField, LabelText("첫 화면 힌트")]
    private string welcomeHint = "→  다음 장      F1 · Esc  닫고 시작하기";

    [Title("입력 설정")]
    [SerializeField, LabelText("입력 임계값")]
    private float inputThreshold = 0.5f;
    // 방향키가 이 값을 넘는 순간 한 번만 반응한다. (WeaponWheelUI와 같은 방식)

    [SerializeField, LabelText("자동 열기 지연(초)")]
    private float autoOpenDelay = 0.5f;

    [Title("DOTween 연출 설정")]
    [SerializeField, LabelText("열림 시간")]
    private float openDuration = 0.25f;

    [SerializeField, LabelText("열림 Ease")]
    private Ease openEase = Ease.OutBack;

    [SerializeField, LabelText("열림 시작 크기")]
    private float openStartScale = 0.92f;

    [SerializeField, LabelText("닫힘 시간")]
    private float closeDuration = 0.15f;

    [SerializeField, LabelText("닫힘 Ease")]
    private Ease closeEase = Ease.InQuad;

    [SerializeField, LabelText("닫힘 끝 크기")]
    private float closeEndScale = 0.95f;

    [SerializeField, LabelText("쪽 넘김 절반 시간")]
    private float flipHalfDuration = 0.1f;

    [SerializeField, LabelText("탭 펀치 시간")]
    private float tabPunchDuration = 0.25f;

    [SerializeField, LabelText("탭 펀치 세기")]
    private float tabPunchStrength = 0.12f;

    [Title("사운드")]
    [SerializeField, LabelText("오디오 소스 (2D)")]
    private AudioSource audioSource;
    // 패널에 붙인 2D AudioSource를 연결한다. 비워 두면 소리 없이 동작한다.
    // AudioSource는 Time.timeScale의 영향을 받지 않아, 게임이 멈춘 가이드북 안에서도 소리가 난다.

    [SerializeField, LabelText("펼침 소리")]
    private AudioClip openClip;
    // Assets/Sound/UI/GuideBook/Book_Open.wav를 연결한다.

    [SerializeField, LabelText("덮음 소리")]
    private AudioClip closeClip;
    // Assets/Sound/UI/GuideBook/Book_Close.wav를 연결한다.

    [SerializeField, LabelText("넘김 소리")]
    private AudioClip flipClip;
    // Assets/Sound/UI/GuideBook/Book_PageFlip.wav를 연결한다. 장 넘김과 탭 이동 둘 다에 쓴다.

    [SerializeField, LabelText("펼침/덮음 볼륨"), Range(0f, 1f)]
    private float openCloseVolume = 0.9f;

    [SerializeField, LabelText("넘김 볼륨"), Range(0f, 1f)]
    private float flipVolume = 0.8f;
    // 넘김은 자주 나는 소리라 펼침/덮음보다 조금 작게 둔다.

    [SerializeField, LabelText("넘김 피치 범위")]
    private Vector2 flipPitchRange = new Vector2(0.95f, 1.05f);
    // 넘길 때마다 이 범위에서 피치를 살짝 바꿔, 연달아 넘겨도 같은 소리가 기계적으로 반복되는 느낌을 줄인다.

    private const string SeenKey = "Coo3D_GuideBookSeen";

    // 펼침 하나(왼쪽+오른쪽)와 그 펼침이 속한 탭 번호(-1이면 첫 화면)를 묶은 평탄화 목록이다.
    private struct SpreadEntry
    {
        public GuidePage left;
        public GuidePage right;
        public int tabIndex;
    }

    private readonly List<SpreadEntry> _spreads = new List<SpreadEntry>();
    private readonly List<int> _tabFirstSpread = new List<int>();
    private readonly List<Image> _tabImages = new List<Image>();
    private readonly List<Text> _tabTexts = new List<Text>();
    private readonly List<RectTransform> _tabRects = new List<RectTransform>();

    private InputAction _guideAction;
    private InputAction _cancelAction;
    private InputAction _navigateAction;
    private InputActionMap _playerActionMap;
    // 가이드북이 열린 동안 이동/공격 입력이 게임으로 전달되지 않도록 Player 맵을 통째로 끄기 위해 캐시한다.

    private int _spreadIndex;
    private int _prevDirX;
    private int _prevDirY;
    // 방향키를 누르고 있는 동안 연속 반응하지 않도록 직전 입력 방향을 기억한다.

    private Tween _fadeTween;
    private Tween _scaleTween;
    private Tween _flipTween;
    private Tween _tabPunchTween;
    private Coroutine _autoOpenRoutine;

    [ReadOnly, ShowInInspector, LabelText("현재 펼침 번호")]
    private int CurrentSpreadIndex => _spreadIndex;

    [ReadOnly, ShowInInspector, LabelText("열려 있음")]
    private bool IsOpenView => IsOpen;

    private void Awake()
    {
        var uiMap = inputActionAsset.FindActionMap("UI", throwIfNotFound: true);
        _guideAction = uiMap.FindAction("Guide", throwIfNotFound: true);
        _cancelAction = uiMap.FindAction("Cancel", throwIfNotFound: true);
        _navigateAction = uiMap.FindAction("Navigate", throwIfNotFound: true);
        _playerActionMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);

        BuildSpreadList();
        BuildTabs();

        IsOpen = false;
        SetVisible(false);
        hudHint.SetActive(true);
    }

    private void Start()
    {
        // 처음 플레이하는 경우에만 첫 화면을 자동으로 펼친다.
        if (PlayerPrefs.GetInt(SeenKey, 0) == 0)
            _autoOpenRoutine = StartCoroutine(AutoOpenRoutine());
    }

    private void OnEnable()
    {
        _guideAction.Enable();
        _cancelAction.Enable();
        _navigateAction.Enable();
        _guideAction.performed += OnGuidePerformed;
        _cancelAction.performed += OnCancelPerformed;
    }

    private void OnDisable()
    {
        _guideAction.performed -= OnGuidePerformed;
        _cancelAction.performed -= OnCancelPerformed;
        _guideAction.Disable();
        _cancelAction.Disable();
        _navigateAction.Disable();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _scaleTween?.Kill();
        _flipTween?.Kill();
        _tabPunchTween?.Kill();
        IsOpen = false;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Time.timeScale이 0이어도 입력은 계속 읽어야 하므로 타이머 없이 입력값 변화만 본다.
        Vector2 nav = _navigateAction.ReadValue<Vector2>();
        int dirX = nav.x > inputThreshold ? 1 : (nav.x < -inputThreshold ? -1 : 0);
        int dirY = nav.y > inputThreshold ? 1 : (nav.y < -inputThreshold ? -1 : 0);

        // 중립에서 임계값을 넘는 순간에만 한 번 반응한다.
        if (dirX != 0 && dirX != _prevDirX) MoveSpread(dirX);
        if (dirY != 0 && dirY != _prevDirY) MoveTab(dirY > 0 ? -1 : 1);

        _prevDirX = dirX;
        _prevDirY = dirY;
    }

    private void OnGuidePerformed(InputAction.CallbackContext ctx)
    {
        if (IsOpen) CloseBook();
        else OpenBook(false);
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        if (IsOpen) CloseBook();
    }

    private IEnumerator AutoOpenRoutine()
    {
        // 씬이 막 시작한 직후 바로 뜨면 정신없으므로 잠시 기다린다. (timeScale 영향을 받지 않도록 Realtime)
        yield return new WaitForSecondsRealtime(autoOpenDelay);
        _autoOpenRoutine = null;
        OpenBook(true);
    }

    // 데이터를 [첫 화면, 탭0의 펼침들, 탭1의 펼침들 ...] 순서의 한 줄 목록으로 편다.
    private void BuildSpreadList()
    {
        _spreads.Clear();
        _tabFirstSpread.Clear();

        _spreads.Add(new SpreadEntry { left = data.welcomeLeft, right = data.welcomeRight, tabIndex = -1 });

        for (int t = 0; t < data.tabs.Count; t++)
        {
            _tabFirstSpread.Add(_spreads.Count);
            GuideTab tab = data.tabs[t];
            for (int s = 0; s < tab.spreads.Count; s++)
                _spreads.Add(new SpreadEntry { left = tab.spreads[s].left, right = tab.spreads[s].right, tabIndex = t });
        }
    }

    // 탭 템플릿을 복제해서 데이터의 탭 개수만큼 만든다.
    private void BuildTabs()
    {
        tabTemplate.gameObject.SetActive(false);
        for (int t = 0; t < data.tabs.Count; t++)
        {
            RectTransform rect = Instantiate(tabTemplate, tabTemplate.parent);
            rect.gameObject.SetActive(true);
            rect.name = "Tab_" + data.tabs[t].tabName;
            rect.anchoredPosition = new Vector2(tabStartX + t * tabSpacing, rect.anchoredPosition.y);

            Text label = rect.GetComponentInChildren<Text>();
            label.text = data.tabs[t].tabName;

            _tabRects.Add(rect);
            _tabImages.Add(rect.GetComponent<Image>());
            _tabTexts.Add(label);
        }
    }

    private void OpenBook(bool fromAuto)
    {
        if (IsOpen) return;
        // 파우즈나 무기 휠이 시간을 제어 중이면 겹치지 않도록 열지 않는다.
        if (PauseMenuUI.IsPaused) return;
        if (weaponWheelUI != null && weaponWheelUI.IsOpen) return;

        if (_autoOpenRoutine != null)
        {
            StopCoroutine(_autoOpenRoutine);
            _autoOpenRoutine = null;
        }

        // 자동으로 열린 경우뿐 아니라 직접 F1로 열어도 '본 것'으로 처리해 다음엔 자동으로 안 뜬다.
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

        // 처음 열 때는 첫 화면부터, F1로 다시 열 때는 마지막으로 보던 곳에서 이어서 본다.
        if (fromAuto) _spreadIndex = 0;

        IsOpen = true;
        Time.timeScale = 0f;
        _playerActionMap.Disable();
        // Player 맵을 꺼서 열려 있는 동안 키 입력이 이동/공격으로 새지 않게 한다.

        // 열 때 눌려 있던 방향키가 곧바로 반응하지 않도록 현재 상태를 기준으로 삼는다.
        Vector2 nav = _navigateAction.ReadValue<Vector2>();
        _prevDirX = nav.x > inputThreshold ? 1 : (nav.x < -inputThreshold ? -1 : 0);
        _prevDirY = nav.y > inputThreshold ? 1 : (nav.y < -inputThreshold ? -1 : 0);

        RefreshContent();
        hudHint.SetActive(false);
        SetVisible(true);
        PlaySound(openClip, openCloseVolume, 1f);

        _fadeTween?.Kill();
        bookGroup.alpha = 0f;
        _fadeTween = bookGroup.DOFade(1f, openDuration).SetUpdate(true);

        _scaleTween?.Kill();
        bookRoot.localScale = Vector3.one * openStartScale;
        _scaleTween = bookRoot.DOScale(Vector3.one, openDuration).SetEase(openEase).SetUpdate(true);
    }

    private void CloseBook()
    {
        if (!IsOpen) return;

        IsOpen = false;
        LastClosedFrame = Time.frameCount;
        // 히트스톱 등으로 timeScale이 0.05 같은 값일 수 있어서 저장해 둔 값이 아니라 1로 되돌린다.
        Time.timeScale = 1f;
        _playerActionMap.Enable();
        hudHint.SetActive(true);
        PlaySound(closeClip, openCloseVolume, 1f);

        _flipTween?.Kill();
        pagesRoot.localScale = Vector3.one;

        _fadeTween?.Kill();
        _fadeTween = bookGroup.DOFade(0f, closeDuration).SetUpdate(true);

        _scaleTween?.Kill();
        _scaleTween = bookRoot
            .DOScale(Vector3.one * closeEndScale, closeDuration)
            .SetEase(closeEase)
            .SetUpdate(true)
            .OnComplete(() => SetVisible(false));
    }

    // 좌우 방향키: 펼침을 한 칸 이동한다. 양 끝에서는 멈춘다.
    private void MoveSpread(int delta)
    {
        int next = Mathf.Clamp(_spreadIndex + delta, 0, _spreads.Count - 1);
        if (next == _spreadIndex) return;

        int prevTab = _spreads[_spreadIndex].tabIndex;
        _spreadIndex = next;
        PlayFlip();
        PlayFlipSound();
        if (_spreads[_spreadIndex].tabIndex != prevTab) PunchActiveTab();
    }

    // 위/아래 방향키: 이전/다음 탭의 첫 펼침으로 점프한다. 첫 화면에서 아래는 0번 탭이다.
    private void MoveTab(int delta)
    {
        if (_tabFirstSpread.Count == 0) return;

        int currentTab = _spreads[_spreadIndex].tabIndex;
        int targetTab;
        if (currentTab < 0) targetTab = delta > 0 ? 0 : -1;
        else targetTab = currentTab + delta;

        if (targetTab < 0 || targetTab >= _tabFirstSpread.Count) return;

        _spreadIndex = _tabFirstSpread[targetTab];
        PlayFlip();
        PlayFlipSound();
        PunchActiveTab();
    }

    // 쪽을 가로로 접었다 펴면서 중간에 내용을 바꿔 책장이 넘어가는 느낌을 준다.
    private void PlayFlip()
    {
        _flipTween?.Kill();
        // 중간에 끊겨도 RefreshContent는 항상 최신 _spreadIndex를 읽으므로 내용이 어긋나지 않는다.
        _flipTween = DOTween.Sequence()
            .Append(pagesRoot.DOScaleX(0f, flipHalfDuration).SetEase(Ease.InQuad))
            .AppendCallback(RefreshContent)
            .Append(pagesRoot.DOScaleX(1f, flipHalfDuration).SetEase(Ease.OutQuad))
            .SetUpdate(true);
        RefreshTabs();
        RefreshArrows();
    }

    private void PunchActiveTab()
    {
        int tab = _spreads[_spreadIndex].tabIndex;
        if (tab < 0) return;

        _tabPunchTween?.Kill();
        _tabRects[tab].localScale = Vector3.one;
        _tabPunchTween = _tabRects[tab]
            .DOPunchScale(Vector3.one * tabPunchStrength, tabPunchDuration)
            .SetUpdate(true);
    }

    private void RefreshContent()
    {
        SpreadEntry spread = _spreads[_spreadIndex];
        leftPage.Show(spread.left);
        rightPage.Show(spread.right);
        RefreshPageNumbers();
        RefreshTabs();
        RefreshArrows();
        hintText.text = spread.tabIndex < 0 ? welcomeHint : normalHint;
    }

    // 첫 화면은 "- 0 -" / "- i -", 이후는 왼쪽 1+2k, 오른쪽 2+2k (k는 첫 화면 다음부터 센 펼침 번호)다.
    private void RefreshPageNumbers()
    {
        if (_spreadIndex == 0)
        {
            leftPageNumber.text = "- 0 -";
            rightPageNumber.text = "- i -";
            return;
        }

        int k = _spreadIndex - 1;
        leftPageNumber.text = "- " + (1 + 2 * k) + " -";
        rightPageNumber.text = "- " + (2 + 2 * k) + " -";
    }

    private void RefreshArrows()
    {
        prevArrow.SetActive(_spreadIndex > 0);
        nextArrow.SetActive(_spreadIndex < _spreads.Count - 1);
    }

    // 선택된 탭은 높고 밝게, 나머지는 낮고 어둡게 보여준다. 첫 화면에서는 선택된 탭이 없다.
    private void RefreshTabs()
    {
        int active = _spreads[_spreadIndex].tabIndex;
        for (int t = 0; t < _tabRects.Count; t++)
        {
            bool isActive = t == active;
            Color baseColor = data.tabs[t].tabColor;
            Color shown = isActive ? baseColor : baseColor * inactiveTabBrightness;
            shown.a = 1f;
            _tabImages[t].color = shown;

            Vector2 size = _tabRects[t].sizeDelta;
            size.y = isActive ? activeTabHeight : inactiveTabHeight;
            _tabRects[t].sizeDelta = size;
            _tabTexts[t].fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private void SetVisible(bool visible)
    {
        bookGroup.alpha = visible ? 1f : 0f;
        bookGroup.interactable = visible;
        bookGroup.blocksRaycasts = visible;
    }

    // 실제로 장이 넘어갔을 때만 호출된다(양 끝에서 더 못 넘기면 소리도 나지 않는다).
    private void PlayFlipSound()
    {
        PlaySound(flipClip, flipVolume, Random.Range(flipPitchRange.x, flipPitchRange.y));
    }

    // 세 소리가 같은 AudioSource를 쓰므로 피치를 매번 지정해 직전 넘김 피치가 남지 않게 한다.
    private void PlaySound(AudioClip clip, float volume, float pitch)
    {
        if (audioSource == null || clip == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    [Button("펼침 소리 테스트")]
    private void TestOpenSound() => PlaySound(openClip, openCloseVolume, 1f);

    [Button("덮음 소리 테스트")]
    private void TestCloseSound() => PlaySound(closeClip, openCloseVolume, 1f);

    [Button("넘김 소리 테스트")]
    private void TestFlipSound() => PlayFlipSound();

    [Button("열기 테스트")]
    private void TestOpen() => OpenBook(false);

    [Button("다음 장")]
    private void TestNext() => MoveSpread(1);

    [Button("본 기록 초기화")]
    private void ResetSeenFlag()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
    }
}
