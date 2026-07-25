# 타이틀 화면 + 세이브 슬롯 선택 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임 실행 시 처음 뜨는 `Title.unity`에 "처음부터"/"이어하기"/"종료" 버튼을 두고, 두 버튼 모두 3개 세이브 슬롯 중 하나를 고르는 동일한 슬롯 선택 화면으로 이어지게 한다. 슬롯 선택 화면에서 세이브 삭제도 할 수 있다.

**Architecture:** 기존 `SaveManager`의 슬롯 API(`HasSave`/`NewGame`/`LoadGame`/`DeleteSlot`)를 그대로 재사용하고, 저장 시각을 부작용 없이 조회하는 `PeekSlotData` 메서드 1개만 추가한다. 새 UI 스크립트 4개(`TitleMenuController`, `SaveSlotSelectController`, `SaveSlotUIItem`, `ConfirmPopupUI`)가 씬 전환과 슬롯 클릭 로직을 담당하고, `SaveManager`를 프리팹으로 추출해 `Title.unity`/`SampleScene` 양쪽에 배치함으로써(기존 `Awake()`의 중복 방지 로직 재사용) 코드 변경 없이 두 씬 모두에서 싱글턴이 살아있게 만든다.

**Tech Stack:** Unity 6 (6000.0.68f1), C#, DOTween Pro, Odin Inspector, Easy Save 3(ES3), New Input System(`InputSystemUIInputModule`).

## Global Constraints

- 이 프로젝트에는 자동화 테스트 프레임워크가 구성되어 있지 않다. 각 태스크의 검증은 "구현 → Unity Editor Play Mode에서 수동 검증" 사이클로 진행한다. (기존 `docs/superpowers/plans/2026-07-17-spring-flowerbud-puzzle.md`와 동일한 컨벤션)
- DOTween을 쓰는 곳은 트윈을 변수에 저장하고 오브젝트 파괴 시 `Kill()`한다.
- Odin `[SerializeField]`에는 한글 `[LabelText]`를 붙이고, 새 코드의 주요 필드/메서드에는 한국어 주석(왜 필요한지 위주)을 단다. 네임스페이스는 쓰지 않는다(기존 `Assets/Scripts/UI/*.cs`와 동일).
- 기존 UI 스크립트(`CoinUI`, `PlayerHealthUI`, `SeasonGaugeUI`, `WeaponWheelUI`) 전부 TextMeshPro가 아닌 `UnityEngine.UI.Text`를 쓰고 있으므로, 새 UI도 동일하게 `UnityEngine.UI.Text`를 사용한다.
- `ProjectSettings/ProjectSettings.asset`의 `activeInputHandler: 1`(New Input System 전용)이 확인됐다. `Button.onClick`이 동작하려면 EventSystem에 legacy `StandaloneInputModule`이 아니라 `InputSystemUIInputModule`이 붙어 있어야 한다. `SampleScene`의 기존 `EventSystem` 오브젝트(`Assets/InputSystem_Actions.inputactions`를 참조하는 `InputSystemUIInputModule` 사용 중)가 이미 이 구성으로 되어 있으므로, `Title.unity`에도 동일하게 구성한다.
- `SaveManager.cs`의 기존 메서드(`HasSave`, `NewGame`, `LoadGame`, `SaveToSlot`, `DeleteSlot`)는 시그니처/동작을 변경하지 않는다. `PeekSlotData` 메서드 1개만 추가한다.
- `SampleScene`의 기존 게임플레이 스크립트(Player, Enemy, Season, Puzzle 등)는 건드리지 않는다.
- 설계 문서: `docs/superpowers/specs/2026-07-25-title-screen-save-slot-design.md`

---

## 파일 구조

- **Modify:** `Assets/Scripts/Managers/SaveManager.cs` — `PeekSlotData(int slot)` 메서드 추가
- **Create:** `Assets/Scripts/UI/ConfirmPopupUI.cs` — 예/아니오 확인 팝업 (덮어쓰기/삭제 공용)
- **Create:** `Assets/Scripts/UI/SaveSlotUIItem.cs` — 슬롯 한 칸 UI (번호/상태/삭제 버튼)
- **Create:** `Assets/Scripts/UI/SaveSlotSelectController.cs` — 슬롯 3개 목록 + 모드별(새게임/이어하기) 클릭 분기
- **Create:** `Assets/Scripts/UI/TitleMenuController.cs` — 타이틀 루트 메뉴(처음부터/이어하기/종료)
- **Create:** `Assets/Prefabs/SaveManager.prefab` — 기존 `SaveManager` GameObject를 프리팹화
- **Modify:** `Assets/Scenes/SampleScene.unity` — 기존 `SaveManager` 오브젝트를 프리팹 인스턴스로 교체
- **Create:** `Assets/Scenes/Title.unity` — 신규 타이틀 씬
- **Modify:** Build Settings (`Title`=index 0, `SampleScene`=index 1)

---

### Task 1: `SaveManager.cs`에 `PeekSlotData` 조회 메서드 추가

**Files:**
- Modify: `Assets/Scripts/Managers/SaveManager.cs`

**Interfaces:**
- Consumes: 기존 `private string GetFilePath(int slot)`, 기존 `private string saveKey` 필드
- Produces (Task 4가 사용): `public SaveData PeekSlotData(int slot)` — 슬롯에 세이브가 없으면 `null`, 있으면 해당 `SaveData`를 부작용 없이(씬 전환/체크포인트 변경 없이) 반환한다.

- [ ] **Step 1: `Assets/Scripts/Managers/SaveManager.cs`의 `HasSave` 메서드 바로 뒤에 아래 메서드를 추가한다.**

`public bool HasSave(int slot) => ES3.FileExists(GetFilePath(slot));` 줄 바로 다음에 빈 줄을 하나 두고 삽입한다.

```csharp
    // 슬롯 데이터를 부작용 없이 조회한다. 슬롯 선택 UI가 저장 시각을 표시할 때 사용하며,
    // LoadGame과 달리 체크포인트를 갱신하거나 씬을 불러오지 않는다.
    public SaveData PeekSlotData(int slot)
    {
        string path = GetFilePath(slot);
        return ES3.FileExists(path) ? ES3.Load<SaveData>(saveKey, path) : null;
    }
```

- [ ] **Step 2: Unity Editor 컴파일 확인**

1. Unity Editor로 돌아가 컴파일이 끝날 때까지 기다린다.
2. Console에 에러가 없는지 확인한다 (MCP를 쓰는 경우 `read_console`로 확인).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Managers/SaveManager.cs
git commit -m "$(cat <<'EOF'
SaveManager에 PeekSlotData 조회 메서드 추가

세이브 슬롯 선택 UI가 저장 시각을 표시할 때 LoadGame처럼 씬 전환/
체크포인트 복원 부작용 없이 데이터만 읽을 수 있도록 조회 전용
메서드 하나를 추가한다. 기존 메서드는 변경하지 않는다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `ConfirmPopupUI.cs` 생성 — 공용 예/아니오 확인 팝업

**Files:**
- Create: `Assets/Scripts/UI/ConfirmPopupUI.cs`

**Interfaces:**
- Consumes: 없음 (독립 컴포넌트)
- Produces (Task 4가 사용): `public void Show(string message, System.Action onConfirm)` — 메시지를 표시하며 팝업을 열고, 확인 버튼을 누르면 `onConfirm`을 실행한다. 취소 버튼을 누르면 아무 동작 없이 닫힌다.

- [ ] **Step 1: `Assets/Scripts/UI/ConfirmPopupUI.cs`를 아래 내용으로 생성한다.**

```csharp
using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 덮어쓰기/삭제처럼 되돌리기 어려운 동작을 실행하기 전에 예/아니오로 한 번 더 확인받는 공용 팝업이다.
// SaveSlotSelectController가 상황에 맞는 메시지와 확인 시 실행할 동작을 넘겨서 재사용한다.
public class ConfirmPopupUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("메시지 텍스트")]
    private Text messageText;

    [SerializeField, LabelText("확인 버튼")]
    private Button yesButton;

    [SerializeField, LabelText("취소 버튼")]
    private Button noButton;

    [Title("등장 연출 설정")]
    [SerializeField, LabelText("등장 시간")]
    private float popInDuration = 0.2f;

    [SerializeField, LabelText("등장 Ease")]
    private Ease popInEase = Ease.OutBack;

    private Tween _popTween;
    private Action _pendingConfirmAction;
    // Show()를 호출할 때마다 새로 채워지며, 확인 버튼을 눌렀을 때 실행할 동작을 잠시 기억해 둔다.

    private void Awake()
    {
        gameObject.SetActive(false);

        yesButton.onClick.AddListener(HandleYesClicked);
        noButton.onClick.AddListener(HandleNoClicked);
    }

    private void OnDestroy()
    {
        _popTween?.Kill();
    }

    // message를 표시하며 팝업을 연다. 확인 버튼을 누르면 onConfirm이 실행된다.
    public void Show(string message, Action onConfirm)
    {
        messageText.text = message;
        _pendingConfirmAction = onConfirm;

        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;

        _popTween?.Kill();
        _popTween = transform.DOScale(Vector3.one, popInDuration).SetEase(popInEase);
    }

    private void HandleYesClicked()
    {
        gameObject.SetActive(false);
        _pendingConfirmAction?.Invoke();
        _pendingConfirmAction = null;
    }

    private void HandleNoClicked()
    {
        gameObject.SetActive(false);
        _pendingConfirmAction = null;
    }
}
```

- [ ] **Step 2: Unity Editor 컴파일 확인**

1. Console에 에러가 없는지 확인한다.
2. 아직 씬에 배치하지 않으므로 이 시점에는 Play Mode 검증을 하지 않는다 (Task 7에서 씬에 배치한 뒤 실제 동작을 확인한다).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/ConfirmPopupUI.cs
git commit -m "$(cat <<'EOF'
ConfirmPopupUI 생성 - 덮어쓰기/삭제 확인용 공용 예아니오 팝업

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `SaveSlotUIItem.cs` 생성 — 슬롯 한 칸 UI

**Files:**
- Create: `Assets/Scripts/UI/SaveSlotUIItem.cs`

**Interfaces:**
- Consumes: 없음 (독립 컴포넌트)
- Produces (Task 4가 사용):
  - `public Button SelectButton` — 슬롯 선택 버튼 참조 (Task 4가 클릭 리스너를 직접 연결한다)
  - `public Button DeleteButton` — 슬롯 삭제 버튼 참조
  - `public void SetState(bool hasSave, string savedAtIso)` — `hasSave`가 true면 저장 시각을, false면 "비어있음"을 표시하고 삭제 버튼을 숨긴다.

- [ ] **Step 1: `Assets/Scripts/UI/SaveSlotUIItem.cs`를 아래 내용으로 생성한다.**

```csharp
using System;
using System.Globalization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 세이브 슬롯 선택 화면에서 슬롯 한 칸(번호, 저장 시각 또는 "비어있음", 삭제 버튼)을 표시한다.
// 클릭 이벤트 처리는 SaveSlotSelectController가 슬롯 번호를 캡처한 람다로 직접 연결한다.
public class SaveSlotUIItem : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("상태 텍스트")]
    private Text stateText;
    // 저장 시각 또는 "비어있음"을 표시한다. 슬롯 번호 라벨("슬롯 1" 등)은 Inspector에서 고정 텍스트로 미리 넣어 둔다.

    [SerializeField, LabelText("선택 버튼")]
    private Button selectButton;

    [SerializeField, LabelText("삭제 버튼")]
    private Button deleteButton;

    public Button SelectButton => selectButton;
    public Button DeleteButton => deleteButton;

    // hasSave가 true면 저장 시각을 표시하고 삭제 버튼도 보여준다.
    // false면 "비어있음"을 표시하고 삭제할 대상이 없으므로 삭제 버튼을 숨긴다.
    public void SetState(bool hasSave, string savedAtIso)
    {
        stateText.text = hasSave ? FormatSavedAt(savedAtIso) : "비어있음";
        deleteButton.gameObject.SetActive(hasSave);
    }

    // SaveData.savedAtIso(ISO 8601, DateTime.UtcNow.ToString("o"))를 "yyyy-MM-dd HH:mm 저장됨" 형태로 바꾼다.
    // 파싱에 실패하면 원본 문자열을 그대로 보여준다.
    private string FormatSavedAt(string savedAtIso)
    {
        if (DateTime.TryParse(savedAtIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var savedAt))
            return $"{savedAt.ToLocalTime():yyyy-MM-dd HH:mm} 저장됨";

        return savedAtIso;
    }
}
```

- [ ] **Step 2: Unity Editor 컴파일 확인**

Console에 에러가 없는지 확인한다.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/SaveSlotUIItem.cs
git commit -m "$(cat <<'EOF'
SaveSlotUIItem 생성 - 세이브 슬롯 한 칸의 상태 표시 UI

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `SaveSlotSelectController.cs` 생성 — 슬롯 목록 + 모드별 클릭 분기

**Files:**
- Create: `Assets/Scripts/UI/SaveSlotSelectController.cs`

**Interfaces:**
- Consumes:
  - Task 1의 `SaveManager.Instance.PeekSlotData(int slot)`, 기존 `SaveManager.Instance.HasSave(int slot)`/`NewGame(int slot)`/`LoadGame(int slot)`/`DeleteSlot(int slot)`
  - Task 2의 `ConfirmPopupUI.Show(string, Action)`
  - Task 3의 `SaveSlotUIItem.SelectButton`/`DeleteButton`/`SetState(bool, string)`
- Produces (Task 5가 사용):
  - `public enum Mode { NewGame, Continue }`
  - `public void Open(Mode mode)` — 슬롯 선택 패널을 열고 목록을 새로고침한다.

- [ ] **Step 1: `Assets/Scripts/UI/SaveSlotSelectController.cs`를 아래 내용으로 생성한다.**

```csharp
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
        panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    // mode에 맞게 슬롯 목록을 새로고침하고 패널을 페이드 인으로 연다.
    public void Open(Mode mode)
    {
        _currentMode = mode;
        RefreshSlots();

        titleRootPanel.gameObject.SetActive(false);

        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        _fadeTween?.Kill();
        _fadeTween = panel.DOFade(1f, fadeDuration);
    }

    private void Close()
    {
        _fadeTween?.Kill();
        _fadeTween = panel.DOFade(0f, fadeDuration)
            .OnComplete(() => panel.gameObject.SetActive(false));

        titleRootPanel.gameObject.SetActive(true);
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

    // 씬을 먼저 옮긴 뒤 NewGame을 호출해야 SaveData.sceneName이 "Title"이 아니라
    // gameplaySceneName으로 정확히 기록된다 (NewGame은 호출 시점의 활성 씬 이름을 그대로 저장하기 때문).
    private void StartNewGame(int slot)
    {
        SceneManager.LoadScene(gameplaySceneName);
        SaveManager.Instance.NewGame(slot);
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
```

- [ ] **Step 2: Unity Editor 컴파일 확인**

Console에 에러가 없는지 확인한다. (`SaveManager`, `SaveData`, `ConfirmPopupUI`, `SaveSlotUIItem` 참조가 모두 존재하므로 이 시점에 정상 컴파일되어야 한다.)

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/SaveSlotSelectController.cs
git commit -m "$(cat <<'EOF'
SaveSlotSelectController 생성 - 새게임/이어하기 공용 슬롯 선택 로직

3개 슬롯 목록을 보여주고, 모드(NewGame/Continue)에 따라 클릭 시
SaveManager.NewGame 또는 LoadGame으로 분기한다. 새게임은 씬을 먼저
옮긴 뒤 NewGame을 호출해 SaveData.sceneName이 정확히 기록되게 한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: `TitleMenuController.cs` 생성 — 타이틀 루트 메뉴

**Files:**
- Create: `Assets/Scripts/UI/TitleMenuController.cs`

**Interfaces:**
- Consumes: Task 4의 `SaveSlotSelectController.Mode`, `SaveSlotSelectController.Open(Mode)`, 기존 `SaveManager.Instance.HasSave(int slot)`
- Produces: 없음 (이 서브프로젝트의 마지막 스크립트 — Task 6/7에서 씬에 배치해서 쓴다)

- [ ] **Step 1: `Assets/Scripts/UI/TitleMenuController.cs`를 아래 내용으로 생성한다.**

```csharp
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
```

- [ ] **Step 2: Unity Editor 컴파일 확인**

Console에 에러가 없는지 확인한다.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/TitleMenuController.cs
git commit -m "$(cat <<'EOF'
TitleMenuController 생성 - 타이틀 루트 메뉴(처음부터/이어하기/종료)

세이브가 하나도 없으면 이어하기 버튼을 비활성화하고, 두 버튼 모두
SaveSlotSelectController.Open으로 슬롯 선택 화면을 연다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: `SaveManager` 프리팹화 + `SampleScene` 반영

**Files:**
- Create: `Assets/Prefabs/SaveManager.prefab`
- Modify: `Assets/Scenes/SampleScene.unity`

**Interfaces:**
- Consumes: 기존 `SampleScene`의 `SaveManager` GameObject(컴포넌트: `SaveManager.cs`, 필드 `saveKey = "saveData"`)
- Produces (Task 7이 사용): `Assets/Prefabs/SaveManager.prefab` — `Title.unity`에도 배치할 프리팹

이 태스크는 코드 변경이 없다. `SaveManager.Awake()`의 기존 중복 방지 로직(`Instance != null && Instance != this`면 자기 파괴) 덕분에, 같은 프리팹을 여러 씬에 둬도 먼저 로드된 씬의 인스턴스만 살아남는다 — 이 로직이 실제로 씬이 2개일 때도 성립하는지 이번 태스크에서 확인한다.

- [ ] **Step 1: `SampleScene`을 연다.**

Unity Editor에서 `Assets/Scenes/SampleScene.unity`를 연다 (MCP를 쓰는 경우 `manage_scene`으로 씬을 로드/확인한다).

- [ ] **Step 2: Hierarchy에서 `SaveManager` GameObject를 찾아 프리팹으로 추출한다.**

1. Hierarchy에서 `SaveManager` GameObject를 선택한다.
2. Project 창의 `Assets/Prefabs/` 폴더로 드래그해 `Assets/Prefabs/SaveManager.prefab`을 생성한다. (Unity가 자동으로 씬의 인스턴스를 프리팹 인스턴스로 연결해 준다 — 기존 Inspector 값인 `saveKey = "saveData"`가 그대로 유지되는지 Inspector에서 확인한다.)
3. MCP를 쓰는 경우 `manage_prefabs`(또는 `manage_asset`)로 동일한 결과(기존 GameObject 기준 프리팹 생성)를 만들 수 있는지 확인하고, 아니라면 위 수동 절차를 안내한다.

- [ ] **Step 3: `SampleScene`을 Play Mode로 검증한다.**

1. Console에 에러가 없는지 확인한다.
2. Play Mode에 들어가 기존 체크포인트 저장/로드가 그대로 동작하는지 확인한다 (예: `Checkpoint_Test`를 통과시켜 저장되는지, `SaveManager.Instance`가 정상적으로 존재하는지).
3. Play Mode를 종료한다.

- [ ] **Step 4: 씬 저장 후 Commit**

```bash
git add Assets/Prefabs/SaveManager.prefab Assets/Prefabs/SaveManager.prefab.meta Assets/Scenes/SampleScene.unity
git commit -m "$(cat <<'EOF'
SaveManager를 프리팹으로 추출 - Title 씬에서도 재사용하기 위함

기존 SaveManager.Awake()의 중복 방지 로직을 그대로 활용해, 이 프리팹을
Title.unity에도 배치하면 코드 변경 없이 어느 씬이 먼저 로드되든
싱글턴이 살아남는다. SampleScene 쪽 동작/필드 값은 변경 없음.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: `Title.unity` 씬 생성, UI 조립, Build Settings 등록, 전체 플로우 검증

**Files:**
- Create: `Assets/Scenes/Title.unity`
- Modify: Build Settings (`Assets/Scenes/Title.unity`=index 0, `Assets/Scenes/SampleScene.unity`=index 1)

**Interfaces:**
- Consumes: Task 1~6에서 만든 모든 스크립트/프리팹
- Produces: 없음 (이 서브프로젝트의 마지막 태스크)

- [ ] **Step 1: `Assets/Scenes/Title.unity` 씬을 새로 만든다.**

1. Unity Editor에서 File → New Scene (Basic 템플릿)으로 새 씬을 만들고 `Assets/Scenes/Title.unity`로 저장한다.
2. Main Camera와 Directional Light는 기본값을 유지한다 (Odin/DOTween 등 이 태스크에서 조명은 중요하지 않지만, 프로젝트 컨벤션상 새 씬에는 카메라와 라이트를 포함한다).

- [ ] **Step 2: EventSystem을 New Input System용으로 구성한다.**

1. Hierarchy에서 GameObject → UI → Event System으로 EventSystem을 추가한다.
2. 추가된 컴포넌트가 `InputSystemUIInputModule`인지 확인한다 (`activeInputHandler: 1`이라 Unity가 기본으로 이 모듈을 붙여줘야 한다). 만약 legacy `Standalone Input Module`이 붙어 있다면 제거하고 `InputSystemUIInputModule`을 추가한다.
3. `InputSystemUIInputModule`의 `Actions Asset`에 `Assets/InputSystem_Actions.inputactions`를 연결한다 (`SampleScene`의 기존 `EventSystem`과 동일한 에셋).

- [ ] **Step 3: `Assets/Prefabs/SaveManager.prefab` 인스턴스를 배치한다.**

Task 6에서 만든 프리팹을 Hierarchy로 드래그해 인스턴스를 추가한다.

- [ ] **Step 4: Canvas와 3개 패널을 만든다.**

1. GameObject → UI → Canvas로 `Canvas`를 만든다. `Canvas Scaler`를 `SampleScene`의 `GameCanvas`와 동일하게 맞춘다: `UI Scale Mode = Scale With Screen Size`, `Reference Resolution = 1920x1080`, `Match Width Or Height = 0.5`.
2. Canvas 아래에 빈 GameObject 3개를 만들고 각각 `CanvasGroup` 컴포넌트를 추가한다:
   - `TitleRootPanel` (기본 활성, alpha 1)
   - `SlotSelectPanel` (기본 비활성 — `SaveSlotSelectController.Awake()`가 알아서 숨기지만, 에디터에서 미리 알아보기 쉽도록 비활성 상태로 둔다)
   - `ConfirmPopupPanel` (기본 비활성 — `ConfirmPopupUI.Awake()`가 `SetActive(false)` 처리하므로 초기 상태는 활성이어도 무방하지만, 에디터 작업 편의를 위해 비활성으로 둔다)
3. `TitleRootPanel` 아래에:
   - 제목 텍스트(`Text`, 예: "COO3D") — 플레이스홀더이므로 단색 배경 위에 큰 폰트 크기로만 둔다.
   - 배경 `Image` (단색 또는 그라디언트 Sprite) — 플레이스홀더.
   - 버튼 3개(`Button` + 자식 `Text`): "처음부터", "이어하기", "종료".
4. `SlotSelectPanel` 아래에:
   - 슬롯 UI 3개 (`SaveSlotUIItem` 컴포넌트를 붙인 GameObject) — 각각 "슬롯 N" 고정 라벨용 `Text`, 상태 `Text`, 선택 `Button`, 삭제 `Button`을 자식으로 두고 `SaveSlotUIItem`의 `상태 텍스트`/`선택 버튼`/`삭제 버튼` 필드에 연결한다.
   - "뒤로가기" `Button`.
5. `ConfirmPopupPanel` 아래에:
   - 메시지 `Text`, "예" `Button`, "아니오" `Button`.

- [ ] **Step 5: 컨트롤러 컴포넌트를 붙이고 Inspector 참조를 연결한다.**

1. `ConfirmPopupPanel`에 `ConfirmPopupUI` 컴포넌트를 붙이고 메시지 텍스트/예/아니오 버튼을 연결한다.
2. `SlotSelectPanel`에 `SaveSlotSelectController` 컴포넌트를 붙이고: `패널`(자기 자신의 `CanvasGroup`), `타이틀 루트 패널`(`TitleRootPanel`의 `CanvasGroup`), `뒤로가기 버튼`, `슬롯 UI 목록`(슬롯 3개를 순서대로), `확인 팝업`(`ConfirmPopupPanel`의 `ConfirmPopupUI`), `게임플레이 씬 이름`(기본값 `SampleScene` 그대로 둔다)을 연결한다.
3. `TitleRootPanel`에 `TitleMenuController` 컴포넌트를 붙이고: 처음부터/이어하기/종료 버튼, `슬롯 선택 컨트롤러`(`SlotSelectPanel`의 `SaveSlotSelectController`), `세이브 슬롯 개수`(3)를 연결한다.

- [ ] **Step 6: Build Settings에 씬을 등록한다.**

1. File → Build Settings를 연다.
2. `Title.unity`를 추가해 index 0으로, `SampleScene.unity`를 index 1로 순서를 맞춘다 (기존에 `SampleScene`만 등록되어 있었다면 그것이 index 1이 되도록 `Title`을 위로 올린다).

- [ ] **Step 7: Play Mode로 전체 플로우를 검증한다.**

1. `Title` 씬에서 Play Mode에 들어간다. Console에 에러가 없는지 확인한다.
2. 세이브 파일이 하나도 없는 상태(필요하면 `Assets/Prefabs/SaveManager.prefab`의 `DeleteSlot`을 슬롯 1~3에 대해 미리 호출하거나, 세이브 파일이 저장되는 폴더에서 `save_slot*.es3` 파일을 지워 초기화)에서 "이어하기" 버튼이 비활성화(회색)인지 확인한다.
3. "처음부터" → 빈 슬롯 선택 → `SampleScene`으로 정상 진입하는지, Console에 에러가 없는지 확인한다.
4. 진입 후 체크포인트를 하나 통과시켜 저장한 뒤, 타이틀로 돌아가(직접 Play Mode를 종료했다가 다시 시작하거나, 별도 "타이틀로" 이동 경로가 없다면 Play Mode를 재시작해 확인) "이어하기"로 같은 슬롯을 선택했을 때 저장했던 위치로 정확히 복원되는지 확인한다.
5. 이미 세이브가 있는 슬롯에 "처음부터"로 다시 진입 시 덮어쓰기 확인 팝업이 뜨는지, 확인을 누르면 실제로 초기화되는지, 취소를 누르면 아무 일도 일어나지 않는지 확인한다.
6. 슬롯의 "삭제" 버튼 → 확인 팝업 → 삭제 후 해당 슬롯이 "비어있음"으로 바뀌고 "이어하기"에서 선택 불가능(버튼 비활성화)해지는지 확인한다.
7. "뒤로가기" 버튼으로 슬롯 선택 화면에서 타이틀 루트로 정상 복귀하는지 확인한다.
8. "종료" 버튼을 눌렀을 때 에디터에서는 Play Mode가 종료되는지 확인한다.
9. `SampleScene`을 직접 열어 Play Mode로 들어가도(타이틀을 거치지 않아도) 기존처럼 정상 작동하는지 확인한다 — `SaveManager` 프리팹이 두 씬 모두에 있어도 오브젝트가 중복 생성되지 않는지 Hierarchy에서 확인한다.
10. 씬 전환/팝업 DOTween 연출이 중복 실행되거나 오브젝트 비활성화 시 에러를 내지 않는지 확인한다.
11. 이 태스크는 여러 씬을 오가는 상호작용 시나리오라 헤드리스 MCP 환경에서 전부 재현하기 어려울 수 있다 — MCP로 확인 가능한 부분(컴파일, 콘솔 에러, 초기 씬 로드, 버튼 Inspector 연결 상태)은 먼저 확인하고, 나머지(전체 플로우 클릭 테스트)는 사용자가 직접 Play Mode에서 최종 확인한다.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scenes/Title.unity Assets/Scenes/Title.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "$(cat <<'EOF'
타이틀 화면 씬 조립 - 처음부터/이어하기/종료 + 슬롯 선택 UI 연결

Title.unity를 Build Settings index 0으로 등록하고, TitleMenuController/
SaveSlotSelectController/SaveSlotUIItem/ConfirmPopupUI를 배치해
처음부터(새게임)/이어하기(로드)/삭제/종료 전체 플로우를 완성한다.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

## 완료 후 회귀 체크리스트

- `SampleScene`을 타이틀 없이 직접 Play해도 기존 플레이어 이동/점프/시즌/퍼즐 기능이 모두 정상 작동
- 기존 체크포인트 저장/로드(`CheckpointTrigger`, `RegionCheckpoint`)가 그대로 작동
- Console에 에러 없음
