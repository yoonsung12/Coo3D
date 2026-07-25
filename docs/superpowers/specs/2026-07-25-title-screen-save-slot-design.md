# 타이틀 화면 + 세이브 슬롯 선택 설계

날짜: 2026-07-25
관련 시스템: `Assets/Scripts/Managers/SaveManager.cs`, `SaveData.cs` (기존 슬롯 기반 세이브 시스템)

## 배경

- 프로젝트에는 현재 `Assets/Scenes/SampleScene.unity` 하나만 존재하고, 게임 실행 시 곧바로 게임플레이 씬으로 들어간다. 타이틀/메인메뉴가 없다.
- `SaveManager`는 이미 슬롯 단위 세이브 API(`HasSave(slot)`, `NewGame(slot)`, `LoadGame(slot)`, `SaveToSlot(slot)`, `DeleteSlot(slot)`)를 Easy Save 3(ES3) 기반으로 완성해 두었다. `SaveData`에는 `sceneName`, `checkpointPosition`, `savedAtIso`만 있다.
- `SaveManager`는 싱글턴(`DontDestroyOnLoad`)이며 `Awake()`에 중복 생성 방지 로직이 이미 있어, 여러 씬에 같은 오브젝트/프리팹을 둬도 먼저 로드된 것만 살아남는다.
- 목표: "처음부터"(새 게임) / "이어하기"(세이브 슬롯 선택) / "종료" 버튼이 있는 타이틀 화면을 추가한다. 슬롯은 3개. 새 게임도 슬롯 선택 화면을 거친다.

## 목표

1. 게임 실행 시 `Title.unity`가 먼저 뜨고, 여기서 처음부터/이어하기/종료를 선택한다.
2. 처음부터/이어하기 모두 3개 슬롯 중 하나를 고르는 동일한 슬롯 선택 화면을 재사용한다.
3. 슬롯 선택 화면에서 세이브 삭제도 할 수 있다.
4. 기존 `SaveManager`/`SaveData`의 기존 메서드는 한 줄도 수정하지 않는다. (슬롯 목록에 저장 시각을 표시하려면 부작용 없는 조회 메서드가 하나 필요해서, 아래처럼 새 메서드 1개만 추가한다.)

## 아키텍처

### 1. 씬 구성

- `Assets/Scenes/Title.unity` (신규) — Build Settings에서 index 0으로 등록, `SampleScene`은 index 1.
- `SaveManager`를 프리팹(`Assets/Prefabs/SaveManager.prefab`, 신규)으로 추출해 `Title.unity`와 `SampleScene` 양쪽에 인스턴스를 배치한다. 기존 `Awake()`의 중복 방지 로직 덕분에 코드 변경 없이 "먼저 로드된 씬의 것이 계속 살아남는" 구조가 그대로 성립한다.
- `Title.unity`에는 Canvas 하나에 패널 3개를 둔다: `TitleRootPanel`(기본 활성), `SlotSelectPanel`(기본 비활성), `ConfirmPopupPanel`(기본 비활성).

### 2. `SaveManager.cs`에 조회 메서드 1개 추가 (유일한 기존 코드 변경)

슬롯 목록 UI가 `savedAtIso`를 표시하려면 `LoadGame`처럼 씬 전환/체크포인트 복원 부작용 없이 데이터만 읽는 방법이 필요하다. 기존 `private GetFilePath(int slot)`를 그대로 재사용해 아래 메서드 하나만 추가한다(기존 메서드는 시그니처/동작 변경 없음).

```csharp
// 슬롯 데이터를 부작용 없이 조회한다. 슬롯 선택 UI가 저장 시각을 표시할 때 사용하며,
// LoadGame과 달리 체크포인트를 갱신하거나 씬을 불러오지 않는다.
public SaveData PeekSlotData(int slot)
{
    string path = GetFilePath(slot);
    return ES3.FileExists(path) ? ES3.Load<SaveData>(saveKey, path) : null;
}
```

### 3. `TitleMenuController.cs` (신규, `Assets/Scripts/UI/TitleMenuController.cs`)

타이틀 루트 화면(처음부터/이어하기/종료 버튼)을 담당한다.

- `[SerializeField] Button newGameButton, continueButton, quitButton` — Odin `[BoxGroup("버튼 연결")]`로 정리.
- `[SerializeField] SaveSlotSelectController slotSelectController` — 슬롯 화면 컨트롤러 참조.
- `[SerializeField] int slotCount = 3` — Inspector에서 조절 가능한 슬롯 개수(하드코딩 방지).
- `Start()`: `continueButton.interactable`을 `slotCount`만큼 `SaveManager.Instance.HasSave(i)`를 순회해 하나라도 있으면 true, 전부 없으면 false로 설정.
- `OnNewGameClicked()` → `slotSelectController.Open(SaveSlotSelectController.Mode.NewGame)`.
- `OnContinueClicked()` → `slotSelectController.Open(SaveSlotSelectController.Mode.Continue)`.
- `OnQuitClicked()` → `Application.Quit()`. `#if UNITY_EDITOR`에서는 `UnityEditor.EditorApplication.isPlaying = false`로 분기해 에디터에서도 테스트 가능하게 한다.
- 패널 전환은 DOTween으로 `CanvasGroup.DOFade` 사용(간단한 페이드).

### 4. `SaveSlotSelectController.cs` (신규, `Assets/Scripts/UI/SaveSlotSelectController.cs`)

슬롯 3개 목록을 보여주고 모드(새 게임/이어하기)에 따라 클릭 동작을 분기한다.

- `public enum Mode { NewGame, Continue }`
- `[SerializeField] SaveSlotUIItem[] slotItems` — 슬롯 UI 아이템 배열(Inspector에서 3개 연결). Odin `[BoxGroup("슬롯 UI")]`.
- `[SerializeField] ConfirmPopupUI confirmPopup` — 덮어쓰기/삭제 확인 팝업 참조.
- `[SerializeField] string gameplaySceneName = "SampleScene"` — Inspector에서 조절 가능한 게임플레이 씬 이름(하드코딩 방지).
- `_currentMode` 필드로 현재 모드 기억.
- `Open(Mode mode)`: 패널 활성화, `_currentMode = mode`, `RefreshSlots()` 호출.
- `RefreshSlots()`: 슬롯마다 `SaveManager.Instance.PeekSlotData(slot)` 호출(null이면 빈 슬롯) 후 해당 `SaveSlotUIItem.SetState(hasSave, savedAtIso)` 호출. `Continue` 모드에서는 빈 슬롯의 클릭 버튼을 `interactable = false`로 비활성화.
- `OnSlotClicked(int slot)`:
  - `Mode.NewGame`이고 슬롯이 비어 있으면 → 바로 `StartNewGame(slot)`.
  - `Mode.NewGame`이고 슬롯에 세이브가 있으면 → `confirmPopup.Show("정말 덮어쓰시겠습니까?", () => StartNewGame(slot))`.
  - `Mode.Continue`이면(빈 슬롯은 버튼 자체가 비활성화라 여기 도달 안 함) → `SaveManager.Instance.LoadGame(slot)` 호출. (씬 전환/위치 복원은 기존 `LoadGame` 로직이 전부 처리 — `Title` 씬과 저장된 씬 이름이 다르므로 기존 코드의 "다른 씬" 분기가 자동으로 타서 `SampleScene`을 불러오고 위치를 복원한다.)
- `StartNewGame(int slot)`: `SceneManager.LoadScene(gameplaySceneName)` 호출 후 `SaveManager.Instance.NewGame(slot)` 호출. **순서가 중요하다** — 먼저 씬을 옮긴 뒤 `NewGame`을 호출해야 `SaveData.sceneName`이 `"Title"`이 아니라 `"SampleScene"`으로 정확히 기록된다(`NewGame`은 현재 활성 씬 이름을 그대로 저장하기 때문).
- `OnSlotDeleteClicked(int slot)` → `confirmPopup.Show("이 세이브를 삭제하시겠습니까?", () => { SaveManager.Instance.DeleteSlot(slot); RefreshSlots(); })`.
- `OnBackClicked()`: `SlotSelectPanel` 닫고 `TitleRootPanel`로 복귀.

### 5. `SaveSlotUIItem.cs` (신규, `Assets/Scripts/UI/SaveSlotUIItem.cs`)

슬롯 한 칸의 UI를 담당하는 단순 컴포넌트.

- `[SerializeField] TMP_Text slotLabelText` — "슬롯 1" 등 고정 라벨.
- `[SerializeField] TMP_Text stateText` — 저장 시각(`savedAtIso`를 보기 좋은 포맷으로) 또는 "비어있음".
- `[SerializeField] Button selectButton, deleteButton`.
- `SetState(bool hasSave, string savedAtIso)`: 위 텍스트/버튼 활성 상태 갱신. `hasSave`가 false면 `deleteButton.gameObject.SetActive(false)`.
- 클릭 이벤트는 `UnityEvent<int>` 대신 `SaveSlotSelectController`가 각 슬롯 인덱스를 캡처한 람다로 `selectButton.onClick`/`deleteButton.onClick`에 직접 연결한다(단순 구조 유지, 별도 이벤트 클래스 불필요).

### 6. `ConfirmPopupUI.cs` (신규, `Assets/Scripts/UI/ConfirmPopupUI.cs`)

덮어쓰기/삭제 확인에 공용으로 쓰는 예/아니오 팝업.

- `[SerializeField] TMP_Text messageText`, `[SerializeField] Button yesButton, noButton`.
- `Show(string message, Action onConfirm)`: 메시지 설정, 팝업을 DOTween `DOScale`로 팝업 등장 연출과 함께 활성화. `yesButton`에 `onConfirm` 연결 후 팝업 닫기, `noButton`은 팝업만 닫기.
- `OnDestroy()`에서 진행 중인 Tween `Kill()` 처리.

## 데이터 흐름 요약

```
게임 실행 → Title.unity 로드
  → TitleMenuController.Start(): 이어하기 버튼 활성화 여부 계산

[처음부터] → SlotSelectPanel(NewGame 모드)
  → 빈 슬롯 클릭: SceneManager.LoadScene("SampleScene") → SaveManager.NewGame(slot)
  → 채워진 슬롯 클릭: 확인 팝업 → 확인 시 위와 동일

[이어하기] → SlotSelectPanel(Continue 모드)
  → 채워진 슬롯만 클릭 가능: SaveManager.LoadGame(slot) (기존 로직이 씬 전환 + 위치 복원 전부 처리)

[삭제] → 확인 팝업 → SaveManager.DeleteSlot(slot) → 슬롯 목록 새로고침

[종료] → Application.Quit() (에디터에서는 EditorApplication.isPlaying = false)
```

## 기존 기능 영향

- `SaveManager.cs`에 조회 전용 메서드 `PeekSlotData` 1개만 추가한다. 기존 메서드(`HasSave`, `NewGame`, `LoadGame`, `SaveToSlot`, `DeleteSlot`)와 `SaveData.cs`, `CheckpointManager.cs`는 시그니처/동작 변경 없음.
- `SampleScene` 내부 게임플레이 스크립트(Player, Enemy, Season, Puzzle 등)에는 영향 없음.
- 프로젝트 설정 변경: Build Settings에 `Title.unity` 추가 및 씬 순서 조정(Title=0, SampleScene=1)이 필요. 기존에 `SampleScene`을 직접 Play하던 테스트 습관이 있다면, 이제는 저장/체크포인트 관련 테스트도 `Title → 이어하기` 경로로 들어가야 정확히 재현된다는 점을 유의해야 한다(단, `SampleScene`을 에디터에서 바로 Play하는 기존 방식도 `SaveManager`가 씬에 남아있는 한 계속 동작한다 — 타이틀을 거치지 않아도 됨).

## 범위 밖 (Out of scope)

- 세이브 슬롯에 플레이타임, 스테이지 진행률 등 추가 메타데이터 표시 — 지금 `SaveData`에 없는 정보라 이번 범위에서 제외.
- 타이틀 화면 배경/로고 아트 — 플레이스홀더(단색/그라디언트 배경 + 텍스트 제목)로 두고, 아트가 준비되면 교체.
- 버튼 클릭/호버 SFX — `Leohpaz/RPG_Essentials_Free/10_UI_Menu_SFX`에 관련 사운드가 이미 있지만, AudioManager가 아직 없어 이번 범위에서는 다루지 않는다(추후 오디오 시스템 작업 때 같이 연결하는 걸 권장).
- 설정(옵션) 메뉴 — 이번엔 요청 범위 아님.

## 테스트 방법 (Unity Editor)

- Build Settings에 `Title`, `SampleScene` 순서가 올바른지 확인 후 `Title` 씬에서 Play
- 세이브 파일이 하나도 없는 상태에서 "이어하기" 버튼이 비활성화(회색)인지 확인
- "처음부터" → 빈 슬롯 선택 → `SampleScene`으로 정상 진입하는지, Console에 Error 없는지 확인
- 진입 후 체크포인트를 하나 통과시켜 저장한 뒤, 타이틀로 돌아가 "이어하기"로 같은 슬롯을 선택했을 때 저장했던 위치로 정확히 복원되는지 확인
- 이미 세이브가 있는 슬롯에 "처음부터"로 다시 진입 시 덮어쓰기 확인 팝업이 뜨는지, 확인을 누르면 실제로 초기화되는지 확인
- 슬롯의 "삭제" 버튼 → 확인 팝업 → 삭제 후 해당 슬롯이 "비어있음"으로 바뀌고 "이어하기"에서 선택 불가능해지는지 확인
- `SaveManager` 프리팹이 `Title.unity`와 `SampleScene` 양쪽에 있어도 씬 전환 시 오브젝트가 중복 생성되지 않는지(Hierarchy에서 하나만 남는지) 확인
- 씬 전환/팝업 DOTween 연출이 중복 실행되거나 오브젝트 비활성화 시 에러를 내지 않는지 확인
