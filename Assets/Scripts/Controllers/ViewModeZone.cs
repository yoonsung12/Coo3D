using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 이 구역 안에 있는 동안 탑다운 시점(WASD 360도 이동)으로 바꾸고,
// 구역을 벗어나면 다시 사이드뷰로 돌려놓는 트리거다.
// 카메라는 PlayerController의 모드 변경 이벤트를 받아 자동으로 따라 바뀌므로 따로 연결하지 않는다.
[RequireComponent(typeof(Collider))]
public class ViewModeZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고, 탑다운으로 플레이할 공간 전체를 덮도록 크기를 맞춘다.\n" +
             "구역을 나갈 때는 사이드뷰 라인(Z)으로 자동 복귀하므로, 출구 쪽 지형이 그 라인과 이어지게 배치한다.")]
    [Title("사이드뷰 복귀 설정")]
    [SerializeField, LabelText("복귀 Z 직접 지정")]
    private bool useCustomLaneZ;
    // 끄면 플레이어가 구역에 들어온 순간의 Z로 돌아간다(대부분 이걸로 충분하다).
    // 입구와 출구의 사이드뷰 라인이 서로 다를 때만 켜서 직접 지정한다.

    [SerializeField, LabelText("복귀 Z"), ShowIf(nameof(useCustomLaneZ))]
    private float customLaneZ;
    // 구역을 나갈 때 플레이어가 돌아갈 사이드뷰 Z 위치다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("진입 시 Z")]
    private float _enteredZ;

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 사이드뷰에서 걸어 들어온 순간의 Z만 기억해 두었다가, 나갈 때 원래 사이드뷰 라인으로 돌려보낸다.
        // 이미 탑다운인 상태(구역 안으로 리스폰 등)에서 들어온 경우엔 기록을 덮어쓰지 않아 원래 라인이 유지된다.
        if (player.CurrentViewMode == ViewMode.SideView)
            _enteredZ = player.transform.position.z;

        player.SetViewMode(ViewMode.TopDown, GetLaneZ());
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        player.SetViewMode(ViewMode.SideView, GetLaneZ());
    }

    // 이 구역을 나갈 때 돌아갈 사이드뷰 Z 라인이다. PlayerController가 리스폰 후 시점을 맞출 때도 사용한다.
    public float GetLaneZ()
    {
        return useCustomLaneZ ? customLaneZ : _enteredZ;
    }
}
