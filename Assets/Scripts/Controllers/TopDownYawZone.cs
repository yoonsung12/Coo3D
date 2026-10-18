using Sirenix.OdinInspector;
using UnityEngine;

// 탑다운 카메라가 바라보는 방향(Y 회전)을 바꾸는 구간 트리거다.
// 기본 탑다운은 +Z(Y 회전 0도)를 바라보지만, 퍼즐 구역처럼 다른 방향에서 내려다보는 게 나은 곳에 이 구역을 덮어 두면
// 플레이어가 안에 있는 동안 카메라가 지정한 방향으로 돌고, 화살표 이동도 화면 기준으로 함께 돌아간다.
// 구역을 나가면 기본 방향(0도)으로 되돌린다.
[RequireComponent(typeof(Collider))]
public class TopDownYawZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고, 방향을 바꿀 탑다운 공간을 덮도록 크기를 맞춘다.\n" +
             "탑다운 구역(ViewModeZone) 안에 둔다. 사이드뷰에는 영향이 없다.")]
    [Title("방향 설정")]
    [SerializeField, LabelText("탑다운 카메라 방향 (Y 회전)")]
    private float yaw = 90f;
    // 카메라가 바라볼 월드 방향을 Y축 회전 각도로 넣는다. 0 = +Z, 90 = +X, 180 = -Z, -90 = -X.
    // 이 구역 안에서는 ↑ 화살표가 이 방향으로 이동한다.

    public float Yaw => yaw;

    // Enter 대신 Stay를 쓰는 이유: 겹쳐 있는 다른 구역에서 넘어온 같은 프레임에 값이 덮어써져도
    // 다음 물리 프레임에 다시 이 구역의 방향으로 바로잡기 위해서다. (값이 같으면 PlayerController가 바로 return한다.)
    private void OnTriggerStay(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        player.SetTopDownYaw(yaw);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 구역을 여러 박스로 이어 붙인 경우, 아직 다른 TopDownYawZone 안에 있으면 방향을 유지한다.
        if (IsStillInsideAnyYawZone(player)) return;

        player.SetTopDownYaw(0f);
    }

    // 플레이어 몸통 중심이 아직 TopDownYawZone 콜라이더 안에 있는지 검사한다.
    // QueryTriggerInteraction.Collide: 구역 콜라이더는 트리거라 이 옵션이 있어야 검사에 잡힌다.
    private bool IsStillInsideAnyYawZone(PlayerController player)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        Vector3 center = player.transform.TransformPoint(cc.center);
        Collider[] hits = Physics.OverlapSphere(center, 0.1f, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.GetComponent<TopDownYawZone>() != null) return true;
        }
        return false;
    }

    [Button("플레이어에 이 방향 적용 (Play Mode 테스트)")]
    private void TestApplyYaw()
    {
        // 플레이어를 구역까지 걸어가지 않고도 카메라 회전/이동 방향을 바로 확인하기 위한 버튼이다.
        if (PlayerHealth.Instance == null) return;
        PlayerController player = PlayerHealth.Instance.GetComponent<PlayerController>();
        if (player != null)
            player.SetTopDownYaw(yaw);
    }
}
