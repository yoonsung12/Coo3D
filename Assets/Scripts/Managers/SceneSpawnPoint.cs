using Sirenix.OdinInspector;
using UnityEngine;

// 씬 전환 후 플레이어가 등장할 위치를 지정하는 마커 컴포넌트다.
// ScenePortal의 목표 스폰 ID와 이 컴포넌트의 ID가 일치하면 플레이어를 이 위치로 옮기고
// 이어받은 체력을 적용한 뒤 화면을 페이드 인한다.
// 포털을 거치지 않은 일반적인 씬 로드(타이틀에서 새 게임 등)는 SaveManager/SaveSlotSelectController가
// 이미 자체적으로 페이드 인을 처리하므로, 이 컴포넌트는 ID가 일치할 때만 동작한다.
public class SceneSpawnPoint : MonoBehaviour
{
    [Title("스폰 포인트 설정")]
    [SerializeField, LabelText("스폰 포인트 ID")]
    private string spawnId = "";
    // ScenePortal의 '목표 스폰 포인트 ID'와 정확히 일치해야 한다.
    // 예: "stage2_start", "boss_room_entrance"

    [SerializeField, LabelText("페이드 인 시간 (초)")]
    private float fadeInDuration = 0.6f;

    private void Start()
    {
        if (string.IsNullOrEmpty(spawnId)) return;
        if (SceneTransitionData.PendingSpawnId != spawnId) return;

        SceneTransitionData.PendingSpawnId = "";

        PlacePlayer();
        RestorePendingHealth();
        FadeIn();
    }

    // 플레이어를 이 스폰 포인트 위치로 순간 이동시킨다.
    // PlayerHealth.Respawn()이 CharacterController를 안전하게 끄고 위치를 옮긴 뒤 다시 켜준다.
    private void PlacePlayer()
    {
        if (PlayerHealth.Instance == null)
        {
            Debug.LogError("[SceneSpawnPoint] PlayerHealth.Instance가 없습니다. 씬에 Player가 배치되어 있는지 확인하세요.");
            return;
        }

        PlayerHealth.Instance.Respawn(transform.position);
    }

    // ScenePortal이 넘겨준 체력이 있으면 적용해, 이전 씬에서의 체력 상태가 그대로 이어지게 한다.
    private void RestorePendingHealth()
    {
        if (!SceneTransitionData.HasPendingHealth) return;

        SceneTransitionData.HasPendingHealth = false;

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.SetHealth(SceneTransitionData.PendingHealth);
    }

    private void FadeIn()
    {
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeIn(null, fadeInDuration);
        else
            Debug.LogWarning("[SceneSpawnPoint] ScreenFader.Instance가 없습니다.");
    }

    private void OnDrawGizmos()
    {
        // 씬 뷰에서 스폰 위치와 ID를 시각적으로 확인할 수 있도록 기즈모를 그린다.
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.4f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.7f,
            $"SpawnPoint\n\"{spawnId}\""
        );
#endif
    }

    [Button("이 스폰 포인트 테스트 (Play Mode)")]
    private void TestSpawnHere()
    {
        if (!Application.isPlaying) return;
        PlacePlayer();
    }
}
