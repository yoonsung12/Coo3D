using Sirenix.OdinInspector;
using UnityEngine;

// 적의 시야를 담당한다. 이 프로젝트는 쿼터뷰처럼 보이지만 실제 이동/연출은
// 사이드뷰(X-Y 평면, Z 고정) 기준으로 짜여 있어서(PlayerController, Season 낙하물과 동일),
// 시야도 X-Y 평면 각도 + Physics.Raycast로 판정한다.
public class EnemyVision : MonoBehaviour
{
    [Title("시야 설정")]
    [SerializeField, LabelText("시야 거리")]
    private float viewDistance = 8f;
    // 이 거리보다 멀리 있는 Player는 감지하지 못한다.

    // CombatStatsTracker가 "이 적 감지 범위 내에서 Player가 공격했는지" 판단할 때 사용한다.
    public float ViewDistance => viewDistance;

    [SerializeField, LabelText("시야각 (도)")]
    private float viewAngle = 90f;
    // 바라보는 방향을 기준으로 좌우 합쳐 이 각도 안에 있어야 감지된다.

    [SerializeField, LabelText("장애물 레이어")]
    private LayerMask obstacleMask;
    // 시야를 가리는 벽 등의 레이어를 지정한다. Player 레이어는 포함하지 않는다.

    [SerializeField, LabelText("시야 원점 오프셋")]
    private Vector3 visionOffset = new Vector3(0f, 1f, 0f);
    // 발밑이 아닌 눈높이에서 시야를 계산하기 위한 오프셋이다.

    [SerializeField, LabelText("Player 조준 높이 오프셋")]
    private float targetHeightOffset = 1f;
    // Player의 발밑(transform.position)이 아니라 가슴 높이를 조준한다.
    // 발밑 좌표를 그대로 쓰면 Ground 콜라이더 높이와 겹쳐서, 레이가 Player 대신
    // 그 자리의 바닥에 맞아 "가려짐"으로 잘못 판정되는 문제가 있었다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("바라보는 방향 (+1 오른쪽 / -1 왼쪽)")]
    private float _facingDir = 1f;

    public float FacingDir => _facingDir;

    // NFBTEnemyAI/EnemyMovement가 이동 방향에 맞춰 매 프레임 호출해 시야 방향을 갱신한다.
    public void SetFacingDirection(float dir)
    {
        if (dir == 0f) return;
        _facingDir = Mathf.Sign(dir);
    }

    // Player가 시야 거리/각도 안에 있고, 장애물에 가려지지 않았는지 판정한다.
    public bool CanSeePlayer(Transform player)
    {
        if (player == null) return false;

        Vector3 origin = transform.position + visionOffset;
        Vector3 targetPoint = player.position + new Vector3(0f, targetHeightOffset, 0f);
        Vector3 toPlayer = targetPoint - origin;

        // 사이드뷰라 Z 성분은 시야 판정에서 제외한다.
        toPlayer.z = 0f;

        float distance = toPlayer.magnitude;
        if (distance > viewDistance) return false;

        Vector3 facing = new Vector3(_facingDir, 0f, 0f);
        float angle = Vector3.Angle(facing, toPlayer);
        if (angle > viewAngle * 0.5f) return false;

        // 장애물에 가려져 있으면 감지하지 못한다.
        // Player도 obstacleMask에 포함된 레이어(Default)에 있을 수 있으므로,
        // 레이캐스트가 맞은 대상이 Player 자신이면 "가려짐"이 아니라 "보임"으로 처리한다.
        if (Physics.Raycast(origin, toPlayer.normalized, out RaycastHit hit, distance, obstacleMask))
        {
            if (hit.transform != player && !hit.transform.IsChildOf(player))
                return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 씬 뷰에서 시야 범위를 부채꼴로 시각화한다.
        Vector3 origin = transform.position + visionOffset;
        Vector3 facing = new Vector3(_facingDir, 0f, 0f);

        Gizmos.color = Color.yellow;
        // 사이드뷰(X-Y 평면) 안에서 부채꼴을 그리려면 Z축 기준 회전을 써야 한다.
        Quaternion leftRot = Quaternion.Euler(0f, 0f, viewAngle * 0.5f);
        Quaternion rightRot = Quaternion.Euler(0f, 0f, -viewAngle * 0.5f);
        Gizmos.DrawLine(origin, origin + (leftRot * facing) * viewDistance);
        Gizmos.DrawLine(origin, origin + (rightRot * facing) * viewDistance);
        Gizmos.DrawLine(origin, origin + facing * viewDistance);
    }
#endif
}
