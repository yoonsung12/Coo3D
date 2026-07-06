using Sirenix.OdinInspector;
using UnityEngine;

// 우산을 펼친 플레이어를 밀거나 띄우는 바람 구역이다.
// 수직 성분이 우세한 방향이면 접지 여부와 무관하게, 수평 성분이 우세하면
// 공중에서만 작동한다 (구체적인 힘 적용 판정은 다음 태스크에서 추가한다).
[RequireComponent(typeof(Collider))]
public class WindZone : MonoBehaviour
{
    public enum WindMode { Constant, Intermittent }

    [Title("바람 설정")]
    [SerializeField, LabelText("바람 방향")]
    private Vector3 windDirection = Vector3.right;
    // Inspector에서 바람이 부는 방향을 지정한다. 정규화해서 사용하므로 크기는 상관없다.
    // 예: (1,0,0)은 오른쪽, (0,1,0)은 위쪽 — 위쪽 방향이면 접지 중에도 작동하는 수직풍이 된다.

    [SerializeField, LabelText("바람 세기")]
    private float windStrength = 6f;
    // 값이 클수록 플레이어가 더 강하게 밀리거나 빠르게 떠오른다.

    [Title("간헐풍 설정")]
    [SerializeField, LabelText("바람 모드")]
    private WindMode windMode = WindMode.Constant;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("존 안의 플레이어")]
    private PlayerController _playerInZone;

    private Collider _collider;

    // 수직 성분이 수평 성분보다 크면 수직풍으로 취급한다.
    // 수직풍은 지상에서 우산을 펼치기만 해도 작동해야 "위로 슈웅" 이동이 가능하기 때문이다.
    private bool IsVerticalDominant => Mathf.Abs(windDirection.y) > Mathf.Abs(windDirection.x);

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 플레이어가 그냥 통과해야 하므로 트리거로 강제한다.
        _collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInZone = player;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != _playerInZone) return;

        _playerInZone = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();

        // 지속풍은 파란색, 간헐풍은 하늘색으로 구분해 씬 뷰에서 바로 모드를 알 수 있게 한다.
        Gizmos.color = windMode == WindMode.Constant
            ? new Color(0.2f, 0.4f, 1f)
            : new Color(0.4f, 0.8f, 1f);

        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        // 존 중심에서 바람 방향으로 화살표(선 + 화살촉)를 그려 방향을 미리 확인할 수 있게 한다.
        Vector3 dir = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector3.right;
        Vector3 start = transform.position;
        Vector3 end = start + dir * 2f;
        Gizmos.DrawLine(start, end);

        Vector3 back = -dir * 0.4f;
        Vector3 side = Vector3.Cross(dir, Vector3.forward).normalized * 0.3f;
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(dir, Vector3.up).normalized * 0.3f;

        Gizmos.DrawLine(end, end + back + side);
        Gizmos.DrawLine(end, end + back - side);
    }
#endif
}
