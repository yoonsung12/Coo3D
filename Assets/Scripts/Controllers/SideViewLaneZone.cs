using Sirenix.OdinInspector;
using UnityEngine;

// 탑다운 구역(ViewModeZone) 안에 놓는 "사이드뷰 라인" 트리거다.
// 활성화된 뒤에는 플레이어가 이 안에 있는 동안 사이드뷰로 고정하고, 지정한 Z 라인으로 정렬시킨다.
// 오른쪽 끝으로 나가면 다시 탑다운으로 돌려놓고, 왼쪽(입구 쪽)으로 나가면 사이드뷰를 그대로 유지한다.
// 퍼즐(예: BoxLaneGoal)을 풀었을 때 Activate()로 켜는 용도다.
[RequireComponent(typeof(Collider))]
public class SideViewLaneZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고, X는 사이드뷰로 지나갈 구간 전체, Z는 라인 주변을 얇게 덮도록 맞춘다.\n" +
             "Y는 점프(공중 점프 포함)로 올라가는 높이까지 넉넉히 덮어야 위로 빠져나가지 않는다.")]
    [Title("라인 설정")]
    [SerializeField, LabelText("사이드뷰 라인 Z")]
    private float laneZ = -24.2f;
    // 사이드뷰로 바뀔 때 플레이어가 정렬될 Z 위치다. 이 라인 위에 발판(상자/정거장/버스)이 겹쳐 있어야 한다.

    [SerializeField, LabelText("시작부터 활성화")]
    private bool startActive;
    // 끄면 퍼즐을 풀기 전까지 아무 일도 하지 않는다. 테스트할 때만 켜 둔다.

    [SerializeField, LabelText("오른쪽 출구 판정 여유")]
    private float rightExitMargin = 1f;
    // 구역 오른쪽 끝에서 이 거리 안쪽으로 빠져나가면 "오른쪽 출구"로 보고 탑다운으로 돌린다.
    // 위/아래로 빠져나간 경우까지 탑다운으로 바뀌지 않게 하기 위한 여유값이다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("활성화됨")]
    private bool _isActive;

    private Collider _collider;

    public float LaneZ => laneZ;
    // BoxLaneGoal이 상자를 같은 라인에 스냅할 때 사용한다.

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _isActive = startActive;
    }

    // 퍼즐 완료 시 호출해 이 라인을 켠다.
    public void Activate() => _isActive = true;

    public void Deactivate() => _isActive = false;

    // Enter 대신 Stay를 쓰는 이유: 이 구역 안에서 리스폰하면 PlayerController.SyncViewModeToPosition()이
    // 바깥의 큰 ViewModeZone을 보고 탑다운으로 돌려놓는데, Stay에서 다시 사이드뷰로 맞춰 주기 위해서다.
    private void OnTriggerStay(Collider other)
    {
        if (!_isActive) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 이미 사이드뷰면 SetViewMode가 무시되지만, 불필요한 호출을 줄이려고 먼저 검사한다.
        if (player.CurrentViewMode == ViewMode.TopDown)
            player.SetViewMode(ViewMode.SideView, laneZ);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_isActive) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 오른쪽 끝(출구)으로 나갔을 때만 탑다운으로 돌린다. 왼쪽으로 돌아가면 사이드뷰를 유지한다.
        bool exitedRight = player.transform.position.x >= _collider.bounds.max.x - rightExitMargin;
        if (exitedRight)
            player.SetViewMode(ViewMode.TopDown, laneZ);
    }

    [Button("라인 활성화 테스트")]
    private void TestActivate() => Activate();
}
