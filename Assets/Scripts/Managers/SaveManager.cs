using System;
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

    [ReadOnly, ShowInInspector, LabelText("현재 사용 중인 슬롯")]
    private int _currentSlot;

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

    private string GetFilePath(int slot) => $"save_slot{slot}.es3";
    // 슬롯 번호별로 별도의 ES3 파일을 사용해 슬롯끼리 데이터가 섞이지 않게 한다.

    // 슬롯에 저장된 세이브 파일이 있는지 확인한다. 타이틀 화면에서 "이어하기" 버튼 활성화 여부 판단에 사용한다.
    public bool HasSave(int slot) => ES3.FileExists(GetFilePath(slot));

    // 새 게임을 시작한다. 기존 슬롯 파일이 있으면 지우고, 현재 씬/원점 기준으로 새로 저장한다.
    public void NewGame(int slot)
    {
        _currentSlot = slot;

        string path = GetFilePath(slot);
        if (ES3.FileExists(path))
            ES3.DeleteFile(path);

        var data = new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            checkpointPosition = Vector3.zero,
            savedAtIso = DateTime.UtcNow.ToString("o")
        };

        ES3.Save(saveKey, data, path);
    }

    // 슬롯에서 세이브 데이터를 불러와 체크포인트 위치를 갱신하고 플레이어를 그 위치로 되돌린다.
    public void LoadGame(int slot)
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

        // 지금은 씬이 하나뿐이라 대부분 이 분기로 처리되지만, 스테이지가 늘어났을 때를 대비해
        // 세이브된 씬 이름이 다르면 그 씬을 불러온 뒤 위치를 적용하도록 분리해 둔다.
        if (SceneManager.GetActiveScene().name == data.sceneName)
        {
            ApplyLoadedPosition(data.checkpointPosition);
        }
        else
        {
            _pendingLoadPosition = data.checkpointPosition;
            SceneManager.sceneLoaded += OnSceneLoadedApplyPosition;
            SceneManager.LoadScene(data.sceneName);
        }
    }

    private void OnSceneLoadedApplyPosition(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedApplyPosition;
        ApplyLoadedPosition(_pendingLoadPosition);
    }

    private void ApplyLoadedPosition(Vector3 position)
    {
        // GameObject.Find/FindObjectOfType 대신, PlayerHealth가 스스로 등록해 둔 Instance로 접근한다.
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Respawn(position);
    }

    // 현재 체크포인트 상태를 지금 사용 중인 슬롯에 저장한다. CheckpointTrigger가 호출한다.
    public void SaveCheckpoint()
    {
        SaveToSlot(_currentSlot);
    }

    // 지정한 슬롯에 현재 체크포인트 상태를 저장한다.
    public void SaveToSlot(int slot)
    {
        var data = new SaveData
        {
            sceneName = CheckpointManager.HasCheckpoint ? CheckpointManager.CurrentCheckpointScene : SceneManager.GetActiveScene().name,
            checkpointPosition = CheckpointManager.CurrentCheckpointPosition,
            savedAtIso = DateTime.UtcNow.ToString("o")
        };

        ES3.Save(saveKey, data, GetFilePath(slot));
    }

    // 슬롯의 세이브 파일을 완전히 삭제한다. 세이브 삭제 UI 등에서 사용한다.
    public void DeleteSlot(int slot)
    {
        string path = GetFilePath(slot);
        if (ES3.FileExists(path))
            ES3.DeleteFile(path);
    }
}
