using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 이 트리거를 처음 지나가면 카메라가 주목 대상 쪽으로 이동하며 확대했다가 다시 플레이어에게 돌아오는 연출이다.
// 카메라 이동은 기존 SideViewCamera.PlayFocusPan()을 그대로 쓰고, 확대는 카메라 FOV만 잠깐 줄였다가 되돌린다.
// 연출 동안에는 플레이어 조작을 잠그고, 끝나면 다시 풀어 준다(EntrancePortal의 도착 주목 연출과 같은 방식).
[RequireComponent(typeof(Collider))]
public class CameraFocusTrigger : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고, 플레이어가 지나갈 선을 덮도록 크기를 맞춘다.")]
    [Title("연결 참조")]
    [SerializeField, LabelText("주목 대상")]
    private Transform focusTarget;
    // 카메라가 비출 오브젝트를 연결한다. 이 오브젝트가 꺼져 있으면(예: 이미 날아간 비둘기) 연출을 건너뛴다.

    [SerializeField, LabelText("주목 지점 보정")]
    private Vector3 focusOffset;
    // 주목 대상 위치에 더할 월드 좌표 보정값이다. 대상이 화면 가운데에서 벗어나 보이면 조절한다.
    // 예: 탑다운에서 대상이 화면 위쪽에 보이면 Z를 음수로 넣어 카메라가 조금 더 앞(대상 쪽)으로 가게 한다.

    [SerializeField, LabelText("SideViewCamera (비우면 메인 카메라)")]
    private SideViewCamera sideViewCamera;
    // 메인 카메라의 SideViewCamera를 연결한다. 비워두면 시작할 때 메인 카메라에서 찾는다.

    [Title("카메라 이동")]
    [SerializeField, LabelText("다가가는 시간")]
    private float moveDuration = 1.2f;

    [SerializeField, LabelText("머무는 시간")]
    private float holdDuration = 1.5f;

    [SerializeField, LabelText("돌아오는 시간")]
    private float returnDuration = 1f;

    [SerializeField, LabelText("Ease")]
    private Ease ease = Ease.InOutSine;

    [Title("확대")]
    [SerializeField, LabelText("확대 FOV"), Range(10f, 90f)]
    private float zoomFieldOfView = 35f;
    // 값이 작을수록 더 크게 확대된다. 평소 카메라 FOV(60)보다 작게 넣는다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("재생함")]
    private bool _hasPlayed;

    private Camera _camera;
    private PlayerController _player;
    private Sequence _zoomSequence;
    // 연출 도중 씬이 바뀌거나 오브젝트가 꺼질 때 정리할 수 있도록 저장한다.

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (sideViewCamera == null && Camera.main != null)
            sideViewCamera = Camera.main.GetComponent<SideViewCamera>();

        if (sideViewCamera != null)
            _camera = sideViewCamera.GetComponent<Camera>();
    }

    private void OnDestroy()
    {
        _zoomSequence?.Kill();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasPlayed) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 주목 대상이 이미 사라졌다면(이어하기로 처치된 비둘기 등) 보여줄 게 없으므로 다시 재생하지 않게만 해 둔다.
        _hasPlayed = true;
        if (focusTarget == null || !focusTarget.gameObject.activeInHierarchy || sideViewCamera == null) return;

        _player = player;
        PlayFocus();
    }

    private void PlayFocus()
    {
        // 연출 중에는 입력으로 움직이거나 점프하지 않도록 플레이어 조작을 잠시 끈다.
        _player.enabled = false;

        sideViewCamera.PlayFocusPan(focusTarget.position + focusOffset, moveDuration, holdDuration, returnDuration, ease, RestorePlayer);

        if (_camera == null) return;

        // 카메라가 다가가는 동안 함께 확대하고, 돌아오는 동안 원래 FOV로 되돌린다.
        // 시간은 PlayFocusPan과 같게 맞춰 이동과 확대가 한 덩어리로 보이게 한다.
        float originalFieldOfView = _camera.fieldOfView;
        _zoomSequence?.Kill();
        _zoomSequence = DOTween.Sequence()
            .Append(_camera.DOFieldOfView(zoomFieldOfView, moveDuration).SetEase(ease))
            .AppendInterval(holdDuration)
            .Append(_camera.DOFieldOfView(originalFieldOfView, returnDuration).SetEase(ease))
            .SetLink(_camera.gameObject);
    }

    // 카메라가 플레이어에게 완전히 돌아온 뒤 조작을 되돌려 준다.
    private void RestorePlayer()
    {
        if (_player != null)
            _player.enabled = true;
    }

    [Button("연출 테스트 (Play Mode)")]
    private void TestFocus()
    {
        if (!Application.isPlaying || PlayerHealth.Instance == null) return;

        _player = PlayerHealth.Instance.GetComponent<PlayerController>();
        if (_player != null && focusTarget != null && sideViewCamera != null)
            PlayFocus();
    }
}
