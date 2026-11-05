using Sirenix.OdinInspector;
using UnityEngine;

// 트리거 범위에 플레이어가 들어오면 리스폰 지점을 갱신하고, 첫 진입 때 한 번만 디스크에 저장한다.
// 이미 지나간 구역은 다시 밟아도 갱신·저장하지 않는다. 체력 전체 회복은 CheckpointTrigger만 담당한다.
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

        // 첫 진입이 아니면 아무것도 하지 않는다. 저장 이후 되돌아와도 리스폰 지점이 뒤로 밀리지 않게 한다.
        if (SaveManager.Instance != null && !SaveManager.Instance.MarkCheckpointActivated(SaveManager.GetObjectId(gameObject)))
            return;

        Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
        CheckpointManager.SetCheckpoint(position, gameObject.scene.name);

        // 튜브를 탄 채 저장했으면 불러올 때 튜브도 이 근처로 옮길 수 있게 기록한다.
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveCheckpoint(isRidingTube);
    }
}
