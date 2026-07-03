using Sirenix.OdinInspector;
using UnityEngine;

// Rigidbody(3D) 기반으로 적을 이동시킨다.
// 이 프로젝트는 사이드뷰(X-Y 평면, Z 고정) 기준이라 좌우(X) 이동만 다루고 Z는 건드리지 않는다.
// Player는 CharacterController를 쓰지만, 적은 넉백처럼 물리적 외력 반응이 필요해질 것을 고려해
// Rigidbody(3D)를 사용한다 (CLAUDE.md 기준: 외력 반응이 필요한 경우 Rigidbody 우선 고려).
[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    [Title("이동 설정")]
    [SerializeField, LabelText("이동 속도")]
    private float moveSpeed = 2.5f;

    [Title("접지 판정 설정")]
    [SerializeField, LabelText("접지 체크 기준점")]
    private Transform groundCheck;
    // Inspector에서 발밑 위치의 빈 Transform을 연결한다. 비워두면 오브젝트 중심을 사용한다.

    [SerializeField, LabelText("접지 체크 반경")]
    private float groundCheckRadius = 0.3f;

    [SerializeField, LabelText("바닥 레이어")]
    private LayerMask groundLayer;
    // 벽 감지(HasWallAhead)에도 같은 레이어를 재사용한다. 이 프로젝트는 아직 벽 전용 레이어가 없다.

    [Title("순찰 방향 전환 감지 설정")]
    [SerializeField, LabelText("벽 감지 높이")]
    private float wallCheckHeight = 0.5f;

    [SerializeField, LabelText("벽 감지 거리")]
    private float wallCheckDistance = 0.6f;

    [SerializeField, LabelText("낭떠러지 감지 앞쪽 거리")]
    private float edgeCheckDistance = 0.6f;
    // 이동 방향으로 이 거리만큼 앞선 지점 아래에 바닥이 있는지 확인한다.

    [SerializeField, LabelText("낭떠러지 감지 깊이")]
    private float edgeCheckDepth = 1f;

    [Title("궁지몰림 발악 점프공격 설정")]
    [SerializeField, LabelText("점프 힘")]
    private float jumpForce = 6f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("바라보는 방향 (+1 오른쪽 / -1 왼쪽)")]
    public float FacingDir { get; private set; } = 1f;

    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    public bool IsGrounded { get; private set; }

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = true;
        _rb.isKinematic = false;
        // 넉백/중력 등 실제 물리 반응이 필요하므로 Kinematic을 끈다.

        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        // 물리 충돌로 적이 넘어지거나 구르지 않도록 회전은 코드로만 제어한다.
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
    }

    private void UpdateGroundedState()
    {
        Vector3 origin = groundCheck != null ? groundCheck.position : transform.position;
        IsGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundLayer);
    }

    // dir: -1(왼쪽) ~ 1(오른쪽). 0이면 제자리에 멈춘다.
    // Y(중력) 속도는 그대로 두고 X 속도만 갱신해 Rigidbody의 중력 시뮬레이션과 공존한다.
    public void Move(float dir)
    {
        Vector3 velocity = _rb.linearVelocity;
        velocity.x = dir * moveSpeed;
        velocity.z = 0f;
        _rb.linearVelocity = velocity;

        if (dir != 0f)
            FaceDirection(dir);
    }

    // 이동 방향에 맞춰 Y축으로만 회전한다 (PlayerController.HandleFacing과 동일한 좌우 반전 방식).
    // 공격 사거리 안에서 멈춘 채로 Player 쪽만 바라볼 때도 NFBTEnemyAI가 직접 호출한다.
    public void FaceDirection(float dir)
    {
        FacingDir = Mathf.Sign(dir);
        float yRotation = FacingDir >= 0f ? 90f : -90f;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    // 궁지에 몰렸을 때(Cornered) 대각선으로 도약하며 돌진하는 점프공격이다.
    // 접지 상태일 때만 발동해 공중에서 중복 점프하지 않게 한다.
    public void JumpMove(float dir)
    {
        if (!IsGrounded) return;

        Vector3 velocity = _rb.linearVelocity;
        velocity.x = dir * moveSpeed;
        velocity.y = jumpForce;
        velocity.z = 0f;
        _rb.linearVelocity = velocity;

        FaceDirection(dir);
    }

    // dir 방향 앞쪽에 벽이 있으면 true. 순찰 중 방향을 반전시킬지 판단하는 데 사용한다.
    public bool HasWallAhead(float dir)
    {
        if (dir == 0f) return false;

        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Vector3 direction = new Vector3(dir, 0f, 0f);
        return Physics.Raycast(origin, direction, wallCheckDistance, groundLayer);
    }

    // dir 방향으로 조금 앞선 지점 아래에 바닥이 없으면(낭떠러지) false를 반환한다.
    public bool HasGroundAhead(float dir)
    {
        if (dir == 0f) return true;

        Vector3 checkOrigin = transform.position + new Vector3(dir * edgeCheckDistance, 0.1f, 0f);
        return Physics.Raycast(checkOrigin, Vector3.down, edgeCheckDepth, groundLayer);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = groundCheck != null ? groundCheck.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);

        // 순찰 방향 전환 감지용 레이(오른쪽 기준)를 씬 뷰에서 확인할 수 있게 그린다.
        Gizmos.color = Color.cyan;
        Vector3 wallOrigin = transform.position + Vector3.up * wallCheckHeight;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector3.right * wallCheckDistance);

        Vector3 edgeOrigin = transform.position + new Vector3(edgeCheckDistance, 0.1f, 0f);
        Gizmos.DrawLine(edgeOrigin, edgeOrigin + Vector3.down * edgeCheckDepth);
    }
#endif
}
