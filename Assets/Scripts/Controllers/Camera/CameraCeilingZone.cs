using Sirenix.OdinInspector;
using UnityEngine;

// 천장이 낮은 구역에서 사이드뷰 카메라가 천장 위로 올라가 플레이어를 가리지 않도록 카메라 높이를 제한하는 구역이다.
// 플레이어 점프는 그대로 두고(머리는 실제 천장 콜라이더에 막힌다) 카메라만 막는다.
// 트리거 콜라이더를 쓰지 않고 범위(Bounds)로만 판정한다. 다른 스크립트의 트리거 감지에 섞이지 않고,
// PlayerHealth.Respawn()처럼 CharacterController를 끈 채 순간이동해 OnTriggerExit가 오지 않는 경우에도 제한이 제대로 풀린다.
public class CameraCeilingZone : MonoBehaviour
{
    [InfoBox("이 오브젝트 위치를 중심으로 '구역 크기'만큼의 상자 안에 플레이어가 있으면 카메라 높이를 제한한다.\n" +
             "씬 뷰에서 오브젝트를 선택하면 구역(초록 상자)과 최대 높이(빨간 면)가 보인다.")]
    [Title("연결")]
    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // Inspector에서 씬의 Main Camera(SideViewCamera)를 연결한다.

    [SerializeField, LabelText("플레이어")]
    private CharacterController player;
    // Inspector에서 Player 오브젝트를 연결한다. 몸통 중심이 구역 안에 있는지 판정할 때 쓴다.

    [Title("구역 설정")]
    [SerializeField, LabelText("구역 크기")]
    private Vector3 zoneSize = new Vector3(10f, 6f, 10f);
    // 천장이 낮은 공간 전체를 덮도록 맞춘다. 천장 위(다른 층)까지 덮지 않게 위쪽 끝은 천장 아랫면에 맞춘다.

    [SerializeField, LabelText("카메라 최대 높이 (월드 Y)")]
    private float maxCameraY;
    // 천장 아랫면보다 0.5 정도 낮게 넣는다. 카메라 Near Clip(0.3)만큼은 여유가 있어야 천장이 화면을 가르지 않는다.
    // 값이 작을수록 카메라가 낮게 깔려 플레이어를 옆에서 보게 된다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 구역 안에 있음")]
    private bool _isInside;

    private Bounds ZoneBounds => new Bounds(transform.position, zoneSize);

    private void Update()
    {
        if (sideViewCamera == null || player == null) return;

        Vector3 playerCenter = player.transform.TransformPoint(player.center);
        bool inside = ZoneBounds.Contains(playerCenter);

        if (inside)
        {
            // 붙어 있는 다른 구역에서 넘어왔다가 그 구역이 제한을 풀어버린 경우에도 다시 걸 수 있게,
            // 막 들어온 순간뿐 아니라 제한이 꺼져 있으면 다시 건다.
            if (!_isInside || !sideViewCamera.HasMaxHeight)
                sideViewCamera.SetMaxHeight(maxCameraY, this);
        }
        else if (_isInside)
        {
            sideViewCamera.ClearMaxHeight(this);
        }

        _isInside = inside;
    }

    private void OnDisable()
    {
        // 구역 안에 있는 채로 이 오브젝트가 꺼지거나 씬이 바뀌어도 카메라 제한이 남지 않게 푼다.
        if (_isInside && sideViewCamera != null)
            sideViewCamera.ClearMaxHeight(this);
        _isInside = false;
    }

    private void OnDrawGizmosSelected()
    {
        Bounds b = ZoneBounds;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(b.center, b.size);

        // 카메라가 넘지 못하는 높이를 반투명한 면으로 보여 준다.
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawCube(new Vector3(b.center.x, maxCameraY, b.center.z), new Vector3(b.size.x, 0.02f, b.size.z));
    }
}
