using Sirenix.OdinInspector;
using UnityEngine;

// 꽃이 핀 부서진 나무(BlossomStripTree) 위를 덮는 트리거다.
// 꽃이 붙어 있는 동안 플레이어가 덤불에 닿거나 넘으려 하면, 들어온 쪽으로 튕겨내고 덤불을 출렁이게 해
// "투명벽"이 아니라 "꽃덤불에 막혔다"는 느낌을 준다. 꽃이 벗겨지면 BlossomStripTree가 이 트리거를 끈다.
[RequireComponent(typeof(Collider))]
public class BlossomRepelZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고, 덤불의 좌우 폭과 2단 점프 최고점(바닥 위 약 4.1m)보다 조금 높은 곳까지 덮는다.")]
    [Title("연결")]
    [SerializeField, LabelText("꽃 나무")]
    private BlossomStripTree tree;
    // 튕길 때 흔들림/꽃잎 연출을 재생할 나무다.

    [Title("튕김 설정")]
    [SerializeField, LabelText("튕기는 힘")]
    private float pushSpeed = 14f;
    // 들어온 반대쪽(바깥)으로 밀어내는 수평 속도다. 이동 속도(5)보다 충분히 커야 계속 밀고 들어와도 넘어가지 못한다.

    [SerializeField, LabelText("위로 튕기는 힘")]
    private float upSpeed = 3f;
    // 튕길 때 살짝 떠오르게 하는 수직 속도다. 0이면 옆으로만 밀린다.

    [SerializeField, LabelText("튕김 간격(초)")]
    private float repelCooldown = 0.25f;
    // 트리거 안에 머무는 동안 이 간격마다 다시 튕겨낸다. 너무 짧으면 연출이 계속 겹쳐 떨림처럼 보인다.

    private Collider _zone;
    private Vector3 _pushDirection;
    // 플레이어가 들어온 쪽을 기억해 둔 바깥 방향이다. 덤불 한가운데를 넘어서도 반대편으로 밀어주지 않게 하기 위함이다.
    private float _lastRepelTime = -999f;

    private void Awake()
    {
        _zone = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _pushDirection = GetOutwardDirection(player);
        Repel(player);
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (Time.time - _lastRepelTime >= repelCooldown)
            Repel(player);
    }

    // 덤불 중심에서 플레이어 쪽을 향하는 바깥 방향이다.
    // 사이드뷰는 X축 좌우로만, 탑다운은 바닥(XZ) 방향으로 계산한다.
    private Vector3 GetOutwardDirection(PlayerController player)
    {
        Vector3 offset = player.transform.position - _zone.bounds.center;

        if (player.CurrentViewMode == ViewMode.SideView)
            return new Vector3(offset.x >= 0f ? 1f : -1f, 0f, 0f);

        offset.y = 0f;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : -player.FacingDirection;
    }

    private void Repel(PlayerController player)
    {
        if (tree != null && tree.IsStripped) return;

        _lastRepelTime = Time.time;

        // 선풍기 블라스트 반동과 같은 경로(SetBlast)로 밀어내므로 CharacterController 이동 흐름을 그대로 탄다.
        player.SetBlast(_pushDirection * pushSpeed + Vector3.up * upSpeed);

        if (tree != null)
            tree.PlayRepelFeedback();
    }
}
