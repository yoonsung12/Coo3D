using Sirenix.OdinInspector;
using UnityEngine;

// 사이드뷰 진행 방향을 바꾸는 구간 트리거다.
// 기본 사이드뷰는 +X(좌우)로만 진행하지만, Z 방향으로 뻗은 통로 같은 곳에 이 구역을 덮어 두면
// 플레이어가 안에 있는 동안 지정한 축으로 진행하고, 카메라도 통로 옆으로 돌아가 사이드뷰를 유지한다.
// 구역을 나가면 기본 축(+X)으로 되돌린다.
[RequireComponent(typeof(Collider))]
public class SideViewAxisZone : MonoBehaviour
{
    // Inspector에서 고를 진행 방향이다. D키(화면 오른쪽)를 눌렀을 때 나아갈 월드 방향을 뜻한다.
    public enum AxisDirection
    {
        PlusX,
        MinusX,
        PlusZ,
        MinusZ
    }

    [InfoBox("BoxCollider의 Is Trigger를 켜고, 통로 전체(점프 높이 포함)를 덮도록 크기를 맞춘다.\n" +
             "탑다운 구역(ViewModeZone)과 겹치지 않게 배치한다. 겹치면 경계에서 시점이 깜빡일 수 있다.")]
    [Title("진행 방향 설정")]
    [SerializeField, LabelText("진행 방향 (D키 방향)")]
    private AxisDirection direction = AxisDirection.MinusZ;
    // 예: 통로가 -Z로 뻗어 있고 D키로 안쪽으로 들어가게 하려면 MinusZ를 고른다.
    // 카메라는 이 방향이 화면 오른쪽이 되도록 통로 옆에서 바라본다.

    [SerializeField, LabelText("라인 직접 지정")]
    private bool useCustomLane;
    // 끄면 박스 콜라이더의 중심을 라인으로 쓴다(통로 가운데를 걷게 된다).

    [SerializeField, LabelText("라인 좌표 (깊이 방향)"), ShowIf(nameof(useCustomLane))]
    private float customLane;
    // 진행 방향이 ±Z면 X 좌표, ±X면 Z 좌표를 월드 좌표 그대로 넣는다. 플레이어가 이 좌표에 고정된다.
    // (깊이 방향이 -축인 경우의 부호는 LaneDepth에서 알아서 맞춘다.)

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 안에 있음")]
    private bool _playerInside;

    private Collider _collider;

    // 사이드뷰에서 D키로 나아갈 월드 방향이다.
    public Vector3 Axis
    {
        get
        {
            switch (direction)
            {
                case AxisDirection.MinusX: return Vector3.left;
                case AxisDirection.PlusZ: return Vector3.forward;
                case AxisDirection.MinusZ: return Vector3.back;
                default: return Vector3.right;
            }
        }
    }

    // PlayerController가 고정할 깊이 라인 값이다.
    // PlayerController는 "위치 · 깊이 방향(진행 축 × 위쪽)"으로 라인을 비교하므로 같은 방식으로 계산해 넘긴다.
    public float LaneDepth
    {
        get
        {
            Vector3 depthAxis = Vector3.Cross(Axis, Vector3.up);
            // 깊이 방향은 ±X 또는 ±Z 중 하나다. 직접 지정한 좌표를 그 축의 위치로 만들어 같은 방식으로 내적한다.
            Vector3 lanePoint = useCustomLane
                ? new Vector3(customLane, 0f, customLane)
                : GetCollider().bounds.center;
            return Vector3.Dot(lanePoint, depthAxis);
        }
    }

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    private Collider GetCollider()
    {
        // 에디터 테스트 버튼처럼 Awake 전에 불릴 수 있어 필요할 때 가져온다.
        if (_collider == null)
            _collider = GetComponent<Collider>();
        return _collider;
    }

    // Enter 대신 Stay를 쓰는 이유: 탑다운 구역에서 걸어 나온 같은 프레임에 ViewModeZone이 라인을 덮어써도,
    // 다음 물리 프레임에 다시 이 통로의 축/라인으로 바로잡기 위해서다. (값이 같으면 PlayerController가 바로 return한다.)
    private void OnTriggerStay(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInside = true;
        player.SetSideViewAxis(Axis, LaneDepth);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInside = false;

        // 통로를 여러 박스로 이어 붙인 경우, 아직 다른 SideViewAxisZone 안에 있으면 축을 유지한다.
        if (IsStillInsideAnyAxisZone(player)) return;

        // 통로를 벗어나면 기본 사이드뷰(+X 진행)로 되돌리고, 현재 Z를 라인으로 삼는다.
        // 탑다운 구역으로 들어간 경우엔 이후 그 구역을 나갈 때 ViewModeZone이 라인을 다시 정해 준다.
        player.SetSideViewAxis(Vector3.right, player.transform.position.z);
    }

    // 플레이어 몸통 중심이 아직 SideViewAxisZone 콜라이더 안에 있는지 검사한다.
    // QueryTriggerInteraction.Collide: 구역 콜라이더는 트리거라 이 옵션이 있어야 검사에 잡힌다.
    private bool IsStillInsideAnyAxisZone(PlayerController player)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        Vector3 center = player.transform.TransformPoint(cc.center);
        Collider[] hits = Physics.OverlapSphere(center, 0.1f, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<SideViewAxisZone>() != null) return true;
        }
        return false;
    }

    [Button("플레이어에 이 축 적용 (Play Mode 테스트)")]
    private void TestApplyAxis()
    {
        // 플레이어를 통로까지 걸어가지 않고도 카메라 회전/이동 방향을 바로 확인하기 위한 버튼이다.
        if (PlayerHealth.Instance == null) return;
        PlayerController player = PlayerHealth.Instance.GetComponent<PlayerController>();
        if (player != null)
            player.SetSideViewAxis(Axis, LaneDepth);
    }
}
