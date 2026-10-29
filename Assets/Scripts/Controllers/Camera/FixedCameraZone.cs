using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 옛날 바이오하자드처럼, 구역 안에서는 카메라를 정해진 자리에 고정하는 트리거다.
// 플레이어가 구역 안에 있는 동안 SideViewCamera를 "카메라 지점"의 위치/회전에 고정하고,
// 조작은 탑다운 + 카메라 방향 기준으로 바꿔 ↑가 항상 화면 안쪽으로 가게 한다.
// 구역을 나가면 카메라는 다시 플레이어를 따라가고, 시점은 그 위치의 원래 구역 규칙(ViewModeZone 등)으로 돌아간다.
[RequireComponent(typeof(Collider))]
public class FixedCameraZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고 고정 화면으로 보여줄 공간을 덮는다. 여러 개 붙여 ㄱ자 등으로 만들 수 있다.\n" +
             "카메라 지점은 씬 뷰에서 원하는 구도를 잡은 뒤 아래 \"현재 씬 뷰 시점을 카메라 지점으로\" 버튼으로 맞춘다.")]
    [Title("연결")]
    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // Main Camera의 SideViewCamera를 연결한다.

    [SerializeField, LabelText("카메라 지점")]
    private Transform cameraPoint;
    // 구역 안에서 카메라가 머물 위치와 회전이다. 빈 오브젝트를 만들어 연결한다.

    [SerializeField, LabelText("플레이어")]
    private PlayerController player;
    // 구역 안에 있는지 매 프레임 확인하고, 조작 방향을 맞출 플레이어를 연결한다.

    [Title("전환 연출 (DOTween)")]
    [SerializeField, LabelText("들어갈 때 전환 시간")]
    private float enterDuration = 0.8f;
    // 추적 카메라에서 고정 카메라로 넘어가는 시간이다. 0이면 컷처럼 바로 바뀐다.

    [SerializeField, LabelText("나갈 때 전환 시간")]
    private float exitDuration = 0.8f;

    [SerializeField, LabelText("전환 Ease")]
    private Ease ease = Ease.InOutSine;

    [SerializeField, LabelText("순간이동 판정 거리")]
    private float teleportDistance = 5f;
    // 한 프레임에 이 거리보다 멀리 움직여 구역을 드나들었다면 순간이동(리스폰, 구덩이 이동)으로 보고 전환 없이 바로 바꾼다.
    // 그러지 않으면 화면이 밝아질 때 카메라가 먼 곳에서 쓸려 오는 모습이 보인다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 구역 안에 있음")]
    private bool _isInside;

    private Collider[] _colliders;
    private CharacterController _cc;
    private Vector3 _lastPlayerPosition;

    private void Awake()
    {
        _colliders = GetComponents<Collider>();
        foreach (Collider col in _colliders)
            col.isTrigger = true;

        if (player != null)
        {
            _cc = player.GetComponent<CharacterController>();
            _lastPlayerPosition = player.transform.position;
        }
    }

    private void OnDisable()
    {
        // 구역이 꺼지면 카메라가 고정된 채로 남지 않게 바로 풀어 준다.
        if (_isInside && sideViewCamera != null)
            sideViewCamera.EndFixedView(0f, ease);
        _isInside = false;
    }

    private void Update()
    {
        if (player == null || _cc == null || sideViewCamera == null || cameraPoint == null) return;

        // 안/밖 판정은 트리거 이벤트가 아니라 매 프레임 실제 위치로 한다.
        // CharacterController를 끈 채 순간이동하면(Respawn) OnTriggerExit가 오지 않기 때문이다.
        bool inside = IsPlayerInside();
        bool teleported = (player.transform.position - _lastPlayerPosition).sqrMagnitude > teleportDistance * teleportDistance;
        _lastPlayerPosition = player.transform.position;

        if (inside && !_isInside)
            Enter(teleported);
        else if (!inside && _isInside)
            Exit(teleported);

        if (_isInside)
            ApplyCameraRelativeControls();
    }

    private void Enter(bool instant)
    {
        _isInside = true;
        sideViewCamera.StartFixedView(cameraPoint.position, cameraPoint.rotation, instant ? 0f : enterDuration, ease);
    }

    private void Exit(bool instant)
    {
        _isInside = false;
        sideViewCamera.EndFixedView(instant ? 0f : exitDuration, ease);

        // 구역 안에서 강제로 바꿔 둔 탑다운/방향을, 지금 위치의 원래 구역 규칙으로 되돌린다.
        player.SyncViewModeToPosition();
    }

    // 고정 화면에서는 탑다운 이동을 카메라 방향만큼 돌려, ↑가 화면 안쪽·→가 화면 오른쪽이 되게 한다.
    // 구역 경계에서 ViewModeZone이 사이드뷰로 바꿔도 다음 프레임에 다시 맞춰지도록 매 프레임 확인한다.
    private void ApplyCameraRelativeControls()
    {
        if (player.CurrentViewMode != ViewMode.TopDown)
            player.SetViewMode(ViewMode.TopDown, player.transform.position.z);

        player.SetTopDownYaw(cameraPoint.eulerAngles.y);
    }

    // 플레이어 몸통 중심이 이 오브젝트의 콜라이더 중 하나 안에 있는지 확인한다.
    // 발끝(pivot)은 바닥 경계와 겹칠 수 있어 CharacterController 중심으로 검사한다.
    private bool IsPlayerInside()
    {
        Vector3 center = player.transform.TransformPoint(_cc.center);
        foreach (Collider col in _colliders)
        {
            // ClosestPoint는 점이 콜라이더 안에 있으면 그 점을 그대로 돌려주므로, 같으면 안에 있다는 뜻이다.
            if (col.enabled && col.ClosestPoint(center) == center)
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    [Button("현재 씬 뷰 시점을 카메라 지점으로")]
    private void CopySceneViewToCameraPoint()
    {
        UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;
        if (sceneView == null || cameraPoint == null) return;

        // Undo에 기록해 두어 실수로 눌러도 Ctrl+Z로 되돌릴 수 있게 한다.
        UnityEditor.Undo.RecordObject(cameraPoint, "카메라 지점을 씬 뷰 시점으로");
        Transform sceneCamera = sceneView.camera.transform;
        cameraPoint.SetPositionAndRotation(sceneCamera.position, sceneCamera.rotation);
    }
#endif
}
