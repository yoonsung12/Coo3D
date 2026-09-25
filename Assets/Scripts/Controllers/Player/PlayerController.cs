using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

// 플레이어 이동/카메라 시점 모드다.
// SideView: 좌우(X)로만 이동하고 Z는 고정한다. TopDown: 위에서 내려다보며 WASD로 X/Z 360도 이동한다.
public enum ViewMode
{
    SideView,
    TopDown
}

// CharacterController 기반 3D 쿼터뷰 플레이어 이동을 처리한다.
// Rigidbody 대신 CharacterController를 사용해 예측 가능한 이동과 충돌을 보장한다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Title("이동 설정")]
    [SerializeField, LabelText("이동 속도")]
    private float moveSpeed = 5f;
    // 값이 커질수록 플레이어가 더 빠르게 이동한다.

    [SerializeField, LabelText("점프 힘")]
    private float jumpForce = 8f;
    // 값이 커질수록 더 높이 점프한다.

    [SerializeField, LabelText("중력 가속도")]
    private float gravity = -20f;
    // CharacterController는 물리 엔진 중력을 받지 않으므로 수동으로 적용한다.
    // 음수 값이며 클수록 더 빠르게 떨어진다.

    [Title("공중 상승 점프 설정")]
    [SerializeField, LabelText("상승 점프 힘")]
    private float airJumpForce = 10f;
    // 공중에서 점프 버튼을 한 번 더 누르면 적용되는 수직 속도다.
    // jumpForce와 합쳐 도달 가능한 최대 높이가 정해지므로, 발판 배치를 바꿀 땐 이 값도 함께 고려해야 한다.

    [Title("반동 감속 설정")]
    [SerializeField, LabelText("일반 반동 감속률")]
    private float recoilDecay = 8f;
    // 선풍기 일반 바람 반동이 줄어드는 속도다. 클수록 반동이 빨리 사라진다.

    [SerializeField, LabelText("블라스트 반동 감속률")]
    private float blastDecay = 4f;
    // 선풍기 블라스트 반동이 줄어드는 속도다. Lerp 방식으로 지수 감속한다.

    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다.

    [Title("시점 모드 설정")]
    [SerializeField, LabelText("사이드뷰 Z 복귀 속도")]
    private float laneReturnSpeed = 10f;
    // 탑다운 구역에서 나와 사이드뷰로 돌아올 때, 사이드뷰 라인(Z)으로 되돌아가는 초당 최대 속도다.
    // 값이 클수록 빨리 복귀하고, 너무 작으면 사이드뷰에서 한동안 앞뒤로 어긋나 보인다.

    [ReadOnly, ShowInInspector, LabelText("현재 시점 모드")]
    public ViewMode CurrentViewMode { get; private set; } = ViewMode.SideView;

    // 시점 모드가 바뀔 때 알린다. SideViewCamera가 구독해 카메라 시점을 함께 전환한다.
    public event System.Action<ViewMode> OnViewModeChanged;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    public bool IsGrounded { get; private set; } = true;

    // 선풍기가 바람을 쏘는 방향 결정에 사용된다.
    public Vector3 FacingDirection { get; private set; } = Vector3.forward;

    private CharacterController _cc;

    // 이동 속도 성분들 (선풍기 반동/바람과 분리해 각각 감속 처리한다)
    private Vector3 _moveVelocity;
    private Vector3 _recoilVelocity;
    private Vector3 _blastVelocity;
    private Vector3 _windVelocity;
    private Vector3 _platformVelocity;
    // FloatingBox 위에 올라탔을 때 그 발판의 상승/하강 속도를 담는다 (Y 성분만 사용).
    private float _verticalVelocity;

    // 선풍기 차징 중 이동속도를 줄이기 위한 배율 (기본 1.0, 차징 시 0.3)
    private float _speedMultiplier = 1f;

    // 시즌 게이지 디버프(DebuffController)가 제어하는 이동 제한 상태들이다.
    private bool _isBound;    // 봄 디버프: 이동만 막는다 (점프는 가능).
    private bool _isReversed; // 가을 디버프: 좌우 입력 방향이 반전된다.
    private bool _isFrozen;   // 겨울 디버프: 이동과 점프를 모두 막는다.

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private Vector2 _moveInput;

    // 공중 상승 점프를 착지 전까지 한 번만 쓸 수 있게 막는 플래그다. 착지하면 자동으로 풀린다.
    private bool _airJumpUsed;

    // 사이드뷰로 돌아올 때 복귀할 Z 라인과, 아직 복귀 중인지 여부다.
    private float _laneZ;
    private bool _isReturningToLane;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // 쿼터뷰 3D에서는 계단 자동 오르기가 필요 없으므로 stepOffset을 0으로 설정한다.
        // 이 값이 0이 아니면 평지 BoxCollider 가장자리에서 캐릭터가 공중에 떠오르는 문제가 생긴다.
        _cc.stepOffset = 0f;

        // InputActionAsset에서 Player 맵의 Move, Jump 액션을 찾아 연결한다.
        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        _jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _moveAction.Enable();
        _jumpAction.Enable();
        _jumpAction.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        _jumpAction.performed -= OnJumpPerformed;
        _moveAction.Disable();
        _jumpAction.Disable();
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        // 빙결 디버프 중에는 점프도 막는다.
        if (_isFrozen) return;

        if (IsGrounded)
        {
            _verticalVelocity = jumpForce;
            return;
        }

        // 공중 상승 점프: 공중에서 점프 버튼을 한 번 더 누르면, 착지 전까지 딱 한 번만 추가로 더 높이 뛴다.
        if (!_airJumpUsed)
        {
            _verticalVelocity = airJumpForce;
            _airJumpUsed = true;
        }
    }

    private void Update()
    {
        _moveInput = _moveAction.ReadValue<Vector2>();
        IsGrounded = _cc.isGrounded;

        // 접지 중이고 내려가는 중이면 작은 아래 힘을 유지해 경사면 미끄러짐을 방지한다.
        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        // 착지하면 공중 상승 점프를 다시 쓸 수 있게 풀어준다.
        if (IsGrounded)
            _airJumpUsed = false;

        HandleMove();
        HandleFacing();
        // 선풍기/횃불이 사용하는 FacingDirection을 매 프레임 마우스 방향으로 갱신한다.
        // 이동 방향 기반 회전(HandleMove) 이후에 호출해 마우스 조준 방향이 몸통 회전에 최종 반영되게 한다.
        ApplyGravity();
        ApplyFallSpeedLimit();
        ApplyWindVertical();
        ApplyPlatformVertical();
        DecayRecoil();

        // 수평 속도 성분을 합산하고 수직 속도를 Y에 적용해 최종 이동한다.
        Vector3 horizontal = _moveVelocity + _recoilVelocity + _blastVelocity + _windVelocity;

        // 사이드뷰에서는 반동/바람 등 어떤 원인이든 Z 이동을 버려 캐릭터가 앞뒤로 벗어나지 않게 한다.
        // 단, 탑다운 구역에서 막 나온 직후라면 사이드뷰 라인(Z)으로 돌아가는 속도만 허용한다.
        if (CurrentViewMode == ViewMode.SideView)
            horizontal.z = GetLaneReturnVelocityZ();

        Vector3 finalVelocity = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
        _cc.Move(finalVelocity * Time.deltaTime);

        // Move() 호출 이후 접지 상태를 갱신해 같은 프레임 내 다른 스크립트에 최신 값을 제공한다.
        IsGrounded = _cc.isGrounded;
    }

    private void HandleMove()
    {
        if (_isBound || _isFrozen)
        {
            _moveVelocity = Vector3.zero;
            return;
        }

        float xInput = _isReversed ? -_moveInput.x : _moveInput.x;

        // 사이드뷰는 좌우(X)만 사용하므로 W/S 입력을 무시한다.
        // 탑다운은 카메라를 Y축으로 돌리지 않으므로 W가 항상 월드 +Z(화면 위쪽)가 되어 입력을 그대로 쓴다.
        // 가을 디버프(방향 반전)는 탑다운에서 앞뒤 입력까지 함께 뒤집는다.
        float zInput = 0f;
        if (CurrentViewMode == ViewMode.TopDown)
            zInput = _isReversed ? -_moveInput.y : _moveInput.y;

        // X = 좌우
        // Z = 앞뒤
        Vector3 moveDir = new Vector3(xInput, 0f, zInput);

        // 대각선 이동이 더 빨라지는 것 방지
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        _moveVelocity = moveDir * (moveSpeed * _speedMultiplier);

        // 이동하는 방향을 바라보게 함
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDir, Vector3.up);

            transform.rotation = targetRotation;
        }
    }
    private void HandleFacing()
    {
        // Mouse.current가 없으면 (게임패드 전용 환경 등) 처리를 건너뛴다.
        if (Mouse.current == null) return;

        // 카메라에서 마우스 스크린 좌표로 광선을 발사한다.
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (CurrentViewMode == ViewMode.TopDown)
        {
            HandleTopDownFacing(ray);
            return;
        }

        // 사이드뷰: 카메라가 Z축을 따라 바라보므로, 플레이어 위치를 지나는 수직 평면(Z=플레이어Z)과
        // 광선의 교점을 구해 마우스가 가리키는 월드 XY 좌표를 얻는다.
        // 쿼터뷰의 지면 수평 평면 대신, Z축에 수직인 측면 평면을 사용한다.
        Plane sidePlane = new Plane(Vector3.forward, transform.position);
        if (sidePlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 dir = worldPoint - transform.position;
            dir.z = 0f;
            // Z 성분을 제거해 XY 평면 방향만 남긴다.

            if (dir.sqrMagnitude > 0.01f)
            {
                FacingDirection = dir.normalized;
                // FacingDirection에 Y 성분이 포함되어 선풍기/횃불이 위아래 각도로도 작동한다.

                // 캐릭터 몸통은 좌우 방향만 전환한다.
                // 마우스가 오른쪽이면 +X 방향(90°), 왼쪽이면 -X 방향(-90°)으로 Y축만 회전한다.
                // 0°/180°를 쓰면 카메라(Z축) 기준 등/정면이 뒤바뀌어 Z축 회전처럼 보이는 문제가 생긴다.
                float yRotation = dir.x >= 0f ? 90f : -90f;
                transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
        }
    }

    private void HandleTopDownFacing(Ray ray)
    {
        // 탑다운: 플레이어 높이를 지나는 수평 평면(바닥과 평행)과 마우스 광선의 교점을 구해
        // 마우스가 가리키는 바닥 위치를 얻는다. 점프 중에도 플레이어 높이 기준이라 조준이 흔들리지 않는다.
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (!groundPlane.Raycast(ray, out float distance)) return;

        Vector3 dir = ray.GetPoint(distance) - transform.position;
        dir.y = 0f;
        // Y 성분을 제거해 바닥과 평행한 XZ 방향만 남긴다. 탑다운에선 위아래 조준을 쓰지 않는다.

        if (dir.sqrMagnitude > 0.01f)
        {
            FacingDirection = dir.normalized;
            // 사이드뷰와 달리 몸통을 마우스 방향으로 360도 자유롭게 돌린다.
            transform.rotation = Quaternion.LookRotation(FacingDirection, Vector3.up);
        }
    }

    private float GetLaneReturnVelocityZ()
    {
        if (!_isReturningToLane) return 0f;

        float deltaZ = _laneZ - transform.position.z;
        if (Mathf.Abs(deltaZ) < 0.01f)
        {
            _isReturningToLane = false;
            return 0f;
        }

        // 남은 거리를 이번 프레임 안에 채우는 속도를 구하되, laneReturnSpeed를 넘지 않게 제한한다.
        // Time.deltaTime으로 나누는 이유: 최종 속도에 다시 deltaTime이 곱해지므로 도착 지점을 지나치지 않게 하기 위해서다.
        return Mathf.Clamp(deltaZ / Time.deltaTime, -laneReturnSpeed, laneReturnSpeed);
    }

    // ViewModeZone이 구역 진입/이탈 시 호출한다.
    // laneZ: 사이드뷰로 돌아갈 때 복귀할 Z 위치다. 탑다운으로 전환할 때는 사용하지 않는다.
    public void SetViewMode(ViewMode mode, float laneZ)
    {
        if (CurrentViewMode == mode) return;

        CurrentViewMode = mode;
        _laneZ = laneZ;
        // 사이드뷰로 돌아올 때만 Z 라인 복귀를 시작한다. transform.position을 직접 바꾸지 않고
        // CharacterController.Move()로 이동시키므로 벽을 뚫고 순간이동하지 않는다.
        _isReturningToLane = mode == ViewMode.SideView;

        // 이전 모드에서 받은 선풍기 반동이 새 모드에서 엉뚱한 방향으로 이어지지 않게 초기화한다.
        _recoilVelocity = Vector3.zero;
        _blastVelocity = Vector3.zero;

        OnViewModeChanged?.Invoke(mode);
    }

    // PlayerHealth.Respawn()이 순간이동 직후 호출한다.
    // CharacterController를 끈 채 옮기면 트리거 이탈 이벤트가 오지 않으므로, 현재 위치가 ViewModeZone 안인지 직접 검사한다.
    public void SyncViewModeToPosition()
    {
        // 발끝(pivot)은 구역 바닥 경계와 겹칠 수 있어 캐릭터 몸통 중심에서 검사한다.
        // QueryTriggerInteraction.Collide: ViewModeZone은 트리거 콜라이더라 이 옵션이 있어야 검사에 잡힌다.
        Vector3 center = transform.TransformPoint(_cc.center);
        Collider[] hits = Physics.OverlapSphere(center, 0.1f, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            ViewModeZone zone = hit.GetComponent<ViewModeZone>();
            if (zone != null)
            {
                SetViewMode(ViewMode.TopDown, zone.GetLaneZ());
                return;
            }
        }

        SetViewMode(ViewMode.SideView, transform.position.z);
        // 구역 밖으로 리스폰했다면 그 위치가 곧 사이드뷰 라인이므로, 이전 라인으로 끌려가지 않게 복귀를 멈춘다.
        // (이미 사이드뷰였다면 SetViewMode가 무시되므로 여기서 직접 정리한다.)
        _laneZ = transform.position.z;
        _isReturningToLane = false;
    }

    // 테스트 버튼으로 탑다운에 들어간 순간의 Z를 기억해, 사이드뷰 테스트 버튼으로 같은 라인에 돌아오게 한다.
    private float _testLaneZ;

    [Button("탑다운 전환 테스트")]
    private void TestTopDown()
    {
        _testLaneZ = transform.position.z;
        SetViewMode(ViewMode.TopDown, _testLaneZ);
    }

    [Button("사이드뷰 전환 테스트")]
    private void TestSideView() => SetViewMode(ViewMode.SideView, _testLaneZ);

    private void ApplyGravity()
    {
        // 공중에 있을 때만 중력을 누산한다. 접지 시에는 HandleMove에서 -2f로 고정된다.
        if (!IsGrounded)
            _verticalVelocity += gravity * Time.deltaTime;
    }

    private void DecayRecoil()
    {
        // 일반 반동은 MoveTowards로 선형 감속한다.
        _recoilVelocity = Vector3.MoveTowards(_recoilVelocity, Vector3.zero, recoilDecay * Time.deltaTime);
        // 블라스트 반동은 Lerp로 지수 감속해 처음에 빠르게, 나중에 천천히 줄어든다.
        _blastVelocity = Vector3.Lerp(_blastVelocity, Vector3.zero, blastDecay * Time.deltaTime);
    }

    // FanTool 일반 바람에 의한 반동을 설정한다.
    public void SetRecoil(Vector3 velocity)
    {
        _recoilVelocity = velocity;
    }

    // FanTool 블라스트에 의한 강한 반동을 설정한다.
    public void SetBlast(Vector3 velocity)
    {
        _blastVelocity = new Vector3(velocity.x, 0f, velocity.z);
        // 위로 솟구치는 블라스트일 경우 수직 속도도 함께 적용한다.
        if (velocity.y > 0f)
            _verticalVelocity = velocity.y;
    }

    // WindZone 진입 시 지속적인 바람 속도를 설정한다.
    public void SetWindZone(Vector3 windVelocity)
    {
        _windVelocity = windVelocity;
    }

    // WindZone 이탈 시 바람 속도를 초기화한다.
    public void ClearWindZone()
    {
        _windVelocity = Vector3.zero;
    }

    // FloatingBox 위에 올라탔을 때 그 발판의 상승/하강 속도를 설정한다.
    public void SetPlatformVelocity(Vector3 velocity)
    {
        _platformVelocity = velocity;
    }

    // FloatingBox에서 벗어났을 때 발판 속도를 초기화한다.
    public void ClearPlatformVelocity()
    {
        _platformVelocity = Vector3.zero;
    }

    // 선풍기 차징 중 이동속도를 제한하기 위한 배율을 설정한다.
    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }

    // 봄 디버프(속박)에 의해 이동만 막는다. DebuffController가 호출한다.
    public void SetBound(bool bound)
    {
        _isBound = bound;
    }

    // 가을 디버프(방향 반전)에 의해 좌우 입력을 뒤집는다. DebuffController가 호출한다.
    public void SetReverse(bool reverse)
    {
        _isReversed = reverse;
    }

    // 겨울 디버프(빙결)에 의해 이동과 점프를 모두 막는다. DebuffController가 호출한다.
    public void SetFrozen(bool frozen)
    {
        _isFrozen = frozen;
    }

    // 우산 글라이드 중 최대 낙하 속도를 제한한다.
    // maxFallSpeed는 음수 값이다 (예: -3f 이면 초당 3만큼 이하로 내려가지 않음).
    private float _maxFallSpeed = float.NegativeInfinity;
    private bool _hasFallSpeedLimit = false;

    public void SetMaxFallSpeed(float maxFallSpeed)
    {
        _maxFallSpeed = maxFallSpeed;
        _hasFallSpeedLimit = true;
    }

    public void ClearMaxFallSpeed()
    {
        _hasFallSpeedLimit = false;
    }

    private void ApplyFallSpeedLimit()
    {
        // 우산이 열려 있을 때 수직 낙하 속도가 최대값을 초과하지 않도록 제한한다.
        if (_hasFallSpeedLimit && _verticalVelocity < _maxFallSpeed)
            _verticalVelocity = _maxFallSpeed;
    }

    private void ApplyWindVertical()
    {
        // WindZone의 바람 방향에 위쪽 성분이 있으면 수직 속도에 직접 반영한다.
        // _windVelocity는 horizontal 합산에도 쓰이지만(X/Z), Y 성분은 그쪽에서 버려지므로
        // 여기서 별도로 _verticalVelocity에 덮어써야 "위로 부는 바람"이 실제로 작동한다.
        if (_windVelocity.y != 0f)
            _verticalVelocity = _windVelocity.y;
    }

    private void ApplyPlatformVertical()
    {
        // 뜨는 상자 위에 있는 동안은 상자의 상승/하강 속도를 그대로 수직 속도에 반영해
        // 플레이어가 상자와 함께 오르내리게 한다.
        if (_platformVelocity.y != 0f)
            _verticalVelocity = _platformVelocity.y;
    }
}
