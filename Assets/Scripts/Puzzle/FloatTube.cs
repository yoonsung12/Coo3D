using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 물 위에 떠 있는 수영 튜브다. 플레이어가 Interact(Space 꾹)로 타면 튜브 구멍에 앉은 채 합쳐지고,
// 이후 선풍기 반동(SetRecoil/SetBlast)이 플레이어 대신 튜브를 밀어 물 위를 미끄러진다.
// 튜브는 수면 높이(Y)에 고정된 Rigidbody라서 XZ 평면으로만 움직이고, 벽에는 살짝 튕긴다.
// 도착 지점 트리거(arrivalDockTrigger)에 들어가면 자동으로 내려준다.
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
    // 내린 뒤 플레이어가 서는 위치다. 도착 선착장 바닥 위에 둔다.

    [SerializeField, LabelText("튜브 재배치 지점들")]
    private Transform[] respawnPoints;
    // 전복해서 섬 체크포인트로 리스폰할 때, 튜브도 그 섬 옆 물 위에 다시 나타나야 플레이어가 고립되지 않는다.
    // 시작 지점/섬마다 수면 위 빈 오브젝트를 만들어 넣는다. 복귀 위치(XZ)에서 가장 가까운 지점이 쓰이고, 비워 두면 처음 자리로 돌아간다.

    [SerializeField, LabelText("탑승 안내 UI (선택)")]
    private CanvasGroup boardPrompt;
    // 탑승 가능 범위에 들어오면 페이드로 나타나는 안내다. 비워 두면 안내 없이 동작한다.

    [Title("탑승")]
    [SerializeField, LabelText("탑승 가능 거리")]
    private float boardRange = 2.5f;
    // 플레이어와 튜브의 수평(XZ) 거리가 이 값 이하일 때만 탑승할 수 있다. 클수록 멀리서도 탄다.

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
    public bool IsMounted => player != null && player.IsMounted;

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

        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
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
    }

    private void Update()
    {
        if (player == null) return;

        // 탑승 중에는 안내를 숨기고, 아니면 범위 안일 때만 보여준다.
        SetPromptVisible(!player.IsMounted && IsPlayerInRange());
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
        // 탑승한 채 도착 선착장 트리거에 들어오면 자동으로 내려준다.
        if (IsMounted && arrivalDockTrigger != null && other == arrivalDockTrigger)
            Dismount(arrivalExitPoint != null ? arrivalExitPoint.position : player.transform.position);
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (player == null || player.IsMounted) return;
        if (!player.IsGrounded || !IsPlayerInRange()) return;

        Mount();
    }

    // 플레이어와 튜브의 XZ 거리가 탑승 가능 거리 안인지 확인한다. 높이 차이는 무시한다.
    private bool IsPlayerInRange()
    {
        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= boardRange * boardRange;
    }

    private void SetPromptVisible(bool visible)
    {
        if (boardPrompt == null || _promptVisible == visible) return;
        _promptVisible = visible;

        _promptTween?.Kill();
        _promptTween = boardPrompt.DOFade(visible ? 1f : 0f, promptFadeDuration);
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
        if (player != null && player.IsMounted)
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
