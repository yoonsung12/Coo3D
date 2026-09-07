using Sirenix.OdinInspector;
using UnityEngine;

// FuseLine 뿌리 본선에 장식으로 붙는 잔가지 하나를 나타낸다. 순수 시각 요소라 콜라이더나
// 게임플레이 로직은 없고, 본선이 자라나는 위치를 넘어서기 전까지 숨겨두는 역할만 한다.
[RequireComponent(typeof(LineRenderer))]
public class RootBranchVisual : MonoBehaviour
{
    [SerializeField, LabelText("본선 기준 부착 거리"), ReadOnly]
    private float attachDistance;
    // FuseLine의 시작 웨이포인트(waypoints[0])로부터 이 잔가지가 붙은 지점까지의 누적 거리다.
    // 씬 생성 시점에 자동으로 계산되어 채워지며, Inspector에는 확인 용도로만 노출한다.

    private LineRenderer _lineRenderer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
    }

    // 씬 생성 스크립트가 잔가지를 만들 때 부착 거리를 기록해둔다.
    public void SetAttachDistance(float distance) => attachDistance = distance;

    // FuseLine이 지금 그리고 있는 컷오프 거리(burnedDistance)가 이 잔가지의 부착 거리를
    // 지나면(본선이 이 지점까지 자라났으면) 보이게 하고, 아직 안 지나갔으면 숨긴다.
    public void ApplyGrowProgress(float burnedDistance)
    {
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.enabled = burnedDistance <= attachDistance;
    }
}
