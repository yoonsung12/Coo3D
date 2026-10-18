using Sirenix.OdinInspector;
using UnityEngine;

// Rigidbody(3D) 기반으로 적을 이동시킨다.
// float(±1) 버전 API는 좌우(X) 이동만 다루고 Z는 0으로 고정한다(보스가 사용).
// Vector3 버전 API는 사이드뷰 앞뒤 이동이 생기면서 추가한 XZ 360도 이동이다(잡몹 NFBTEnemyAI가 사용).
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

    [Title("근접 공격 히트박스 보정")]
    [SerializeField, LabelText("공격 히트박스(AttackPoint)")]
    private Transform attackAnchor;
    // EnemyAttackHitbox가 달린 자식(AttackPoint)을 연결한다. 비워두면 보정하지 않는다.
    // FaceDirection이 오브젝트를 Y축으로 90도 회전시키는데, attackAnchor의 로컬 X 오프셋을
    // 그대로 두면 회전에 밀려 월드 Z축으로 튀어나가 버려서(사이드뷰 X축 기준 판정과 어긋남)
    // 회전과 무관하게 항상 캐릭터의 앞쪽(월드 X축)을 향하도록 매 회전마다 위치를 다시 계산한다.

    [Title("360도 이동 설정 (잡몹 추적용)")]
    [SerializeField, LabelText("회전 속도 (도/초)")]
    private float turnSpeed = 720f;
    // Vector3 버전 Move/FaceDirection을 쓸 때 몸이 목표 방향으로 돌아가는 속도다.
    // 값이 클수록 홱 돌고, 작을수록 천천히 돈다. 보스처럼 float(±1) 버전만 쓰는 적에는 영향이 없다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("바라보는 방향 (+1 오른쪽 / -1 왼쪽)")]
    public float FacingDir { get; private set; } = 1f;

    [ReadOnly, ShowInInspector, LabelText("바라보는 방향 (XZ 벡터)")]
    public Vector3 FacingVector { get; private set; } = Vector3.right;
    // 360도 이동에서 실제로 바라보려는 수평 방향이다. 투사체 발사/시야 방향 계산에 쓴다.

    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    public bool IsGrounded { get; private set; }

    private Rigidbody _rb;
    private Vector3 _attackAnchorBaseOffset;
    // attackAnchor의 원래(회전 전) 로컬 오프셋이다. +1(오른쪽) 방향 기준으로 저장해두고,
    // 방향이 바뀔 때마다 이 기준값을 회전에 맞춰 다시 투영한다.

    private bool _useSmoothFacing;
    // Vector3 버전 API가 한 번이라도 호출되면 켜진다. 켜진 동안만 Update에서 몸을 목표 방향으로 천천히 돌린다.
    // float 버전만 쓰는 보스는 계속 꺼져 있어 기존의 ±90도 즉시 회전이 그대로 유지된다.

    private Quaternion _targetRotation = Quaternion.Euler(0f, 90f, 0f);

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = true;
        _rb.isKinematic = false;
        // 넉백/중력 등 실제 물리 반응이 필요하므로 Kinematic을 끈다.

        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        // 물리 충돌로 적이 넘어지거나 구르지 않도록 회전은 코드로만 제어한다.

        if (attackAnchor != null)
            _attackAnchorBaseOffset = attackAnchor.localPosition;
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
    }

    private void Update()
    {
        if (!_useSmoothFacing) return;

        // Time.deltaTime을 곱해 프레임 수와 상관없이 초당 turnSpeed 도씩 일정하게 돌게 한다.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation, turnSpeed * Time.deltaTime);
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
        FacingVector = new Vector3(FacingDir, 0f, 0f);
        float yRotation = FacingDir >= 0f ? 90f : -90f;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        _targetRotation = transform.rotation;

        UpdateAttackAnchorPosition();
    }

    // ── 360도 이동(XZ 평면) 버전 ──
    // 사이드뷰에서도 플레이어가 앞뒤(Z)로 움직이게 되면서, 잡몹이 X축뿐 아니라 XZ 어느 방향으로든 따라가도록 추가했다.
    // 위의 float(±1) 버전은 보스가 그대로 쓰므로 건드리지 않았다.

    // dir: XZ 평면 방향. 길이가 속도 배율이 된다(1이면 moveSpeed, 0이면 정지).
    // Y(중력) 속도는 그대로 두고 X/Z 속도만 갱신한다.
    public void Move(Vector3 dir)
    {
        dir.y = 0f;
        Vector3 velocity = _rb.linearVelocity;
        velocity.x = dir.x * moveSpeed;
        velocity.z = dir.z * moveSpeed;
        _rb.linearVelocity = velocity;

        if (dir.sqrMagnitude > 0.0001f)
            FaceDirection(dir);
    }

    // 몸이 dir 방향을 바라보도록 목표 회전만 정한다. 실제 회전은 Update에서 turnSpeed로 부드럽게 진행된다.
    public void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        FacingVector = dir.normalized;
        if (Mathf.Abs(FacingVector.x) > 0.01f)
            FacingDir = Mathf.Sign(FacingVector.x);
        // 기존 FacingDir(±1)을 읽는 코드가 있을 수 있어 X 성분 부호로 계속 갱신해 둔다.

        _targetRotation = Quaternion.LookRotation(FacingVector, Vector3.up);

        if (!_useSmoothFacing)
        {
            _useSmoothFacing = true;
            UpdateAttackAnchorForSmoothFacing();
        }
    }

    // 몸이 360도로 돌기 시작하면 히트박스는 몸과 함께 돌면 되므로, "몸 앞쪽" 로컬 위치에 한 번만 고정한다.
    // 기준 오프셋은 오른쪽(+X, Y회전 90도)을 바라볼 때 월드 기준 값이라, 그 회전의 역으로 로컬 좌표로 바꾼다.
    private void UpdateAttackAnchorForSmoothFacing()
    {
        if (attackAnchor == null) return;

        Vector3 rightFacingWorldOffset = new Vector3(
            Mathf.Abs(_attackAnchorBaseOffset.x),
            _attackAnchorBaseOffset.y,
            _attackAnchorBaseOffset.z);

        attackAnchor.localPosition = Quaternion.Inverse(Quaternion.Euler(0f, 90f, 0f)) * rightFacingWorldOffset;
    }

    // 수평 속도와 점프 힘을 직접 지정하는 XZ 방향 도약이다. 실제로 뛰었으면 true를 반환한다.
    public bool JumpMove(Vector3 dir, float horizontalSpeed, float upForce)
    {
        if (!IsGrounded) return false;

        dir.y = 0f;
        Vector3 horizontal = dir.normalized * horizontalSpeed;
        _rb.linearVelocity = new Vector3(horizontal.x, upForce, horizontal.z);

        FaceDirection(dir);
        return true;
    }

    // 궁지몰림 발악용 기본 도약(걷기 속도 + 기본 점프 힘)이다.
    public void JumpMove(Vector3 dir)
    {
        JumpMove(dir, moveSpeed, jumpForce);
    }

    // dir(XZ) 방향 앞쪽에 벽이 있으면 true.
    public bool HasWallAhead(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;

        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        return Physics.Raycast(origin, dir.normalized, wallCheckDistance, groundLayer);
    }

    // dir(XZ) 방향으로 조금 앞선 지점 아래에 바닥이 없으면(낭떠러지) false를 반환한다.
    // 사이드뷰 앞뒤(Z) 이동이 생기면서 길 앞/뒤 가장자리에서도 떨어지지 않게 하기 위해 쓴다.
    public bool HasGroundAhead(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return true;

        Vector3 checkOrigin = transform.position + dir.normalized * edgeCheckDistance + Vector3.up * 0.1f;
        return Physics.Raycast(checkOrigin, Vector3.down, edgeCheckDepth, groundLayer);
    }

    // attackAnchor를 회전과 무관하게 항상 월드 X축 기준 앞쪽에 위치하도록 다시 계산한다.
    // 기준 오프셋(_attackAnchorBaseOffset)은 +1(오른쪽) 방향 기준이라 왼쪽을 바라볼 때는 X를 반전한다.
    private void UpdateAttackAnchorPosition()
    {
        if (attackAnchor == null) return;

        Vector3 desiredWorldOffset = new Vector3(
            Mathf.Abs(_attackAnchorBaseOffset.x) * FacingDir,
            _attackAnchorBaseOffset.y,
            _attackAnchorBaseOffset.z);

        attackAnchor.localPosition = Quaternion.Inverse(transform.rotation) * desiredWorldOffset;
    }

    // 궁지에 몰렸을 때(Cornered) 대각선으로 도약하며 돌진하는 점프공격이다.
    // 접지 상태일 때만 발동해 공중에서 중복 점프하지 않게 한다.
    public void JumpMove(float dir)
    {
        JumpMove(dir, moveSpeed, jumpForce);
    }

    // 수평 속도와 점프 힘을 직접 지정하는 도약이다. NFBTEnemyAI의 도약공격처럼
    // 걷기 속도보다 멀리 뛰어들어야 할 때 사용한다. 실제로 뛰었으면 true를 반환한다.
    public bool JumpMove(float dir, float horizontalSpeed, float upForce)
    {
        if (!IsGrounded) return false;

        Vector3 velocity = _rb.linearVelocity;
        velocity.x = dir * horizontalSpeed;
        velocity.y = upForce;
        velocity.z = 0f;
        _rb.linearVelocity = velocity;

        FaceDirection(dir);
        return true;
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
