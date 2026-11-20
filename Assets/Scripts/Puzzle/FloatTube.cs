using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 물 위에 떠 있는 수영 튜브다. 플레이어가 Interact(Space 꾹)로 타면 튜브 구멍에 앉은 채 합쳐지고,
// 이후 선풍기 반동(SetRecoil/SetBlast)이 플레이어 대신 튜브를 밀어 물 위를 미끄러진다.
// 튜브는 수면 높이(Y)에 고정된 Rigidbody라서 XZ 평면으로만 움직이고, 벽에는 살짝 튕긴다.
// 도착 지점 트리거(arrivalDockTrigger)에 들어가면 자동으로 내려준다. "도착하면 자동 하차"를 끄면 안내를 띄우고 Interact로 내린다.
[RequireComponent(typeof(Rigidbody))]
public class FloatTube : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("플레이어")]
    private PlayerController player;
    // Player 태그가 붙은 PlayerController를 연결한다. 탑승 거리 계산과 탑승/하차 호출에 쓴다.

    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Assets/InputSystem_Actions 에셋을 연결한다. Player 맵의 Interact 액션으로 탑승한다.

    [SerializeField, LabelText("좌석")]
    private Transform seat;
    // 튜브 자식 빈 오브젝트다. 플레이어의 발(루트)이 이 위치에 놓이므로, 링보다 약간 아래에 둬서 몸이 구멍에 잠긴 것처럼 보이게 한다.

    [SerializeField, LabelText("튜브 모델")]
    private Transform visualModel;
    // 메시를 가진 자식이다. 탑승 펀치/블라스트 기울기 연출만 이 오브젝트에 적용해 물리 충돌체와 분리한다.

    [SerializeField, LabelText("도착 지점 트리거")]
    private Collider arrivalDockTrigger;
    // 맞은편 선착장에 둔 Is Trigger 콜라이더다. 탑승한 채 튜브가 여기에 들어오면 자동으로 내린다.

    [SerializeField, LabelText("하차 위치")]
    private Transform arrivalExitPoint;
    // 내린 뒤 플레이어가 서는 위치다. 도착 선착장 바닥 위에 둔다. 아래 "하차 구역"을 연결하면 그쪽이 우선이다.

    [SerializeField, LabelText("도착하면 자동 하차")]
    private bool autoDismountOnArrival = true;
    // 켜면 기존처럼 도착 트리거에 닿는 순간 자동으로 내린다.
    // 끄면 도착 트리거 안에 있는 동안 "하차 안내"가 뜨고, 플레이어가 Interact(Space 꾹)를 눌러야 내린다.

    [SerializeField, LabelText("하차 안내 UI"), HideIf(nameof(autoDismountOnArrival))]
    private CanvasGroup dismountPrompt;
    // 수동 하차일 때 머리 위에 띄울 World Space Canvas의 CanvasGroup이다. 탑승 안내와 같은 방식으로 따라다닌다.

    [SerializeField, LabelText("하차 구역 (선택)")]
    private Collider dismountArea;
    // 도착 쪽 땅 위를 덮는 Is Trigger 콜라이더다. 연결하면 고정 위치 대신 "튜브와 가장 가까운 이 구역의 땅"에 내려,
    // 튜브 바로 옆에서 내린 것처럼 보인다. 비워 두면 위의 "하차 위치"에 내린다.

    [SerializeField, LabelText("하차 땅 안쪽 여유"), ShowIf("@dismountArea != null || (landingSpots != null && landingSpots.Length > 0)")]
    private float dismountInset = 0.6f;
    // 땅 가장자리(물과 맞닿은 끝)에 딱 붙어 서면 다시 물로 떨어질 수 있어, 이만큼 땅 안쪽으로 들여서 내린다.
    // "하차 구역"과 "오르내리는 땅 목록" 둘 다 이 값을 쓴다.

    [Title("오르내리는 땅 (선택)")]
    [SerializeField, LabelText("오르내리는 땅 목록")]
    private Collider[] landingSpots;
    // 선착장·섬처럼 "여기서만 타고 내린다"고 정한 땅의 (Is Trigger가 아닌) 콜라이더를 넣는다.
    // 비워 두면 기존 방식(탑승 거리/탑승 구역, 도착 트리거)으로만 동작한다.
    // 넣으면 1) 이 땅 위에 선 채 튜브가 그 땅 옆에 있을 때만 탈 수 있고, 2) 탑승 중 튜브가 이 땅 옆에 오면 하차 안내가 뜨고 내릴 수 있다.

    [SerializeField, LabelText("땅 옆 판정 거리"), ShowIf("@landingSpots != null && landingSpots.Length > 0")]
    private float landingDistance = 3f;
    // 튜브 중심과 땅 가장자리 사이의 수평 거리가 이 값 이하이면 "땅 옆에 댔다"고 본다. 클수록 조금 떨어져 있어도 타고 내린다.

    [SerializeField, LabelText("튜브 재배치 지점들")]
    private Transform[] respawnPoints;
    // 전복해서 섬 체크포인트로 리스폰할 때, 튜브도 그 섬 옆 물 위에 다시 나타나야 플레이어가 고립되지 않는다.
    // 시작 지점/섬마다 수면 위 빈 오브젝트를 만들어 넣는다. 복귀 위치(XZ)에서 가장 가까운 지점이 쓰이고, 비워 두면 처음 자리로 돌아간다.

    [SerializeField, LabelText("탑승 안내 UI (선택)")]
    private CanvasGroup boardPrompt;
    // 탑승 가능 범위에 들어오면 페이드로 나타나는 안내다. 비워 두면 안내 없이 동작한다.
    // World Space Canvas에 붙은 CanvasGroup을 연결하면 플레이어 머리 위를 따라다니며 카메라를 바라본다.

    [SerializeField, LabelText("안내 높이"), ShowIf(nameof(boardPrompt))]
    private float promptHeight = 2.3f;
    // 플레이어 발(루트) 기준으로 안내를 띄울 높이(m)다. 클수록 머리 위 더 높이 뜬다.

    [Title("탑승")]
    [SerializeField, LabelText("탑승 가능 거리")]
    private float boardRange = 2.5f;
    // 플레이어와 튜브의 수평(XZ) 거리가 이 값 이하일 때만 탑승할 수 있다. 클수록 멀리서도 탄다.

    [SerializeField, LabelText("탑승 구역 (선택)")]
    private Collider boardArea;
    // 출발 선착장(StartDock) 위를 덮는 Is Trigger 콜라이더다. 플레이어가 이 안에 있으면 튜브와 조금 떨어져 있어도 탈 수 있다.
    // 비워 두면 기존처럼 "탑승 가능 거리"로만 판정한다.

    [SerializeField, LabelText("구역 탑승 최대 거리"), ShowIf(nameof(boardArea))]
    private float areaBoardMaxDistance = 9f;
    // 탑승 구역 안이어도 튜브가 이 거리(XZ)보다 멀리 떠내려가 있으면 타지 않는다.
    // 튜브가 다른 섬에 가 있는데 선착장에서 순간이동해 타는 것을 막기 위한 값이다.

    [SerializeField, LabelText("탑승 구역 안에서만 탑승"), ShowIf(nameof(boardArea))]
    private bool boardOnlyInArea;
    // 켜면 "탑승 가능 거리"는 무시하고 탑승 구역 안에서만 탈 수 있다.
    // 도착 쪽에서 튜브 바로 옆에 내린 뒤 다시 타고 되돌아가지 못하게 하는 한 방향 튜브에 켠다.

    [Title("물리")]
    [SerializeField, LabelText("물 저항 (Drag)")]
    private float waterDrag = 1.2f;
    // 클수록 밀어도 빨리 멈춘다. 작을수록 오래 미끄러진다.

    [SerializeField, LabelText("최대 속도")]
    private float maxSpeed = 9f;
    // 수평 속도가 이 값을 넘지 않게 제한한다. 블라스트 여러 번에 너무 빨라지는 것을 막는다.

    [SerializeField, LabelText("일반 반동 배율")]
    private float pushMultiplier = 1f;
    // 선풍기 일반 바람 반동 속도를 가속도로 바꿀 때 곱하는 값이다. 클수록 천천히 쏴도 잘 가속된다.

    [SerializeField, LabelText("반동 감쇠")]
    private float pushDecay = 20f;
    // 선풍기를 멈춘 뒤 밀던 힘이 0으로 줄어드는 속도(초당)다. FanTool은 쏘는 동안만 반동을 호출하므로 이게 있어야 힘이 자연스럽게 끊긴다.

    [SerializeField, LabelText("블라스트 배율")]
    private float blastMultiplier = 0.35f;
    // 블라스트 반동(10~40)에 곱해 튜브가 받는 순간 속도로 바꾼다. 예: 40이면 약 14m/s.

    [SerializeField, LabelText("벽 튕김"), Range(0f, 1f)]
    private float wallBounciness = 0.3f;
    // 벽에 부딪혔을 때 튕겨 나오는 정도다. 0이면 딱 멈추고 1에 가까울수록 크게 튕긴다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("탑승 펀치 세기")]
    private float boardPunchStrength = 0.25f;
    // 탑승하는 순간 튜브 모델이 아래로 꺼졌다 올라오는 거리(m)다.

    [SerializeField, LabelText("탑승 펀치 시간")]
    private float boardPunchDuration = 0.4f;

    [SerializeField, LabelText("블라스트 기울기 각도")]
    private float blastTiltAngle = 15f;
    // 블라스트를 맞으면 튜브 모델이 진행 방향으로 기우뚱하는 각도(도)다.

    [SerializeField, LabelText("블라스트 기울기 시간")]
    private float blastTiltDuration = 0.5f;

    [SerializeField, LabelText("Ease")]
    private Ease punchEase = Ease.OutQuad;

    [SerializeField, LabelText("안내 페이드 시간")]
    private float promptFadeDuration = 0.2f;

    // 장애물이 탑승자의 도구(우산 등)를 확인할 때 쓴다.
    public PlayerController Player => player;

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("탑승 중")]
    public bool IsMounted => player != null && player.IsMounted && player.MountedTube == this;
    // 씬에 튜브가 여러 개면 "플레이어가 어느 튜브든 탔는지"가 아니라 "이 튜브에 탔는지"를 봐야 한다.
    // 그러지 않으면 다른 튜브에 타는 순간, 이 튜브가 자기가 탑승 중이라고 착각해 엉뚱한 곳에 내려 버린다.

    [ShowInInspector, ReadOnly, LabelText("현재 속도")]
    private float CurrentSpeed => _rigidbody != null
        ? new Vector3(_rigidbody.linearVelocity.x, 0f, _rigidbody.linearVelocity.z).magnitude
        : 0f;

    private Rigidbody _rigidbody;
    private InputAction _interactAction;
    private Vector3 _push;
    // 선풍기 일반 반동이 만든 현재 가속도다. FixedUpdate에서 적용하고 서서히 줄인다.
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Vector3 _modelStartLocalPosition;
    private Quaternion _modelStartLocalRotation;
    private bool _promptVisible;
    private Tween _boardTween;
    private Tween _tiltTween;
    private Tween _promptTween;
    private bool _dismountPromptVisible;
    private Tween _dismountPromptTween;
    private bool _isInArrivalDock;

    [ShowInInspector, ReadOnly, LabelText("지금 붙어 있는 땅")]
    private Collider _dockedLanding;
    // 매 프레임 갱신하는 "튜브 바로 옆에 있는 오르내리는 땅"이다. 없으면 null이다. 하차 안내·하차 위치에 함께 쓴다.
    // 튜브가 도착 트리거 안에 있는지다. 수동 하차일 때 이 동안만 내릴 수 있다.
    private Transform _cameraTransform;
    // 안내가 바라볼 메인 카메라다. 매 프레임 Camera.main을 찾지 않도록 Awake에서 한 번 저장해 둔다.
    private CharacterController _playerCC;
    // 탑승 구역 안/밖 판정에 플레이어 몸통 중심을 쓰기 위해 저장해 둔다.

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        // 수면 위를 미끄러지는 물체이므로 중력을 끄고 Y 이동/회전을 잠가 XZ 평면으로만 움직이게 한다.
        _rigidbody.useGravity = false;
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        // 블라스트 속도가 빨라 벽을 뚫고 지나가지 않도록 연속 충돌 검사를 쓴다.
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rigidbody.linearDamping = waterDrag;

        // 벽에 부딪히면 살짝 튕기도록 런타임에 물리 머티리얼을 만들어 콜라이더에 붙인다.
        Collider tubeCollider = GetComponent<Collider>();
        if (tubeCollider != null)
        {
            tubeCollider.material = new PhysicsMaterial("FloatTube_Bounce")
            {
                bounciness = wallBounciness,
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }

        _startPosition = transform.position;
        _startRotation = transform.rotation;
        if (visualModel != null)
        {
            _modelStartLocalPosition = visualModel.localPosition;
            _modelStartLocalRotation = visualModel.localRotation;
        }

        if (boardPrompt != null)
            boardPrompt.alpha = 0f;

        if (dismountPrompt != null)
            dismountPrompt.alpha = 0f;

        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;

        if (player != null)
            _playerCC = player.GetComponent<CharacterController>();

        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
    }

    private void Start()
    {
        // 물 위 체크포인트에서 불러오면 튜브가 시작 위치에 그대로 있어 플레이어가 고립되므로, 체크포인트 근처 재배치 지점으로 옮긴다.
        if (SaveManager.Instance != null && SaveManager.Instance.SavedOnTube
            && CheckpointManager.HasCheckpoint
            && CheckpointManager.CurrentCheckpointScene == gameObject.scene.name)
        {
            ResetNear(CheckpointManager.CurrentCheckpointPosition);
        }
    }

    private void OnEnable()
    {
        _interactAction.Enable();
        _interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        _interactAction.performed -= OnInteractPerformed;
        _interactAction.Disable();
    }

    private void OnDestroy()
    {
        _boardTween?.Kill();
        _tiltTween?.Kill();
        _promptTween?.Kill();
        _dismountPromptTween?.Kill();
    }

    private void Update()
    {
        if (player == null) return;

        // 튜브가 지금 어느 "오르내리는 땅" 옆에 있는지 먼저 갱신한다. 탑승/하차 판정이 모두 이 값을 쓴다.
        _dockedLanding = FindDockedLanding();

        // 탑승 중에는 안내를 숨기고, 아니면 범위 안일 때만 보여준다.
        SetPromptVisible(!player.IsMounted && IsPlayerInRange());

        // 수동 하차 튜브는 탑승한 채 도착 트리거 안이거나 오르내리는 땅 옆일 때만 하차 안내를 보여준다.
        SetDismountPromptVisible(CanDismountHere());
    }

    private bool HasLandingSpots => landingSpots != null && landingSpots.Length > 0;

    // 튜브와 가장 가까운 오르내리는 땅을 찾고, 판정 거리 안이면 돌려준다. 목록이 비었거나 모두 멀면 null이다.
    private Collider FindDockedLanding()
    {
        if (!HasLandingSpots) return null;

        Collider nearest = null;
        float nearestSqr = landingDistance * landingDistance;
        foreach (Collider spot in landingSpots)
        {
            if (spot == null || !spot.enabled) continue;

            // ClosestPoint는 땅 콜라이더 표면에서 튜브와 가장 가까운 점이다. 높이 차이는 빼고 수평 거리만 비교한다.
            Vector3 offset = spot.ClosestPoint(transform.position) - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= nearestSqr)
            {
                nearestSqr = offset.sqrMagnitude;
                nearest = spot;
            }
        }
        return nearest;
    }

    // 플레이어 발밑 바로 아래 땅이 이 콜라이더인지 확인한다. "그 땅 위에 서 있어야 탈 수 있다"는 조건에 쓴다.
    private bool IsPlayerStandingOn(Collider spot)
    {
        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        // QueryTriggerInteraction.Ignore: 땅 위를 덮은 구역 트리거가 아니라 실제 땅 콜라이더를 맞힌다.
        return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider == spot;
    }

    // 플레이어가 이동한 뒤(Update) 위치를 따라가야 흔들리지 않으므로 LateUpdate에서 안내 위치를 맞춘다.
    // 보이는 동안(페이드 중 포함)만 계산한다.
    private void LateUpdate()
    {
        if (player == null) return;

        FollowPlayerHead(boardPrompt);
        FollowPlayerHead(dismountPrompt);
    }

    // 안내 UI를 플레이어 머리 위에 두고 카메라 쪽을 보게 한다. 탑승 안내와 하차 안내가 같이 쓴다.
    private void FollowPlayerHead(CanvasGroup prompt)
    {
        if (prompt == null || prompt.alpha <= 0f) return;

        Transform promptTransform = prompt.transform;
        promptTransform.position = player.transform.position + Vector3.up * promptHeight;

        // 카메라와 같은 회전을 주면 사이드뷰·탑다운 어느 시점에서도 글씨가 화면 정면으로 보인다.
        if (_cameraTransform != null)
            promptTransform.rotation = _cameraTransform.rotation;
    }

    // 수동 하차 튜브에 탑승한 채 도착 트리거 안이거나, 오르내리는 땅 옆에 있으면 내릴 수 있다.
    private bool CanDismountHere()
    {
        return !autoDismountOnArrival && IsMounted && (_isInArrivalDock || _dockedLanding != null);
    }

    private void FixedUpdate()
    {
        // 선풍기 일반 반동은 가속도로 계속 밀어준다.
        _rigidbody.AddForce(_push, ForceMode.Acceleration);

        // FixedUpdate는 일정한 간격으로 호출되므로 Time.fixedDeltaTime을 곱해 프레임과 상관없이 같은 속도로 힘이 줄게 한다.
        _push = Vector3.MoveTowards(_push, Vector3.zero, pushDecay * Time.fixedDeltaTime);

        // 수평 속도를 최대 속도로 제한한다.
        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (flatVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            flatVelocity = flatVelocity.normalized * maxSpeed;
            _rigidbody.linearVelocity = new Vector3(flatVelocity.x, velocity.y, flatVelocity.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (arrivalDockTrigger == null || other != arrivalDockTrigger) return;
        _isInArrivalDock = true;

        // 자동 하차 튜브는 탑승한 채 도착 선착장 트리거에 들어오면 바로 내려준다.
        // 수동 하차 튜브는 여기서 내리지 않고, 트리거 안에 있는 동안 Interact를 기다린다.
        if (autoDismountOnArrival && IsMounted)
            Dismount(GetDismountPosition());
    }

    private void OnTriggerExit(Collider other)
    {
        if (arrivalDockTrigger != null && other == arrivalDockTrigger)
            _isInArrivalDock = false;
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (player == null) return;

        // Interact 이벤트는 씬의 모든 튜브가 동시에 받는다. 플레이어가 다른 튜브에 타 있으면 이 튜브는 관여하지 않는다.
        if (player.IsMounted && !IsMounted) return;

        // 탑승 중에 누르면, 도착 트리거 안일 때만 내린다(수동 하차 튜브).
        if (IsMounted)
        {
            if (CanDismountHere())
                Dismount(GetDismountPosition());
            return;
        }

        if (!player.IsGrounded || !IsPlayerInRange()) return;

        Mount();
    }

    // 내릴 위치를 정한다. 하차 구역이 있으면 튜브와 가장 가까운 구역 안의 땅, 없으면 고정 하차 위치를 쓴다.
    private Vector3 GetDismountPosition()
    {
        // 오르내리는 땅 옆이면 그 땅 위, 튜브와 가장 가까운 곳에 내린다.
        if (_dockedLanding != null)
            return GetInsetPointOn(_dockedLanding);

        if (dismountArea == null)
            return arrivalExitPoint != null ? arrivalExitPoint.position : player.transform.position;

        return GetInsetPointOn(dismountArea);
    }

    // 콜라이더(하차 구역 또는 땅) 위에서 튜브와 가장 가까운 점을 찾아 안쪽으로 조금 들이고, 실제 바닥 높이에 맞춘 위치를 돌려준다.
    private Vector3 GetInsetPointOn(Collider area)
    {
        // ClosestPoint는 콜라이더 표면에서 튜브와 가장 가까운 점을 준다. 튜브는 물 위(구역 밖)라 이 점은 물과 맞닿은 땅 끝이 된다.
        Vector3 tubePosition = transform.position;
        Vector3 edgePoint = area.ClosestPoint(tubePosition);

        // 땅 끝에 딱 붙지 않도록 튜브 반대쪽(땅 안쪽)으로 조금 들인다. 높이 차이는 무시하고 수평 방향만 쓴다.
        Vector3 inward = edgePoint - tubePosition;
        inward.y = 0f;
        if (inward.sqrMagnitude > 0.0001f)
            edgePoint += inward.normalized * dismountInset;

        // 들인 점이 구역을 벗어나지 않게 다시 구역 안으로 맞춘다.
        edgePoint = area.ClosestPoint(edgePoint);

        // 구역 위에서 아래로 레이를 쏴 실제 바닥 높이에 세운다. QueryTriggerInteraction.Ignore로 구역 트리거 자신은 무시한다.
        Vector3 rayOrigin = new Vector3(edgePoint.x, area.bounds.max.y + 1f, edgePoint.z);
        float rayLength = area.bounds.size.y + 5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.05f;

        return edgePoint;
    }

    // 탑승할 수 있는 위치인지 확인한다. 높이 차이는 무시하고 XZ 거리만 본다.
    // 1) 튜브와 탑승 가능 거리 안이거나, 2) 탑승 구역(선착장) 안이고 튜브가 구역 탑승 최대 거리 안에 떠 있으면 탈 수 있다.
    private bool IsPlayerInRange()
    {
        // 오르내리는 땅 목록이 있으면 기존 거리/구역 판정 대신 "튜브가 붙어 있는 그 땅 위에 서 있는지"만 본다.
        if (HasLandingSpots)
            return _dockedLanding != null && IsPlayerStandingOn(_dockedLanding);

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        float sqrDistance = offset.sqrMagnitude;

        // 한 방향 튜브는 거리만으로는 타지 못하고, 아래의 탑승 구역 판정만 쓴다.
        bool useRangeCheck = !(boardOnlyInArea && boardArea != null);
        if (useRangeCheck && sqrDistance <= boardRange * boardRange) return true;

        return boardArea != null
            && sqrDistance <= areaBoardMaxDistance * areaBoardMaxDistance
            && IsPlayerInsideBoardArea();
    }

    // 플레이어 몸통 중심이 탑승 구역 콜라이더 안에 있는지 확인한다.
    // ClosestPoint는 점이 콜라이더 안이면 그 점을 그대로 돌려주므로, 같으면 안에 있다는 뜻이다(FixedCameraZone과 같은 방식).
    private bool IsPlayerInsideBoardArea()
    {
        Vector3 center = _playerCC != null ? player.transform.TransformPoint(_playerCC.center) : player.transform.position + Vector3.up;
        return boardArea.enabled && boardArea.ClosestPoint(center) == center;
    }

    private void SetPromptVisible(bool visible)
    {
        if (boardPrompt == null || _promptVisible == visible) return;
        _promptVisible = visible;

        _promptTween?.Kill();
        _promptTween = boardPrompt.DOFade(visible ? 1f : 0f, promptFadeDuration);
    }

    // 하차 안내를 탑승 안내와 같은 시간으로 페이드한다. 상태가 바뀔 때만 Tween을 새로 만든다.
    private void SetDismountPromptVisible(bool visible)
    {
        if (dismountPrompt == null || _dismountPromptVisible == visible) return;
        _dismountPromptVisible = visible;

        _dismountPromptTween?.Kill();
        _dismountPromptTween = dismountPrompt.DOFade(visible ? 1f : 0f, promptFadeDuration);
    }

    private void Mount()
    {
        player.MountTo(this, seat);

        // 탑승하는 순간 튜브가 물에 꾹 눌렸다 올라오는 연출이다. 충돌체가 아닌 모델에만 적용한다.
        if (visualModel != null)
        {
            _boardTween?.Kill();
            visualModel.localPosition = _modelStartLocalPosition;
            _boardTween = visualModel
                .DOPunchPosition(Vector3.down * boardPunchStrength, boardPunchDuration)
                .SetEase(punchEase);
        }
    }

    private void Dismount(Vector3 exitPosition)
    {
        player.Dismount(exitPosition);
    }

    // PlayerController.SetRecoil이 탑승 중 매 프레임 호출한다. 이번 프레임의 반동 속도를 밀 가속도로 바꿔 저장한다.
    public void ApplyPush(Vector3 recoilVelocity)
    {
        _push = new Vector3(recoilVelocity.x, 0f, recoilVelocity.z) * pushMultiplier;
    }

    // PlayerController.SetBlast가 탑승 중 한 번 호출한다. 순간 속도를 더하고 기울기 연출을 재생한다.
    public void ApplyBlast(Vector3 blastVelocity)
    {
        Vector3 flat = new Vector3(blastVelocity.x, 0f, blastVelocity.z);
        _rigidbody.AddForce(flat * blastMultiplier, ForceMode.VelocityChange);

        if (visualModel == null || flat.sqrMagnitude < 0.0001f) return;

        // 진행 방향에 수직인 수평 축을 중심으로 기울여, 앞쪽이 살짝 들리는 느낌을 준다.
        Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);
        _tiltTween?.Kill();
        visualModel.localRotation = _modelStartLocalRotation;
        _tiltTween = visualModel
            .DOPunchRotation(axis * blastTiltAngle, blastTiltDuration)
            .SetEase(punchEase);
    }

    // 장애물에 부딪혀 뒤집혔을 때 호출한다. 그 자리에서 내려 낙사 연출로 복귀 지점까지 보내고, 튜브는 처음 자리로 되돌린다.
    public void Capsize(Vector3 recoverPosition)
    {
        // 이 튜브에 탄 경우에만 플레이어를 내린다. 다른 튜브에 탄 플레이어까지 전복시키지 않기 위해서다.
        if (IsMounted)
        {
            player.Dismount(player.transform.position);
            player.GetComponent<FallDeathHandler>()?.HandleFall(recoverPosition);
        }

        ResetTube(recoverPosition);
    }

    // 소용돌이 같은 장애물이 호출한다. 이 씬의 최신 체크포인트로 복귀하고, 체크포인트가 없으면 시작 재배치 지점으로 돌아간다.
    public void CapsizeToCheckpoint()
    {
        Vector3 recover;
        if (CheckpointManager.HasCheckpoint && CheckpointManager.CurrentCheckpointScene == gameObject.scene.name)
            recover = CheckpointManager.CurrentCheckpointPosition;
        else
            recover = (respawnPoints != null && respawnPoints.Length > 0 && respawnPoints[0] != null)
                ? respawnPoints[0].position
                : _startPosition;
        Capsize(recover);
    }

    // 복귀 위치(XZ)와 가장 가까운 재배치 지점을 고른다. 지정된 지점이 없으면 처음 자리를 쓴다.
    private Vector3 GetRespawnPosition(Vector3 recoverPosition)
    {
        Vector3 best = _startPosition;
        float bestSqr = float.MaxValue;
        if (respawnPoints == null) return best;

        foreach (Transform point in respawnPoints)
        {
            if (point == null) continue;
            Vector3 offset = point.position - recoverPosition;
            offset.y = 0f; // 높이 차이는 무시하고 수평 거리만 비교한다.
            if (offset.sqrMagnitude < bestSqr)
            {
                bestSqr = offset.sqrMagnitude;
                best = point.position;
            }
        }
        return best;
    }

    // 탑승 중 사망 등으로 플레이어가 먼저 순간이동한 뒤, 튜브를 그 위치와 가장 가까운 재배치 지점으로 옮긴다.
    public void ResetNear(Vector3 position)
    {
        ResetTube(position);
    }

    private void ResetTube(Vector3 recoverPosition)
    {
        Vector3 resetPosition = GetRespawnPosition(recoverPosition);
        _push = Vector3.zero;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = resetPosition;
        _rigidbody.rotation = _startRotation;
        transform.SetPositionAndRotation(resetPosition, _startRotation);
    }

    [Button("강제 탑승 (Play Mode)")]
    private void TestMount()
    {
        if (Application.isPlaying && player != null)
            Mount();
    }

    [Button("블라스트 테스트 (Play Mode)")]
    private void TestBlast()
    {
        if (!Application.isPlaying || player == null) return;
        // 선풍기는 바라보는 반대 방향으로 반동을 주므로 같은 방식으로 흉내 낸다.
        ApplyBlast(-player.FacingDirection * 30f);
    }

    [Button("전복 테스트 (Play Mode)")]
    private void TestCapsize()
    {
        if (!Application.isPlaying) return;

        CapsizeToCheckpoint();
    }
}
