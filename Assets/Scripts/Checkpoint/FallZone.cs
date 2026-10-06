using Sirenix.OdinInspector;
using UnityEngine;

// 절벽/구멍 아래 빈 공간에 배치하는 낙사 판정 트리거다.
// 들어온 오브젝트가 플레이어(FallDeathHandler)면 체력을 깎고 복귀 지점으로 되돌리고,
// 적(Enemy)이면 그대로 즉사시킨다.
[RequireComponent(typeof(Collider))]
public class FallZone : MonoBehaviour
{
    [Title("복귀 설정")]
    [SerializeField, LabelText("복귀 지점")]
    private Transform recoverPoint;
    // 플레이어가 떨어졌을 때 되돌아올 안전한 위치다. 절벽/구멍 바로 위 땅처럼,
    // 다시 떨어지지 않을 만한 위치에 빈 오브젝트를 놓고 연결한다.

    [SerializeField, LabelText("체크포인트가 있으면 체크포인트로 복귀")]
    private bool preferCheckpoint;
    // 맵이 길어서 하나의 FallZone이 넓은 구간을 덮는 씬(예: Summer)에서 켠다.
    // 이 씬에서 밟은 체크포인트가 있으면 그 위치로, 없으면 위의 복귀 지점으로 돌아간다.
    // 끄면 기존처럼 항상 복귀 지점으로 돌아간다.

    private void Awake()
    {
        // 플레이어/적이 그냥 통과해서 떨어져야 하므로 트리거로 강제한다.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var fallHandler = other.GetComponentInParent<FallDeathHandler>();
        if (fallHandler != null)
        {
            // 다른 씬에서 찍힌 체크포인트 좌표는 이 씬에서 의미가 없으므로 같은 씬일 때만 사용한다.
            bool useCheckpoint = preferCheckpoint
                && CheckpointManager.HasCheckpoint
                && CheckpointManager.CurrentCheckpointScene == gameObject.scene.name;
            if (useCheckpoint)
            {
                fallHandler.HandleFall(CheckpointManager.CurrentCheckpointPosition);
                return;
            }

            if (recoverPoint == null)
            {
                Debug.LogWarning($"[FallZone] '{name}'에 복귀 지점(recoverPoint)이 연결되지 않았습니다.", this);
                return;
            }

            fallHandler.HandleFall(recoverPoint.position);
            return;
        }

        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
            enemy.TakeDamage(float.MaxValue);
    }
}
