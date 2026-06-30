using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

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
    private float _verticalVelocity;

    // 선풍기 차징 중 이동속도를 줄이기 위한 배율 (기본 1.0, 차징 시 0.3)
    private float _speedMultiplier = 1f;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private Vector2 _moveInput;

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
        // 접지 상태일 때만 점프할 수 있다.
        if (IsGrounded)
            _verticalVelocity = jumpForce;
    }

    private void Update()
    {
        _moveInput = _moveAction.ReadValue<Vector2>();
        IsGrounded = _cc.isGrounded;

        // 접지 중이고 내려가는 중이면 작은 아래 힘을 유지해 경사면 미끄러짐을 방지한다.
        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        HandleMove();
        HandleFacing();
        ApplyGravity();
        ApplyFallSpeedLimit();
        DecayRecoil();

        // 수평 속도 성분을 합산하고 수직 속도를 Y에 적용해 최종 이동한다.
        Vector3 horizontal = _moveVelocity + _recoilVelocity + _blastVelocity + _windVelocity;
        Vector3 finalVelocity = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
        _cc.Move(finalVelocity * Time.deltaTime);

        // Move() 호출 이후 접지 상태를 갱신해 같은 프레임 내 다른 스크립트에 최신 값을 제공한다.
        IsGrounded = _cc.isGrounded;
    }

    private void HandleMove()
    {
        // 사이드뷰: 입력 X → 월드 X축 이동만 사용한다. Z축 이동은 없다.
        Vector3 moveDir = new Vector3(_moveInput.x, 0f, 0f);
        _moveVelocity = moveDir * (moveSpeed * _speedMultiplier);
    }

    private void HandleFacing()
    {
        // Mouse.current가 없으면 (게임패드 전용 환경 등) 처리를 건너뛴다.
        if (Mouse.current == null) return;

        // 카메라에서 마우스 스크린 좌표로 광선을 발사한다.
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

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

    // 선풍기 차징 중 이동속도를 제한하기 위한 배율을 설정한다.
    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
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
}
