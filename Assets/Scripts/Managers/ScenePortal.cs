using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 특정 구역에 플레이어가 진입하면 지정된 씬으로 전환하는 포털 트리거다.
// 페이드 아웃 → 씬 로드 순서로 진행하며, 새 씬에서 SceneSpawnPoint가 플레이어를 배치하고 페이드 인한다.
// 이 컴포넌트가 붙은 오브젝트에는 Is Trigger가 체크된 Collider가 있어야 한다.
[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [Title("목표 씬 설정")]

#if UNITY_EDITOR
    [SerializeField, LabelText("목표 씬 파일")]
    private SceneAsset targetSceneAsset;
    // Inspector에서 씬 파일을 드래그해서 연결한다. 문자열 오타로 인한 오류를 방지한다.
#endif

    [SerializeField, LabelText("목표 씬 이름"), ReadOnly]
    private string targetSceneName;
    // targetSceneAsset에서 자동으로 동기화된다. 직접 입력하지 않는다.

    [SerializeField, LabelText("목표 스폰 포인트 ID")]
    private string targetSpawnId = "";
    // 목표 씬에 배치된 SceneSpawnPoint의 ID와 정확히 일치해야 한다.
    // 예: "stage2_start", "boss_room_entrance"

    [Title("연출 설정")]
    [SerializeField, LabelText("페이드 아웃 시간 (초)")]
    private float fadeOutDuration = 0.4f;

    [Title("런타임 상태")]
    [ShowInInspector, ReadOnly, LabelText("전환 중")]
    private bool _isTransitioning = false;
    // 중복 전환을 방지하기 위한 플래그다.

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isTransitioning) return;
        if (!other.CompareTag("Player")) return;

        _isTransitioning = true;

        // 스폰 ID를 정적 변수에 저장해 씬 전환 후 SceneSpawnPoint가 읽을 수 있게 한다.
        SceneTransitionData.PendingSpawnId = targetSpawnId;

        // 현재 체력을 이어받도록 기록한다. 새 씬에서 체력이 초기화되지 않고 그대로 유지된다.
        if (PlayerHealth.Instance != null)
        {
            SceneTransitionData.HasPendingHealth = true;
            SceneTransitionData.PendingHealth = PlayerHealth.Instance.CurrentHealth;
        }

        // 새 씬의 SeasonGaugeManager가 게이지/디버프를 초기화하지 않고 이어가도록 신호를 보낸다.
        SceneTransitionData.PreserveSeasonState = true;

        // ScreenFader는 DontDestroyOnLoad 싱글턴이라 씬 전환 후에도 같은 인스턴스가 유지되므로,
        // 여기서 검게 덮은 화면이 씬 로드 동안 자연스럽게 이어진다.
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(() => SceneManager.LoadScene(targetSceneName), fadeOutDuration);
        else
            SceneManager.LoadScene(targetSceneName);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // SceneAsset이 변경되면 씬 이름을 자동으로 동기화한다.
        targetSceneName = targetSceneAsset != null ? targetSceneAsset.name : "";
    }

    [Button("Build Settings 등록 여부 확인")]
    private void CheckBuildSettings()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[ScenePortal] 목표 씬이 설정되지 않았습니다.");
            return;
        }

        foreach (var scene in EditorBuildSettings.scenes)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            if (name == targetSceneName)
            {
                string status = scene.enabled ? "활성화됨" : "비활성화됨 — Build Settings에서 체크해야 합니다";
                Debug.Log($"[ScenePortal] '{targetSceneName}' → Build Settings에 등록됨 ({status})");
                return;
            }
        }

        Debug.LogError($"[ScenePortal] '{targetSceneName}' → Build Settings에 없습니다. File → Build Settings에서 씬을 추가하세요.");
    }
#endif
}
