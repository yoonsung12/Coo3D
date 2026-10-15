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

    [SerializeField, LabelText("중력 가속도")]
    private float gravity = -20f;
    // CharacterController는 물리 엔진 중력을 받지 않으므로 수동으로 적용한다.
    // 음수 값이며 클수록 더 빠르게 떨어진다.

    [Title("점프 설정 (누르는 길이로 높이 조절)")]
    [InfoBox("점프 버튼을 누르고 있는 동안 '상승 속도'로 일정하게 올라가고, 떼는 순간 상승이 끊긴다.\n" +
             "끝까지 누르면 '최대 높이', 톡 누르면 '최소 높이'만큼 뛴다. 유지 시간은 이 값들로 자동 계산된다.")]
    [SerializeField, LabelText("최대 높이")]
    private float jumpMaxHeight = 4.1f;
    // 버튼을 끝까지 누르고 있을 때 도달하는 높이(m)다. 발판 배치는 이 높이를 기준으로 한다.
    // 기본값 4.1은 예전 "점프(힘 8) + 공중 2단 점프(힘 10)"를 중력 -20에서 합친 최대 높이와 같다.

    [SerializeField, LabelText("최소 높이")]
    private float jumpMinHeight = 0.4f;
    // 버튼을 아주 짧게 눌렀을 때도 최소한 오르는 높이(m)다. 너무 작으면 점프한 느낌이 거의 없다.

    [SerializeField, LabelText("상승 속도")]
    private float jumpRiseSpeed = 8f;
    // 누르고 있는 동안 올라가는 초당 속도다. 값이 작을수록 누르는 길이에 따른 높이 차이를 조절하기 쉽고,
    // 너무 크면(속도²/(2×중력)이 최대 높이를 넘으면) 누르는 길이와 상관없이 항상 최대 높이를 넘게 된다.

    [SerializeField, LabelText("뗐을 때 남는 상승 속도")]
    private float jumpReleaseVelocity = 0f;
    // 버튼을 뗀 순간 남겨둘 상승 속도다. 0이면 즉시 꺾여 떨어지고, 조금 주면 정점이 부드럽게 둥글어진다.

    // 누르고 있으면 중력 없이 상승을 유지하는 최대 시간이다.
    // 이후 상승 속도에서 중력으로 감속하며 오르는 높이(속도²/(2×중력))를 빼서, 합계가 정확히 최대 높이가 되게 한다.
    [ShowInInspector, ReadOnly, LabelText("최대 유지 시간(자동)")]
    private float JumpHoldMaxTime =>
        Mathf.Max(0f, (jumpMaxHeight - jumpRiseSpeed * jumpRiseSpeed / (2f * -gravity)) / jumpRiseSpeed);

    // 톡 눌러도 이 시간 동안은 상승을 유지해 최소 높이를 보장한다.
    [ShowInInspector, ReadOnly, LabelText("최소 유지 시간(자동)")]
    private float JumpHoldMinTime => Mathf.Min(jumpMinHeight / jumpRiseSpeed, JumpHoldMaxTime);

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

    // 사이드뷰에서 D키(화면 오른쪽)로 나아가는 월드 방향이다. 기본은 +X이고,
    // Z 방향으로 뻗은 통로처럼 다른 방향으로 진행해야 하는 구간에서는 SideViewAxisZone이 바꾼다.
    [ReadOnly, ShowInInspector, LabelText("사이드뷰 진행 축")]
    public Vector3 SideViewAxis { get; private set; } = Vector3.right;

    // 사이드뷰 진행 축이 바뀔 때 알린다. SideViewCamera가 구독해 카메라를 통로 옆으로 돌린다.
    public event System.Action<Vector3> OnSideViewAxisChanged;

    // 사이드뷰에서 고정하는 "깊이" 방향(진행 축에 수직인 수평 방향)이다. 카메라가 바라보는 방향과 같다.
    // 기본 축(+X)이면 +Z가 되어, 기존처럼 Z 좌표를 라인으로 고정하는 것과 똑같이 동작한다.
    private Vector3 SideViewDepthAxis => Vector3.Cross(SideViewAxis, Vector3.up);

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    public bool IsGrounded { get; private set; } = true;

    [ReadOnly, ShowInInspector, LabelText("점프 버튼 유지 상승 중")]
    private bool _isJumpHolding;
    // true인 동안은 중력 대신 상승 속도를 유지한다.

    [ReadOnly, ShowInInspector, LabelText("얼음 미끄러짐 중")]
    public bool IsSlipping => _slipIce != null;

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

    // 베개 공격 중 이동 정지 상태다. SwordAttack이 공격 시작 시 켜고 연출이 끝나면 끈다.
    // 땅에 서 있을 때만 이동을 막고, 공중에서는 좌우 이동을 그대로 허용한다.
    private bool _isAttackLocked;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private Vector2 _moveInput;

    // 화살표 입력이 이 값보다 커야 조준 방향으로 인정한다. 게임패드 스틱이 살짝 흔들리는 정도는 무시하기 위한 값이다.
    private const float AimInputThreshold = 0.1f;

    // 사이드뷰에서 마지막으로 바라본 좌우 방향이다. +1이면 진행 축 앞(기본 +X), -1이면 뒤(-X)를 본다.
    // 좌우 화살표를 떼도 이 방향을 유지해야 선풍기/횃불이 계속 같은 쪽으로 나간다.
    private float _sideFacingSign = 1f;

    // 플레이어가 직접 한 점프로 올라가는 중인지다. 버튼을 떼서 상승을 끊는 처리는 이 점프에만 적용해,
    // 선풍기 블라스트·바람·뜨는 상자로 솟구치는 중에 버튼을 떼도 영향이 없게 한다.
    private bool _isPlayerJumpRising;
    private float _jumpHoldTimer;
    // 이번 점프에서 버튼을 누르고 상승을 유지한 시간이다.

    // 사이드뷰에서 플레이어가 유지할 라인(깊이 좌표)이다. 사이드뷰 동안은 항상 이 라인으로 되돌아간다.
    // 기본 축(+X)에서는 Z 좌표 그대로이고, -Z 통로처럼 축이 바뀌면 깊이 방향(X) 좌표가 된다.
    private float _laneZ;

    // 미끄러짐을 적용할 얼음 발판이다. null이면 일반 바닥처럼 즉시 반응하는 이동을 한다.
    // 착지 중에는 발밑 얼음으로 갱신되고, 공중에서는 마지막 값을 유지해 점프 중에도 관성이 이어진다.
    private IcePlatform _slipIce;
    // 이번 Move() 중 OnControllerColliderHit에서 발밑으로 감지한 미끄러운 얼음 발판이다.
    private IcePlatform _groundIceThisMove;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // 사이드뷰에서는 항상 이 Z 라인으로 되돌아가므로, 씬에 배치된 위치를 첫 라인으로 삼는다.
        // (초기값 0으로 두면 시작하자마자 Z 0으로 끌려간다. 이후 순간이동은 Respawn()이 라인을 갱신한다.)
        _laneZ = transform.position.z;

        // 씬에 배치된 캐릭터가 바라보는 좌우 방향을 처음 조준 방향으로 삼는다.
        // 기본값(+1)으로 두면 왼쪽을 보고 시작하는 씬에서 첫 프레임에 몸이 오른쪽으로 홱 돌아간다.
        _sideFacingSign = Vector3.Dot(transform.forward, SideViewAxis) >= 0f ? 1f : -1f;

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

        // 땅에 있을 때만 점프한다. (공중 점프는 쓰지 않는다)
        if (!IsGrounded) return;

        // 누르는 순간 상승을 시작하고, 버튼을 떼거나 최대 유지 시간이 지날 때까지 UpdateJumpHold()가 이어간다.
        _verticalVelocity = jumpRiseSpeed;
        _isJumpHolding = true;
        _isPlayerJumpRising = true;
        _jumpHoldTimer = 0f;
    }

    // 점프 버튼을 누르고 있는 동안 상승 속도를 유지하고, 떼면 상승을 끊어 낮게 뛰게 한다.
    // 버튼 상태는 performed 이벤트가 아니라 매 프레임 IsPressed()로 확인해, 떼는 순간을 놓치지 않게 한다.
    private void UpdateJumpHold()
    {
        if (!_isPlayerJumpRising) return;

        bool isHeld = _jumpAction.IsPressed();

        if (_isJumpHolding)
        {
            _jumpHoldTimer += Time.deltaTime;

            // 최소 유지 시간 전에 뗐다면 최소 높이까지는 올라간 뒤에 끊는다.
            if (!isHeld && _jumpHoldTimer >= JumpHoldMinTime)
            {
                CutJump();
                return;
            }

            if (_jumpHoldTimer >= JumpHoldMaxTime)
                _isJumpHolding = false;
            // 최대 유지 시간이 지나면 중력을 다시 받아, 정점까지 자연스럽게 감속하며 올라간다.
            else
                _verticalVelocity = jumpRiseSpeed;
        }
        else if (!isHeld)
        {
            // 감속하며 올라가는 중에 떼도 바로 꺾이게 한다.
            CutJump();
            return;
        }

        // 정점을 지나 내려가기 시작하면 이번 점프 처리는 끝난다.
        if (_verticalVelocity <= 0f)
            EndPlayerJump();
    }

    // 버튼을 떼서 남은 상승 속도를 끊는다.
    private void CutJump()
    {
        _verticalVelocity = Mathf.Min(_verticalVelocity, jumpReleaseVelocity);
        EndPlayerJump();
    }

    // 플레이어 점프 상태를 끝낸다. 외부 힘(블라스트·바람·발판)이 수직 속도를 덮어쓸 때도 호출해,
    // 이후에 버튼을 떼도 그 힘으로 솟구치는 움직임을 끊지 않게 한다.
    private void EndPlayerJump()
    {
        _isJumpHolding = false;
        _isPlayerJumpRising = false;
    }

    private void Update()
    {
        _moveInput = _moveAction.ReadValue<Vector2>();

        // 도구 휠(Z)이나 일시정지 메뉴가 열려 시간이 멈춘 동안에는 화살표 입력을 무시한다.
        // 휠에서는 화살표로 슬롯을 고르는데, 이 입력이 캐릭터 회전/조준까지 바꾸지 않게 하기 위해서다.
        if (Time.timeScale == 0f)
            _moveInput = Vector2.zero;

        IsGrounded = _cc.isGrounded;

        // 접지 중이고 내려가는 중이면 작은 아래 힘을 유지해 경사면 미끄러짐을 방지한다.
        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        HandleMove();
        HandleFacing();
        // 선풍기/횃불이 사용하는 FacingDirection을 매 프레임 화살표 입력으로 갱신한다.
        // 이동 방향 기반 회전(HandleMove) 이후에 호출해 조준 방향이 몸통 회전에 최종 반영되게 한다.
        UpdateJumpHold();
        ApplyGravity();
        ApplyFallSpeedLimit();
        ApplyWindVertical();
        ApplyPlatformVertical();
        DecayRecoil();

        // 수평 속도 성분을 합산하고 수직 속도를 Y에 적용해 최종 이동한다.
        Vector3 horizontal = _moveVelocity + _recoilVelocity + _blastVelocity + _windVelocity;

        // 사이드뷰에서는 반동/바람 등 어떤 원인이든 깊이 방향 이동을 버려 캐릭터가 앞뒤로 벗어나지 않게 한다.
        // 대신 사이드뷰 라인으로 돌아가는 속도만 허용한다(탑다운에서 나온 직후, 비스듬한 벽에 미끄러져 라인에서 밀린 경우).
        // 기본 축(+X)이면 깊이 방향이 +Z라서 "horizontal.z = 복귀 속도"와 같은 계산이다.
        if (CurrentViewMode == ViewMode.SideView)
        {
            Vector3 depthAxis = SideViewDepthAxis;
            horizontal -= depthAxis * Vector3.Dot(horizontal, depthAxis);
            horizontal += depthAxis * GetLaneReturnVelocityZ();
        }

        Vector3 finalVelocity = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
        _groundIceThisMove = null;
        _cc.Move(finalVelocity * Time.deltaTime);

        // Move() 호출 이후 접지 상태를 갱신해 같은 프레임 내 다른 스크립트에 최신 값을 제공한다.
        IsGrounded = _cc.isGrounded;

        // 점프 중 천장에 머리가 닿으면 상승을 끊는다. 누르는 동안 상승 속도를 유지하므로,
        // 이 처리가 없으면 버튼을 떼거나 유지 시간이 끝날 때까지 천장에 붙어 있게 된다.
        if (_isPlayerJumpRising && (_cc.collisionFlags & CollisionFlags.Above) != 0)
        {
            _verticalVelocity = 0f;
            EndPlayerJump();
        }

        // 착지해 있으면 발밑이 얼음인지로 미끄러짐 여부를 정한다. (일반 바닥에 닿으면 null이 되어 즉시 조작감 복귀)
        // 공중이면 갱신하지 않아, 얼음에서 점프했을 때 착지 전까지 관성이 유지된다.
        if (IsGrounded)
            _slipIce = _groundIceThisMove;
    }

    // CharacterController.Move() 도중 콜라이더에 닿을 때마다 호출된다.
    // 접지 중에는 매 프레임 아래로 누르므로(-2f) 발밑 바닥과도 계속 호출되어, 별도 트리거 없이 발판을 판정할 수 있다.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // 법선이 위를 향하면(0.5 이상 ≒ 60도 이하 경사) 바닥, 아니면 벽/천장으로 본다.
        if (hit.normal.y > 0.5f)
        {
            if (hit.collider.TryGetComponent(out IcePlatform ice) && ice.IsSlippery)
                _groundIceThisMove = ice;
            return;
        }

        // 얼음에서 미끄러지다 벽에 부딪히면 벽 쪽으로 향하는 속도를 없앤다.
        // 그러지 않으면 남은 관성이 계속 벽을 밀어서, 반대로 방향을 바꿀 때 한동안 멈춰 있는 것처럼 느껴진다.
        // (일반 바닥에서는 _moveVelocity가 매 프레임 새로 대입되므로 영향이 없다.)
        Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
        float intoWall = Vector3.Dot(_moveVelocity, wallNormal);
        if (intoWall < 0f)
            _moveVelocity -= wallNormal * intoWall;
    }

    private void HandleMove()
    {
        // 공격 중 이동 정지는 접지 중일 때만 적용해, 공중 공격 중에는 좌우 이동이 끊기지 않게 한다.
        if (_isBound || _isFrozen || (_isAttackLocked && IsGrounded))
        {
            // 얼음 위라면 즉시 멈추지 않고 미끄러지다 멈춘다.
            ApplyMoveVelocity(Vector3.zero, false);
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

        // 사이드뷰에서는 좌우 입력을 진행 축 방향으로 쓴다. 기본 축(+X)이면 위와 같은 값이고,
        // -Z 통로에서는 D키가 통로 안쪽(-Z)으로 나아가게 된다.
        if (CurrentViewMode == ViewMode.SideView)
            moveDir = SideViewAxis * xInput;

        // 대각선 이동이 더 빨라지는 것 방지
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        bool hasInput = moveDir.sqrMagnitude > 0.01f;
        ApplyMoveVelocity(moveDir * (moveSpeed * _speedMultiplier), hasInput);

        // 이동하는 방향을 바라보게 함
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDir, Vector3.up);

            transform.rotation = targetRotation;
        }
    }

    // 목표 이동 속도를 _moveVelocity에 반영한다.
    // 일반 바닥: 즉시 대입해 기존과 똑같이 반응한다.
    // 얼음(공중 관성 포함): MoveTowards로 목표 속도를 천천히 따라가 미끄러지게 만든다.
    private void ApplyMoveVelocity(Vector3 targetVelocity, bool hasInput)
    {
        if (_slipIce == null)
        {
            _moveVelocity = targetVelocity;
            return;
        }

        // 지금 움직이는 방향 쪽으로 입력 중이면 가속도, 입력이 없거나 반대로 누르면 감속도(마찰)를 쓴다.
        // Dot >= 0: 현재 속도와 목표 방향이 90도 이내라 같은 쪽으로 가속하는 중이라는 뜻이다.
        bool isAccelerating = hasInput && Vector3.Dot(targetVelocity, _moveVelocity) >= 0f;
        float rate = isAccelerating ? _slipIce.Acceleration : _slipIce.Deceleration;

        // Time.deltaTime을 곱해 프레임레이트와 무관하게 초당 rate만큼만 속도가 변하게 한다.
        _moveVelocity = Vector3.MoveTowards(_moveVelocity, targetVelocity, rate * Time.deltaTime);
    }

    private void HandleFacing()
    {
        if (CurrentViewMode == ViewMode.TopDown)
        {
            HandleTopDownFacing();
            return;
        }

        // 사이드뷰: 좌우 화살표로 몸 방향(진행 축의 앞/뒤)을 정하고, 위/아래 화살표로 위아래 조준을 더한다.
        // 가을 디버프(방향 반전) 중에는 이동 방향(HandleMove)과 똑같이 좌우를 뒤집어, 움직이는 쪽을 바라보게 한다.
        float xInput = _isReversed ? -_moveInput.x : _moveInput.x;
        float yInput = _moveInput.y;

        // 좌우를 누르지 않을 때도 마지막으로 바라본 방향을 유지해야 하므로 부호만 기억해 둔다.
        if (Mathf.Abs(xInput) > AimInputThreshold)
            _sideFacingSign = Mathf.Sign(xInput);

        Vector3 bodyDir = SideViewAxis * _sideFacingSign;

        // 위/아래를 누르면 조준에 Y 성분을 넣는다.
        // 좌우도 함께 누르면 45도 대각선, 위/아래만 누르면 바로 위/아래를 조준한다.
        Vector3 aimDir = bodyDir;
        if (Mathf.Abs(yInput) > AimInputThreshold)
        {
            Vector3 vertical = Vector3.up * Mathf.Sign(yInput);
            aimDir = Mathf.Abs(xInput) > AimInputThreshold ? (bodyDir + vertical).normalized : vertical;
        }

        FacingDirection = aimDir;
        // FacingDirection에 Y 성분이 포함되어 선풍기/횃불이 위아래 각도로도 작동한다.

        // 캐릭터 몸통은 진행 축의 앞/뒤 방향만 전환한다. 위아래를 조준해도 몸은 기울이지 않는다.
        // 카메라 쪽(깊이 방향)을 바라보게 하면 등/정면이 뒤바뀌어 Z축 회전처럼 보이는 문제가 생긴다.
        transform.rotation = Quaternion.LookRotation(bodyDir, Vector3.up);
    }

    private void HandleTopDownFacing()
    {
        // 탑다운: 화살표로 이동하는 방향(8방향)을 바라본다. 입력이 없으면 마지막 방향을 유지한다.
        // HandleMove와 같은 규칙으로 가을 디버프(방향 반전) 중에는 앞뒤/좌우를 모두 뒤집는다.
        float xInput = _isReversed ? -_moveInput.x : _moveInput.x;
        float zInput = _isReversed ? -_moveInput.y : _moveInput.y;
        Vector3 dir = new Vector3(xInput, 0f, zInput);
        // Y 성분은 0으로 두어 바닥과 평행한 XZ 방향만 쓴다. 탑다운에선 위아래 조준을 쓰지 않는다.

        if (dir.sqrMagnitude > AimInputThreshold * AimInputThreshold)
        {
            FacingDirection = dir.normalized;
            // 사이드뷰와 달리 몸통을 조준 방향으로 360도 자유롭게 돌린다.
            transform.rotation = Quaternion.LookRotation(FacingDirection, Vector3.up);

            // 사이드뷰로 돌아갔을 때 탑다운에서 마지막으로 바라보던 좌우 방향을 이어받는다.
            float side = Vector3.Dot(FacingDirection, SideViewAxis);
            if (Mathf.Abs(side) > 0.01f)
                _sideFacingSign = Mathf.Sign(side);
        }
    }

    // 사이드뷰 동안 항상 호출되어 Z 라인을 유지한다.
    // 탑다운에서 막 나왔을 때뿐 아니라, 비스듬한 콜라이더(부서진 나무 등)에 비비다가
    // CharacterController의 미끄러짐 처리로 Z가 밀려난 경우에도 다음 이동에서 라인으로 되돌린다.
    private float GetLaneReturnVelocityZ()
    {
        // 깊이 방향으로 라인까지 남은 거리다. 기본 축이면 깊이 방향이 +Z라서 "라인Z - 현재Z"와 같다.
        float deltaZ = _laneZ - Vector3.Dot(transform.position, SideViewDepthAxis);
        if (Mathf.Abs(deltaZ) < 0.01f) return 0f;

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
        // 사이드뷰로 돌아오면 Update의 GetLaneReturnVelocityZ()가 이 라인으로 복귀시킨다. transform.position을 직접 바꾸지 않고
        // CharacterController.Move()로 이동시키므로 벽을 뚫고 순간이동하지 않는다.

        // 이전 모드에서 받은 선풍기 반동이 새 모드에서 엉뚱한 방향으로 이어지지 않게 초기화한다.
        _recoilVelocity = Vector3.zero;
        _blastVelocity = Vector3.zero;

        OnViewModeChanged?.Invoke(mode);
    }

    // SideViewAxisZone이 구역 안에서 매 물리 프레임 호출하고, 구역을 나가면 기본 축(+X)으로 되돌릴 때도 호출한다.
    // axis: 사이드뷰에서 D키로 나아갈 수평 방향. laneDepth: 깊이 방향으로 고정할 라인 좌표.
    public void SetSideViewAxis(Vector3 axis, float laneDepth)
    {
        _laneZ = laneDepth;
        // 라인은 매번 갱신해, 다른 구역(ViewModeZone 등)이 같은 프레임에 덮어쓴 라인도 바로잡는다.

        if (SideViewAxis == axis) return;

        SideViewAxis = axis;

        // 이전 축 기준으로 받은 선풍기 반동이 새 축에서 엉뚱한 방향으로 이어지지 않게 초기화한다.
        _recoilVelocity = Vector3.zero;
        _blastVelocity = Vector3.zero;

        OnSideViewAxisChanged?.Invoke(axis);
    }

    // PlayerHealth.Respawn()이 순간이동 직후 호출한다.
    // CharacterController를 끈 채 옮기면 트리거 이탈 이벤트가 오지 않으므로, 현재 위치가 ViewModeZone 안인지 직접 검사한다.
    public void SyncViewModeToPosition()
    {
        // 순간이동(리스폰) 후에는 얼음에서 받던 관성을 이어가지 않는다.
        _slipIce = null;
        _moveVelocity = Vector3.zero;


        // 발끝(pivot)은 구역 바닥 경계와 겹칠 수 있어 캐릭터 몸통 중심에서 검사한다.
        // QueryTriggerInteraction.Collide: ViewModeZone은 트리거 콜라이더라 이 옵션이 있어야 검사에 잡힌다.
        Vector3 center = transform.TransformPoint(_cc.center);
        Collider[] hits = Physics.OverlapSphere(center, 0.1f, ~0, QueryTriggerInteraction.Collide);

        SideViewAxisZone axisZone = null;
        foreach (Collider hit in hits)
        {
            ViewModeZone zone = hit.GetComponent<ViewModeZone>();
            if (zone != null)
            {
                SetViewMode(ViewMode.TopDown, zone.GetLaneZ());
                return;
            }

            if (axisZone == null)
                axisZone = hit.GetComponent<SideViewAxisZone>();
        }

        // 리스폰 위치가 통로(SideViewAxisZone) 안이면 그 구역의 축/라인을, 아니면 기본 축(+X)과 현재 Z를 쓴다.
        // CharacterController를 끈 채 옮기면 트리거 이벤트가 오지 않으므로 여기서 직접 맞춘다.
        if (axisZone != null)
            SetSideViewAxis(axisZone.Axis, axisZone.LaneDepth);
        else
            SetSideViewAxis(Vector3.right, transform.position.z);

        float laneDepth = Vector3.Dot(transform.position, SideViewDepthAxis);
        SetViewMode(ViewMode.SideView, axisZone != null ? axisZone.LaneDepth : laneDepth);
        // 구역 밖으로 리스폰했다면 그 위치가 곧 사이드뷰 라인이므로, 이전 라인으로 끌려가지 않게 라인을 갱신한다.
        // (이미 사이드뷰였다면 SetViewMode가 무시되므로 여기서 직접 정리한다.)
        _laneZ = axisZone != null ? axisZone.LaneDepth : laneDepth;
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
        // 점프 버튼을 누르고 상승을 유지하는 동안은 중력을 받지 않는다.
        if (!IsGrounded && !_isJumpHolding)
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
        {
            _verticalVelocity = velocity.y;
            EndPlayerJump();
        }
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

    // 베개 공격 중 이동을 막는다. SwordAttack이 공격 시작/종료 시 호출한다.
    public void SetAttackLock(bool locked)
    {
        _isAttackLocked = locked;
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
        // 빙결되면 누르고 있던 점프 상승도 멈추고 중력으로 떨어지게 한다.
        if (frozen) _isJumpHolding = false;
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
        {
            _verticalVelocity = _windVelocity.y;
            EndPlayerJump();
        }
    }

    private void ApplyPlatformVertical()
    {
        // 뜨는 상자 위에 있는 동안은 상자의 상승/하강 속도를 그대로 수직 속도에 반영해
        // 플레이어가 상자와 함께 오르내리게 한다.
        if (_platformVelocity.y != 0f)
        {
            _verticalVelocity = _platformVelocity.y;
            EndPlayerJump();
        }
    }
}
