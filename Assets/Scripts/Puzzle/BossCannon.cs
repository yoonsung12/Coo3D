using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 최종보스 여름 패턴 전용 대포다. 인력 단계가 시작되면 BossSummerPattern이 PullToStop()을 한 번
// 호출해서 오른쪽(맵 중앙 방향)으로 짧게 슈욱 이동시키지만, stopX를 넘어서지는 않는다("왼쪽 끝에서 멈춘다").
// 레이저가 활성 상태일 때만 상호작용 프롬프트가 뜨고, Interact 입력 시 포탄을 발사해서
// 레이저를 쏘고 있는 보스를 저지한다. VendingMachine.cs의 Interact 프롬프트 구조를 그대로 재사용한다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BossCannon : MonoBehaviour
{
    [Title("견인 설정")]
    [SerializeField, LabelText("멈추는 X 좌표")]
    private float stopX = -19.0f;
    // 인력에 끌려오다가 이 X 좌표에 도달하면 더 이상 오른쪽(중앙 방향)으로 이동하지 않는다.

    [Title("발사 설정")]
    [SerializeField, LabelText("발사 위치")]
    private Transform muzzlePoint;
    // Inspector에서 대포 자식의 발사 위치(빈 오브젝트)를 연결한다.

    [SerializeField, LabelText("포탄 프리팹")]
    private BossCannonball cannonballPrefab;

    [Title("연결")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다. Player 맵의 Interact 액션을 사용한다.

    [Title("상호작용 프롬프트")]
    [SerializeField, LabelText("프롬프트 Canvas Group")]
    private CanvasGroup interactPromptCanvasGroup;
    // 플레이어가 범위 안에 있고 레이저가 활성 상태일 때 대포 위에 "E" 텍스트를 띄우는 World Space Canvas다.

    [SerializeField, LabelText("프롬프트 페이드 시간")]
    private float promptFadeDuration = 0.2f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 범위 안에 있는지")]
    private bool _playerInRange;

    private Rigidbody _rb;
    private InputAction _interactAction;
    private Tween _promptFadeTween;
    private Tween _pullTween;
    private bool _isPromptVisible;
    private Boss _boss;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        // 대포는 스스로 물리 반응하지 않고 PullToStop()으로 직접 위치를 옮기므로 Kinematic으로 둔다.

        GetComponent<Collider>().isTrigger = true;

        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);

        // 씬에 보스가 하나뿐이라 미리 찾아둔다(VendingMachine이 CoinManager.Instance를 참조하는 것과
        // 달리 Boss는 정적 싱글톤이 아니라서 직접 탐색해야 한다).
        _boss = FindFirstObjectByType<Boss>();

        if (interactPromptCanvasGroup != null)
            interactPromptCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        _interactAction.Enable();
        // 공유 Interact 액션 자체가 Hold(일정 시간 누르고 있기) 인터랙션으로 설정되어 있어서,
        // performed를 구독하면 짧게 탭했을 때 반응하지 않는다. VendingMachine 등 다른 곳에서
        // 쓰는 Hold 판정(performed)은 그대로 두고, 이 대포만 누르는 즉시(started) 발사되게 한다.
        _interactAction.started += OnInteractPerformed;
    }

    private void OnDisable()
    {
        _interactAction.started -= OnInteractPerformed;
        _interactAction.Disable();

        _promptFadeTween?.Kill();
        _pullTween?.Kill();
    }

    // BossSummerPattern이 인력 단계 시작 시 한 번만 호출한다. 매 프레임 조금씩 끌려오는 대신,
    // 지금 위치에서 stopX(왼쪽 끝)까지 duration 동안 한 번에 슈욱 이동한다.
    public void PullToStop(float duration)
    {
        _pullTween?.Kill();

        Vector3 target = new Vector3(stopX, _rb.position.y, _rb.position.z);
        _pullTween = DOTween.To(() => _rb.position, x => _rb.MovePosition(x), target, duration)
            .SetEase(Ease.OutQuad);
        // Rigidbody가 Kinematic이라 MovePosition을 계속 경유해야 트리거 감지(플레이어 범위 판정)가
        // 어긋나지 않는다 — DOTween.To의 setter를 MovePosition으로 지정해 매 스텝 이 방식을 유지한다.
    }

    private void Update()
    {
        // 범위 안에 있어도 레이저가 활성 상태가 아니면 프롬프트를 띄우지 않는다.
        bool shouldShow = _playerInRange && BossSummerPattern.IsLaserActive;
        if (shouldShow == _isPromptVisible) return;

        _isPromptVisible = shouldShow;
        SetPromptVisible(shouldShow);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            _playerInRange = false;
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactPromptCanvasGroup == null) return;

        _promptFadeTween?.Kill();
        _promptFadeTween = interactPromptCanvasGroup.DOFade(visible ? 1f : 0f, promptFadeDuration);
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!_playerInRange || !BossSummerPattern.IsLaserActive) return;
        Fire();
    }

    private void Fire()
    {
        if (cannonballPrefab == null || muzzlePoint == null || _boss == null) return;

        BossCannonball ball = Instantiate(cannonballPrefab, muzzlePoint.position, Quaternion.identity);
        ball.Launch(_boss.transform.position);
    }

    [Button("테스트 발사 (범위 무시)")]
    private void TestFire() => Fire();
}
