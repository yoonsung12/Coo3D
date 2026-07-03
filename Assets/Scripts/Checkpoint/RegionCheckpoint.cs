using Sirenix.OdinInspector;
using UnityEngine;

// 트리거 범위에 플레이어가 들어오면 리스폰 지점만 갱신한다. 디스크 저장은 하지 않는다.
// 실제 저장(+ 전체 회복)은 더 드물게 배치되는 CheckpointTrigger가 담당한다.
[RequireComponent(typeof(Collider))]
public class RegionCheckpoint : MonoBehaviour
{
    [Title("리스폰 지점")]
    [SerializeField, LabelText("리스폰 위치 (비워두면 이 오브젝트 위치 사용)")]
    private Transform respawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
        CheckpointManager.SetCheckpoint(position, gameObject.scene.name);
    }
}
