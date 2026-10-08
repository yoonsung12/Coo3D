using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 지하철 입구처럼 "↑ 키를 눌러 입장"하는 같은 씬 안의 이동 포털이다.
// 근처에 오면 안내문을 띄우고, ↑(W) 키를 누르면 카메라를 등지고 걸어 들어가며 화면이 어두워진 뒤
// 도착 지점에서 화면이 밝아지며 걸어 나온다. 씬을 바꾸는 ScenePortal과 달리 씬 안에서 순간이동만 한다.
// 감지용 트리거 콜라이더는 이 오브젝트에 붙이고, 안내문 Canvas는 자식으로 둔다.
[RequireComponent(typeof(Collider))]
public class EntrancePortal : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("도착 지점")]
    private Transform destination;
    // 도착할 위치다. 이 Transform의 앞 방향(파란 Z축)이 도착 후 걸어 나올 방향이 된다.

    [SerializeField, LabelText("안내문 Canvas Group")]
    private CanvasGroup promptCanvasGroup;
    // "↑ 키를 눌러 입장" 텍스트가 있는 World Space Canvas를 연결한다.

    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // 순간이동 직후 카메라를 도착 지점으로 바로 옮기기 위해 연결한다. 비워두면 평소처럼 Lerp로 따라간다.

    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // PlayerController와 같은 InputSystem_Actions 에셋을 연결한다. Move 액션의 위쪽(W/↑)으로 입장한다.

    [Title("입장 연출")]
    [SerializeField, LabelText("들어갈 방향")]
    private Vector3 enterDirection = Vector3.forward;
    // 입구 안쪽으로 걸어 들어갈 방향이다. 기본값 +Z는 카메라를 등지는 방향이다.

    [SerializeField, LabelText("돌아서는 시간")]
    private float turnDuration = 0.2f;

    [SerializeField, LabelText("돌아서는 Ease")]
    private Ease turnEase = Ease.OutQuad;

    [SerializeField, LabelText("걷는 속도")]
    private float walkSpeed = 2.5f;
    // 연출 중 걷는 속도다. 걷기 애니메이션은 CharacterController 속도로 재생되므로 너무 작으면 서 있는 것처럼 보인다.

    [SerializeField, LabelText("들어가며 걷는 시간")]
    private float enterWalkDuration = 0.8f;

    [SerializeField, LabelText("나오며 걷는 시간")]
    private float exitWalkDuration = 1.2f;
    // 도착 후 걸어 나오는 시간이다. 걷는 속도 × 시간만큼 이동하므로 도착 지점 앞에 그만큼 바닥이 있어야 한다.

    [SerializeField, LabelText("페이드 아웃 시간")]
    private float fadeOutDuration = 0.6f;

    [SerializeField, LabelText("페이드 인 시간")]
    private float fadeInDuration = 0.6f;

    [SerializeField, LabelText("안내문 페이드 시간")]
    private float promptFadeDuration = 0.2f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 범위 안에 있는지")]
    private bool _playerInRange;

    [ReadOnly, ShowInInspector, LabelText("입장 연출 중")]
    private bool _isEntering;

    private const float Gravity = -20f;
    // 연출 중에도 바닥에 붙어 걷도록 PlayerController의 기본 중력값과 같은 값을 쓴다.

    private InputAction _moveAction;
    private bool _wasUpPressed;
    // 위쪽 키를 "누른 순간"만 입장으로 처리하기 위해 이전 프레임 입력 상태를 기억한다.

    private PlayerController _player;
    private CharacterController _cc;
    private Collider _triggerCollider;
    // 플레이어가 실제로 감지 범위 안에 있는지 다시 확인할 때 쓰는 이 오브젝트의 트리거 콜라이더다.
    private Tween _promptFadeTween;
    private Tween _turnTween;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;

        if (promptCanvasGroup != null)
            promptCanvasGroup.alpha = 0f;

        if (inputActionAsset != null)
            _moveAction = inputActionAsset.FindActionMap("Player", throwIfNotFound: true).FindAction("Move", throwIfNotFound: true);
        // Move 액션의 Enable/Disable은 PlayerController가 관리하므로 여기서는 값만 읽는다.
    }

    private void OnDestroy()
    {
        _promptFadeTween?.Kill();
        _turnTween?.Kill();
    }

    private void Update()
    {
        if (_moveAction == null || _isEntering) return;

        // 범위 안인지는 트리거 이벤트가 아니라 매 프레임 실제 위치로 판정한다.
        // - CharacterController를 끈 채 순간이동하면(입장, 리스폰, 낙사 복귀) OnTriggerExit가 오지 않는다.
        // - 걸어 들어올 때는 몸통 중심보다 캡슐 가장자리가 먼저 닿아 OnTriggerEnter가 한 번만 일찍 온다.
        // 두 경우 모두 이벤트만 믿으면 상태가 실제 위치와 어긋나므로, 값이 바뀔 때만 안내문을 켜고 끈다.
        bool inRange = IsPlayerInsideTrigger();
        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            SetPromptVisible(inRange);
        }

        // W/↑는 Move 액션의 위쪽 입력이다. 사이드뷰에서는 W/S를 이동에 쓰지 않아 다른 조작과 겹치지 않는다.
        bool isUpPressed = _moveAction.ReadValue<Vector2>().y > 0.5f;
        bool pressedThisFrame = isUpPressed && !_wasUpPressed;
        _wasUpPressed = isUpPressed;

        if (pressedThisFrame && _playerInRange && _player != null)
            StartCoroutine(EnterRoutine());
    }

    // 처음 다가온 플레이어를 기억해 두는 용도로만 쓴다. 범위 안/밖 판정은 Update()에서 한다.
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _player = player;
        _cc = player.GetComponent<CharacterController>();
    }

    // 돌아서기 → 걸어 들어가며 페이드 아웃 → 순간이동 → 걸어 나오며 페이드 인 순서로 진행한다.
    // 매 프레임 CharacterController.Move()를 호출해야 해서 Coroutine으로 작성했다.
    private IEnumerator EnterRoutine()
    {
        _isEntering = true;
        SetPromptVisible(false);

        // 연출 중에는 입력으로 움직이거나 점프하지 않도록 플레이어 조작을 잠시 끈다.
        // (걷기 애니메이션은 PlayerAnimatorController가 CharacterController 속도로 따로 재생한다.)
        _player.enabled = false;

        Vector3 enterDir = new Vector3(enterDirection.x, 0f, enterDirection.z).normalized;
        yield return TurnTowards(enterDir);

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(null, fadeOutDuration);
        yield return WalkFor(enterDir, Mathf.Max(enterWalkDuration, fadeOutDuration));
        // 화면이 완전히 어두워지기 전에 순간이동하면 이동이 보이므로, 걷는 시간이 짧아도 페이드 아웃이 끝날 때까지 걷는다.

        // 순간이동은 반드시 Respawn()을 거쳐야 CharacterController 처리와 사이드뷰/라인 Z 갱신이 함께 된다.
        PlayerHealth.Instance.Respawn(destination.position);
        _playerInRange = false;
        // CharacterController를 끈 채 옮겨 OnTriggerExit가 오지 않으므로, 입구를 떠났다는 것을 직접 기록한다.

        Vector3 exitDir = new Vector3(destination.forward.x, 0f, destination.forward.z).normalized;
        _player.transform.rotation = Quaternion.LookRotation(exitDir, Vector3.up);

        if (sideViewCamera != null)
            sideViewCamera.SnapToTarget();

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeIn(null, fadeInDuration);
        yield return WalkFor(exitDir, exitWalkDuration);

        _player.SyncViewModeToPosition();
        // 걸어 나오며 Z가 바뀐 경우(예: 엘리베이터에서 카메라 쪽으로 걸어 나옴) 멈춘 위치를 사이드뷰 라인으로 삼는다.
        // 이 호출이 없으면 라인이 도착 지점 Z로 남아, 조작이 돌아오자마자 그쪽으로 다시 끌려간다.

        _player.enabled = true;
        _isEntering = false;
        _wasUpPressed = true;
        // 입장할 때 누른 ↑ 키를 계속 누르고 있어도, 손을 뗐다 다시 누르기 전까지는 재입장하지 않게 한다.
    }

    // 플레이어 몸통 중심이 감지용 트리거 안에 있는지 확인한다.
    // 발끝(pivot)은 바닥 경계와 겹칠 수 있어 CharacterController 중심으로 검사한다.
    private bool IsPlayerInsideTrigger()
    {
        if (_player == null || _cc == null) return false;

        Vector3 center = _player.transform.TransformPoint(_cc.center);
        return _triggerCollider.ClosestPoint(center) == center;
        // ClosestPoint는 점이 콜라이더 안에 있으면 그 점을 그대로 돌려주므로, 같으면 안에 있다는 뜻이다.
    }

    // 몸을 목표 방향으로 돌린다. 회전은 충돌과 무관하므로 DOTween으로 돌려도 이동 판정에 영향이 없다.
    private IEnumerator TurnTowards(Vector3 direction)
    {
        _turnTween?.Kill();
        _turnTween = _player.transform
            .DORotateQuaternion(Quaternion.LookRotation(direction, Vector3.up), turnDuration)
            .SetEase(turnEase);
        yield return _turnTween.WaitForCompletion();
    }

    // 정해진 시간 동안 한 방향으로 걷는다. 벽에 막히면 CharacterController가 알아서 멈춰 준다.
    private IEnumerator WalkFor(Vector3 direction, float duration)
    {
        float verticalVelocity = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 바닥에 닿아 있으면 살짝 눌러 붙이고, 공중이면 중력을 누적해 떨어지게 한다.
            verticalVelocity = _cc.isGrounded ? -2f : verticalVelocity + Gravity * Time.deltaTime;

            Vector3 velocity = direction * walkSpeed + Vector3.up * verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);
            // Time.deltaTime을 곱해 프레임레이트와 상관없이 초당 walkSpeed만큼 이동한다.

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptCanvasGroup == null) return;

        _promptFadeTween?.Kill();
        _promptFadeTween = promptCanvasGroup.DOFade(visible ? 1f : 0f, promptFadeDuration);
    }

    [Button("입장 테스트 (Play Mode, 범위 안에서)")]
    private void TestEnter()
    {
        if (!Application.isPlaying || _isEntering || _player == null) return;
        StartCoroutine(EnterRoutine());
    }
}
