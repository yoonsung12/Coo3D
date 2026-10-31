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
        // 튜브를 타고 있는 동안에는 플레이어의 CharacterController가 꺼져 있어서 트리거에는 튜브 콜라이더만 닿는다.
        // 그래서 탑승 중인 튜브도 플레이어와 똑같이 인정해야 물 위에서도 체크포인트가 저장된다.
        bool isPlayer = other.GetComponent<PlayerController>() != null;
        FloatTube tube = other.GetComponent<FloatTube>();
        bool isRidingTube = tube != null && tube.IsMounted;
        if (!isPlayer && !isRidingTube) return;

        Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
        CheckpointManager.SetCheckpoint(position, gameObject.scene.name);
    }
}
