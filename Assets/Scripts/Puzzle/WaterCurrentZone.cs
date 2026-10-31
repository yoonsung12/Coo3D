using Sirenix.OdinInspector;
using UnityEngine;

// 물살 구역이다. 이 트리거 안에 있는 FloatTube를 일정한 방향으로 계속 밀어낸다.
// 플레이어가 선풍기 반동으로 거슬러 올라가야 하는 "역류" 구간에 쓴다. FloatTube 외에는 아무것도 밀지 않는다.
[RequireComponent(typeof(Collider))]
public class WaterCurrentZone : MonoBehaviour
{
    [Title("물살 설정")]
    [SerializeField, LabelText("물살 방향 (Y 무시)")]
    private Vector3 direction = Vector3.forward;
    // 물이 흐르는 월드 방향이다. 수평(XZ)만 쓰고 자동으로 길이 1로 맞춘다.

    [SerializeField, LabelText("물살 세기 (m/s²)")]
    private float strength = 3f;
    // 튜브에 주는 가속도다. 클수록 거슬러 가기 힘들다. 튜브 최대 속도와 물 저항이 있어 무한히 빨라지지는 않는다.

    [Title("연출 (선택)")]
    [SerializeField, LabelText("거품 파티클")]
    private ParticleSystem foam;
    // 물살 방향으로 흐르는 거품이다. 비워 두면 연출 없이 동작한다. 재생은 파티클 자체 설정(Play On Awake)을 따른다.

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        // 트리거가 아니면 튜브를 밀어주지 못하므로 강제로 켠다.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        FloatTube tube = other.GetComponent<FloatTube>();
        if (tube == null) return;

        Rigidbody tubeBody = tube.GetComponent<Rigidbody>();
        if (tubeBody == null) return;

        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.0001f) return;

        // OnTriggerStay는 물리 스텝(FixedUpdate 주기)에 맞춰 호출된다.
        // ForceMode.Acceleration은 내부에서 fixedDeltaTime을 곱해 주므로 Time.deltaTime을 따로 곱하지 않는다.
        tubeBody.AddForce(flat.normalized * strength, ForceMode.Acceleration);
    }

    private void OnDrawGizmos()
    {
        Collider zone = GetComponent<Collider>();
        if (zone == null) return;

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(zone.bounds.center, zone.bounds.size);

        // 물살 방향 화살표
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.0001f) return;
        flat.Normalize();
        Vector3 from = zone.bounds.center;
        Vector3 to = from + flat * 3f;
        Gizmos.DrawLine(from, to);
        Vector3 side = Vector3.Cross(Vector3.up, flat) * 0.5f;
        Gizmos.DrawLine(to, to - flat * 0.8f + side);
        Gizmos.DrawLine(to, to - flat * 0.8f - side);
    }
}
