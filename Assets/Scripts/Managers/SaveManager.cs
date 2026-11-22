using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

// ES3(Easy Save 3)를 사용해 슬롯 단위로 진행 상태를 저장/로드하는 싱글턴 매니저다.
// 씬을 이동해도 계속 존재해야 하므로 DontDestroyOnLoad로 파괴되지 않게 한다.
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Title("세이브 설정")]
    [SerializeField, LabelText("세이브 키 이름")]
    private string saveKey = "saveData";
    // ES3 파일 안에서 데이터를 구분하는 키 이름이다. 슬롯마다 파일이 나뉘므로 키는 고정값으로 둔다.

    public const int AutoSaveSlot = 0;
    // 자동 저장 전용 슬롯 번호다. 수동 저장 슬롯(1~3)은 플레이어가 파우즈 메뉴에서 저장할 때만 바뀐다.

    [ReadOnly, ShowInInspector, LabelText("사망 시 불러올 슬롯")]
    private int _currentSlot = AutoSaveSlot;
    // 수동 슬롯을 불러온 직후 죽으면 그 슬롯으로 돌아가고, 다음 자동 저장부터는 오토 슬롯으로 돌아간다.
    // 이렇게 해야 옛 수동 세이브를 불러오자마자 죽어도 전혀 다른 진행의 오토 세이브로 튀지 않는다.

    [ReadOnly, ShowInInspector, LabelText("횃불 획득 기록")]
    public bool TorchUnlocked { get; private set; }
    // 이 매니저는 씬을 넘어가도 살아 있으므로, 가을에서 얻은 횃불 기록이 다음 씬까지 이어진다.

    [ReadOnly, ShowInInspector, LabelText("우산 획득 기록")]
    public bool UmbrellaUnlocked { get; private set; }
    // 여름의 우산 문 비둘기를 잡고 우산을 주웠는지 기록한다. 횃불 기록과 같은 방식으로 씬을 넘어 이어진다.

    [ReadOnly, ShowInInspector, LabelText("추가 최대체력")]
    public float BonusMaxHealth { get; private set; }
    // 자판기 MaxHP 아이템으로 늘어난 최대 체력 합계다. 씬이 바뀌어도 이 매니저가 들고 있다가 새 PlayerHealth가 시작할 때 더한다.

    [ReadOnly, ShowInInspector, LabelText("추가 하트 수")]
    public int BonusHearts { get; private set; }
    // 자판기 MaxHP 아이템으로 늘어난 하트 개수다. 새 PlayerHealthUI가 하트를 만들 때 기본 개수에 더한다.

    [Title("통계 (게임 클리어 화면용)")]
    [SerializeField, LabelText("타이틀 씬 이름")]
    private string titleSceneName = "Title";
    // 이 이름의 씬에 있는 동안은 플레이 시간을 쌓지 않는다.

    [ReadOnly, ShowInInspector, LabelText("누적 플레이 시간(초)")]
    public float PlayTimeSeconds { get; private set; }
    // 파우즈/게임오버/가이드북은 timeScale 0이므로 그 시간은 포함되지 않는다.

    [ReadOnly, ShowInInspector, LabelText("누적 사망 횟수")]
    public int DeathCount { get; private set; }

    public event Action<bool> TorchUnlockLoaded;

    public event Action<bool> UmbrellaUnlockLoaded;
    // 횃불과 같이, 새 게임/불러오기로 우산 해금 기록이 바뀌면 ToolManager에 알린다.
    // 새 게임/불러오기로 해금 기록이 바뀌면 알린다. 같은 씬에서 이어하기를 해도 ToolManager가 상태를 다시 맞출 수 있게 한다.

    private readonly HashSet<string> _activatedCheckpoints = new HashSet<string>();
    // 이미 지나간 체크포인트 ID 모음이다. 첫 진입 때만 저장하고 회복 외의 처리를 반복하지 않기 위해 기억한다.

    private readonly HashSet<string> _defeatedEnemies = new HashSet<string>();
    // 저장 이후 기록된 처치한 적 ID 모음이다. 씬을 다시 불러와도 Enemy가 이 목록을 보고 죽은 상태를 유지한다.

    private readonly HashSet<string> _collectedCoins = new HashSet<string>();
    // 저장 이후 기록된 먹은 씬 배치 코인 ID 모음이다. 씬을 다시 불러와도 CoinItem이 이 목록을 보고 다시 나타나지 않는다.

    private readonly HashSet<string> _solvedPuzzles = new HashSet<string>();
    // 저장 이후 기록된 해결한 퍼즐 ID 모음이다. 씬을 다시 불러와도 퍼즐(BoxLaneGoal 등)이 이 목록을 보고 풀린 상태로 시작한다.

    private bool _savedOnTube;
    // 마지막 체크포인트가 튜브를 탄 채 저장됐는지 여부다.

    public bool SavedOnTube => _savedOnTube;
    // FloatTube가 불러오기 직후 튜브를 체크포인트 근처로 옮길지 판단할 때 읽는다.

    [ReadOnly, ShowInInspector, LabelText("활성화한 체크포인트 수")]
    private int ActivatedCheckpointCount => _activatedCheckpoints.Count;

    [ReadOnly, ShowInInspector, LabelText("처치한 적 수")]
    private int DefeatedEnemyCount => _defeatedEnemies.Count;

    [ReadOnly, ShowInInspector, LabelText("먹은 코인 수 (씬 배치)")]
    private int CollectedCoinCount => _collectedCoins.Count;

    [ReadOnly, ShowInInspector, LabelText("해결한 퍼즐 수")]
    private int SolvedPuzzleCount => _solvedPuzzles.Count;

    private Vector3 _pendingLoadPosition;
    // 세이브된 씬과 현재 씬이 달라 씬을 새로 불러와야 할 때, 씬 로드가 끝난 뒤 적용할 위치를 잠시 담아둔다.

    private void Awake()
    {
        // 씬 전환 후에도 세이브 매니저가 하나만 존재하도록 중복 생성을 막는다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // timeScale이 0이면(일시정지, 게임오버, 가이드북) 시간을 쌓지 않는다.
        // 타이머 자체는 timeScale의 영향을 받지 않도록 unscaledDeltaTime을 쓴다.
        if (Time.timeScale <= 0f) return;
        if (SceneManager.GetActiveScene().name == titleSceneName) return;

        PlayTimeSeconds += Time.unscaledDeltaTime;
    }

    // 플레이어가 사망할 때마다 PlayerHealth.Die()가 호출한다.
    public void AddDeath() => DeathCount++;

    // 오브젝트를 구분하는 고유 ID를 만든다. "씬이름/부모경로/오브젝트이름" 형태다.
    // 씬에 직접 배치된 오브젝트는 로드할 때마다 같은 ID가 나오므로, 저장 이후에도 같은 적/체크포인트로 식별할 수 있다.
    public static string GetObjectId(GameObject target)
    {
        string path = target.name;
        Transform parent = target.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return target.scene.name + "/" + path;
    }

    // 체크포인트를 처음 활성화했으면 true, 이미 활성화한 적이 있으면 false를 반환한다.
    public bool MarkCheckpointActivated(string id) => _activatedCheckpoints.Add(id);

    // 적을 처치했다고 기록한다. 파일에는 다음 체크포인트 저장 때 함께 기록된다.
    public void MarkEnemyDefeated(string id) => _defeatedEnemies.Add(id);

    public bool IsEnemyDefeated(string id) => _defeatedEnemies.Contains(id);

    // 씬에 배치된 코인을 먹었다고 기록한다. 적과 같이 파일에는 다음 체크포인트 저장 때 함께 기록된다.
    public void MarkCoinCollected(string id) => _collectedCoins.Add(id);

    public bool IsCoinCollected(string id) => _collectedCoins.Contains(id);

    // 퍼즐을 풀었다고 기록한다. 적/코인과 같이 파일에는 다음 체크포인트 저장 때 함께 기록된다.
    public void MarkPuzzleSolved(string id) => _solvedPuzzles.Add(id);

    public bool IsPuzzleSolved(string id) => _solvedPuzzles.Contains(id);

    // MaxHP 아이템을 먹었을 때 호출한다. 하트 1개와 그만큼의 최대 체력을 기록하고,
    // 파일에는 다음 체크포인트 저장 때 코인과 함께 기록된다.
    public void AddMaxHealthBonus(float amount)
    {
        BonusMaxHealth += amount;
        BonusHearts++;
    }

    private string GetFilePath(int slot) => $"save_slot{slot}.es3";
    // 슬롯 번호별로 별도의 ES3 파일을 사용해 슬롯끼리 데이터가 섞이지 않게 한다.

    // 슬롯에 저장된 세이브 파일이 있는지 확인한다. 타이틀 화면에서 "이어하기" 버튼 활성화 여부 판단에 사용한다.
    public bool HasSave(int slot) => ES3.FileExists(GetFilePath(slot));

    // 슬롯 데이터를 부작용 없이 조회한다. 슬롯 선택 UI가 저장 시각을 표시할 때 사용하며,
    // LoadGame과 달리 체크포인트를 갱신하거나 씬을 불러오지 않는다.
    public SaveData PeekSlotData(int slot)
    {
        string path = GetFilePath(slot);
        return ES3.FileExists(path) ? ES3.Load<SaveData>(saveKey, path) : null;
    }

    // 새 게임을 시작한다. 기존 슬롯 파일이 있으면 지우고, 현재 씬/원점 기준으로 새로 저장한다.
    public void NewGame(int slot) => NewGame(slot, SceneManager.GetActiveScene().name);

    // 새 게임을 시작하면서 저장할 씬 이름을 직접 지정한다.
    // SceneManager.LoadScene은 호출한 프레임에 즉시 활성 씬을 바꾸지 않고 다음 프레임에 반영되므로,
    // "씬을 먼저 옮긴 뒤 NewGame을 호출"하는 방식으로는 활성 씬 이름을 정확히 기록할 수 없다.
    // 옮겨갈 씬을 이미 알고 있는 호출자(예: 타이틀의 슬롯 선택 화면)는 이 오버로드로 명시적으로 전달한다.
    public void NewGame(int slot, string sceneName)
    {
        _currentSlot = slot;

        string path = GetFilePath(slot);
        if (ES3.FileExists(path))
            ES3.DeleteFile(path);

        // 새 게임은 횃불을 얻지 않은 상태로 시작한다.
        TorchUnlocked = false;
        TorchUnlockLoaded?.Invoke(TorchUnlocked);

        // 우산도 새 게임에서는 얻지 않은 상태로 시작한다.
        UmbrellaUnlocked = false;
        UmbrellaUnlockLoaded?.Invoke(UmbrellaUnlocked);

        // 이전 플레이의 체크포인트/처치 기록과 코인이 새 게임에 남지 않게 비운다.
        _activatedCheckpoints.Clear();
        _defeatedEnemies.Clear();
        _collectedCoins.Clear();
        _solvedPuzzles.Clear();
        _savedOnTube = false;
        BonusMaxHealth = 0f;
        BonusHearts = 0;
        PlayTimeSeconds = 0f;
        DeathCount = 0;
        if (CoinManager.Instance != null)
            CoinManager.Instance.SetCoin(0);

        var data = new SaveData
        {
            sceneName = sceneName,
            checkpointPosition = Vector3.zero,
            savedAtIso = DateTime.UtcNow.ToString("o"),
            torchUnlocked = false,
            umbrellaUnlocked = false
        };

        ES3.Save(saveKey, data, path);
    }

    // 슬롯에서 세이브 데이터를 불러와 체크포인트 위치를 갱신하고 플레이어를 그 위치로 되돌린다.
    public void LoadGame(int slot) => LoadSlot(slot, false);

    // 사망 후 계속하기에서 호출한다. 마지막으로 저장한 슬롯을 불러오되, 같은 씬이어도 씬을 다시 로드해
    // 적/오브젝트/보스가 초기 상태로 돌아가게 한다. 저장 파일이 없으면 현재 씬만 다시 로드한다.
    public void ReloadLastSave()
    {
        if (ES3.FileExists(GetFilePath(_currentSlot)))
        {
            LoadSlot(_currentSlot, true);
            return;
        }

        // 저장이 없는 에디터 실행 등에서는 위치 적용 없이 현재 씬만 다시 불러온다.
        string sceneToReload = SceneManager.GetActiveScene().name;
        FadeOutThen(() =>
        {
            // 게임오버로 timeScale이 0인 상태라, 씬 로드 직전에 복구해야 다음 씬이 멈추지 않는다.
            Time.timeScale = 1f;
            _applyPendingPosition = false;
            SceneManager.sceneLoaded += OnSceneLoadedApplyPosition;
            SceneManager.LoadScene(sceneToReload);
        });
    }

    private bool _applyPendingPosition = true;
    // 씬 로드 후 _pendingLoadPosition으로 플레이어를 옮길지 여부다. 저장 없이 씬만 다시 불러올 때는 false다.

    // forceReload가 true면 저장된 씬이 현재 씬과 같아도 씬을 다시 로드한다.
    private void LoadSlot(int slot, bool forceReload)
    {
        string path = GetFilePath(slot);

        if (!ES3.FileExists(path))
        {
            Debug.LogWarning($"[SaveManager] 슬롯 {slot}에 저장된 데이터가 없습니다.");
            return;
        }

        _currentSlot = slot;
        var data = ES3.Load<SaveData>(saveKey, path);
        CheckpointManager.SetCheckpoint(data.checkpointPosition, data.sceneName);

        // 위치를 옮기기 전에 해금 기록부터 복원해, 이어하기 직후부터 도구 상태가 맞도록 한다.
        TorchUnlocked = data.torchUnlocked;
        TorchUnlockLoaded?.Invoke(TorchUnlocked);

        UmbrellaUnlocked = data.umbrellaUnlocked;
        UmbrellaUnlockLoaded?.Invoke(UmbrellaUnlocked);

        // 체크포인트/처치 기록을 저장 시점으로 되돌린다. 예전 세이브에는 목록이 없어 null일 수 있으므로 빈 상태로 둔다.
        _activatedCheckpoints.Clear();
        if (data.activatedCheckpointIds != null)
            _activatedCheckpoints.UnionWith(data.activatedCheckpointIds);

        _defeatedEnemies.Clear();
        if (data.defeatedEnemyIds != null)
            _defeatedEnemies.UnionWith(data.defeatedEnemyIds);

        // 먹은 코인 기록도 코인 개수와 같은 저장 시점으로 되돌린다. 예전 세이브에는 목록이 없어 null일 수 있다.
        _collectedCoins.Clear();
        if (data.collectedCoinIds != null)
            _collectedCoins.UnionWith(data.collectedCoinIds);

        // 해결한 퍼즐 기록도 저장 시점으로 되돌린다. 예전 세이브에는 목록이 없어 null일 수 있다.
        _solvedPuzzles.Clear();
        if (data.solvedPuzzleIds != null)
            _solvedPuzzles.UnionWith(data.solvedPuzzleIds);

        _savedOnTube = data.savedOnTube;

        // 통계는 타이틀에서 불러올 때(forceReload == false)만 복원한다.
        // 사망 후 계속하기(forceReload == true)에서 복원하면 방금 센 사망 횟수와 흘러간 플레이 시간이
        // 저장 시점 값으로 되돌려져 버리므로, 이때는 메모리의 현재 값을 그대로 유지한다.
        if (!forceReload)
        {
            PlayTimeSeconds = data.playTimeSeconds;
            DeathCount = data.deathCount;
        }

        // 코인은 저장 시점의 값으로 복귀한다.
        if (CoinManager.Instance != null)
            CoinManager.Instance.SetCoin(data.coinCount);

        // 산 최대체력도 저장 시점으로 되돌린다. 값이 바뀌었는지는 아래에서 씬 재로드 여부를 정할 때 쓴다.
        bool bonusChanged = !Mathf.Approximately(BonusMaxHealth, data.bonusMaxHealth) || BonusHearts != data.bonusHearts;
        BonusMaxHealth = data.bonusMaxHealth;
        BonusHearts = data.bonusHearts;

        // 세이브된 씬이 현재 씬과 같고 강제 재로드가 아니면 위치만 옮긴다.
        // 그 외에는 씬을 새로 불러와 적/오브젝트를 초기 상태로 되돌린 뒤 위치를 적용한다.
        // 최대체력 보너스가 달라졌으면 같은 씬이라도 다시 불러온다. 지금 플레이어/하트 UI는 이전 보너스로 만들어져 있어,
        // 위치만 옮기면 산 하트는 남고 쓴 코인만 돌려받는 일이 생기기 때문이다.
        if (!forceReload && !bonusChanged && SceneManager.GetActiveScene().name == data.sceneName)
        {
            ApplyLoadedPosition(data.checkpointPosition);
        }
        else
        {
            _pendingLoadPosition = data.checkpointPosition;
            string sceneToLoad = data.sceneName;
            FadeOutThen(() =>
            {
                // 게임오버로 timeScale이 0인 상태에서 호출되므로, 씬 로드 직전에 복구한다.
                Time.timeScale = 1f;
                _applyPendingPosition = true;
                SceneManager.sceneLoaded += OnSceneLoadedApplyPosition;
                SceneManager.LoadScene(sceneToLoad);
            });
        }
    }

    // 씬 전환이 갑자기 보이지 않도록 화면을 먼저 검게 덮은 뒤 콜백을 실행한다.
    // 타이틀을 거치지 않은 에디터 실행에서는 ScreenFader가 없을 수 있어, 그때는 바로 실행한다.
    private void FadeOutThen(Action onFadedOut)
    {
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(onFadedOut);
        else
            onFadedOut();
    }

    private void OnSceneLoadedApplyPosition(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedApplyPosition;
        if (_applyPendingPosition)
            ApplyLoadedPosition(_pendingLoadPosition);

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeIn();
    }

    private void ApplyLoadedPosition(Vector3 position)
    {
        // GameObject.Find/FindObjectOfType 대신, PlayerHealth가 스스로 등록해 둔 Instance로 접근한다.
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Respawn(position);
    }

    // 현재 체크포인트 상태를 오토 슬롯에 저장한다. 체크포인트와 씬 입장에서 호출하며, 수동 슬롯은 건드리지 않는다.
    // onTube는 튜브를 탄 채로 저장하는지 여부다(RegionCheckpoint가 물 위 체크포인트에서 true로 넘긴다).
    public void SaveCheckpoint(bool onTube = false)
    {
        _savedOnTube = onTube;
        _currentSlot = AutoSaveSlot;
        SaveToSlot(AutoSaveSlot);
    }

    // 지정한 슬롯에 현재 체크포인트 상태를 저장한다.
    public void SaveToSlot(int slot)
    {
        string sceneName = CheckpointManager.HasCheckpoint ? CheckpointManager.CurrentCheckpointScene : SceneManager.GetActiveScene().name;
        WriteSlot(slot, sceneName, CheckpointManager.CurrentCheckpointPosition);
    }

    // 파우즈 메뉴의 수동 저장에서 호출한다. 플레이어가 땅에 서 있으면 "지금 서 있는 자리"를 저장해,
    // 이어하기 때 마지막 체크포인트가 아니라 저장한 그 위치에서 시작하게 한다.
    // 공중(점프/낙하 중)이나 튜브 위에서는 이어하자마자 떨어질 수 있으므로 기존처럼 마지막 체크포인트를 저장한다.
    public void SaveToSlotAtPlayerPosition(int slot)
    {
        PlayerHealth player = PlayerHealth.Instance;
        PlayerController controller = player != null ? player.GetComponent<PlayerController>() : null;

        bool canSaveHere = controller != null && controller.IsGrounded && !controller.IsMounted;
        if (!canSaveHere)
        {
            SaveToSlot(slot);
            return;
        }

        // 체크포인트(CheckpointManager)는 건드리지 않는다. 저장 위치는 세이브 파일에만 기록된다.
        WriteSlot(slot, player.gameObject.scene.name, player.transform.position);
    }

    // 세이브 파일 한 개를 실제로 기록한다. 위치/씬 이름만 호출하는 쪽에서 정하고, 나머지 진행 상황은 공통으로 담는다.
    private void WriteSlot(int slot, string sceneName, Vector3 position)
    {
        var data = new SaveData
        {
            sceneName = sceneName,
            checkpointPosition = position,
            savedAtIso = DateTime.UtcNow.ToString("o"),
            torchUnlocked = TorchUnlocked,
            umbrellaUnlocked = UmbrellaUnlocked,
            coinCount = CoinManager.Instance != null ? CoinManager.Instance.CoinCount : 0,
            activatedCheckpointIds = new List<string>(_activatedCheckpoints),
            defeatedEnemyIds = new List<string>(_defeatedEnemies),
            collectedCoinIds = new List<string>(_collectedCoins),
            solvedPuzzleIds = new List<string>(_solvedPuzzles),
            bonusMaxHealth = BonusMaxHealth,
            bonusHearts = BonusHearts,
            savedOnTube = _savedOnTube,
            playTimeSeconds = PlayTimeSeconds,
            deathCount = DeathCount
        };

        ES3.Save(saveKey, data, GetFilePath(slot));
    }

    // 횃불을 얻었을 때 ToolManager가 호출한다. 파일에는 다음 체크포인트 저장 때 함께 기록된다.
    public void SetTorchUnlocked(bool unlocked) => TorchUnlocked = unlocked;

    // 우산을 얻었을 때 ToolManager가 호출한다. 파일에는 다음 체크포인트 저장 때 함께 기록된다.
    public void SetUmbrellaUnlocked(bool unlocked) => UmbrellaUnlocked = unlocked;

    // 슬롯의 세이브 파일을 완전히 삭제한다. 세이브 삭제 UI 등에서 사용한다.
    public void DeleteSlot(int slot)
    {
        string path = GetFilePath(slot);
        if (ES3.FileExists(path))
            ES3.DeleteFile(path);
    }

    [Button("마지막 저장 불러오기 (Play Mode)")]
    private void TestReloadLastSave()
    {
        if (!Application.isPlaying) return;
        ReloadLastSave();
    }
}
