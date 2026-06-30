using Sirenix.OdinInspector;
using UnityEngine;

// 사이드뷰 시점에서 플레이어의 X축 이동을 따라가는 카메라 추적 스크립트다.
// Y와 Z는 고정 offset을 유지하고 X축만 플레이어를 부드럽게 추적한다.
public class SideViewCamera : MonoBehaviour
{
    [Title("추적 대상")]
    [SerializeField, LabelText("플레이어")]
    private Transform target;
    // Inspector에서 Player 오브젝트의 Transform을 연결한다.

    [Title("카메라 오프셋")]
    [SerializeField, LabelText("X 오프셋")]
    private float offsetX = 0f;
    // 플레이어 X 위치에 더해지는 오프셋이다. 카메라를 좌우로 미세 조정할 때 사용한다.

    [SerializeField, LabelText("Y 오프셋 (높이)")]
    private float offsetY = 2f;
    // 카메라가 위치할 고정 높이다. 값이 클수록 더 높은 곳에서 바라본다.

    [SerializeField, LabelText("Z 오프셋 (거리)")]
    private float offsetZ = -10f;
    // 카메라가 플레이어로부터 Z축으로 떨어지는 거리다.
    // 음수값이며 절댓값이 클수록 카메라가 더 뒤로 물러서 화면이 넓어진다.

    [SerializeField, LabelText("추적 속도")]
    private float followSpeed = 8f;
    // 카메라가 플레이어를 따라가는 부드러움을 결정한다.
    // 값이 클수록 더 빠르게 따라가고, 작을수록 더 느리게 따라간다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 타겟 X")]
    private float _currentTargetX;

    private void LateUpdate()
    {
        // LateUpdate에서 카메라를 이동시켜 플레이어 이동이 완전히 처리된 뒤에 따라가게 한다.
        if (target == null) return;

        _currentTargetX = target.position.x;

        // X축만 플레이어를 따라가고, Y와 Z는 고정 offset 위치를 유지한다.
        // Lerp로 부드럽게 이동해 갑작스러운 카메라 이동을 방지한다.
        Vector3 targetPosition = new Vector3(
            _currentTargetX + offsetX,
            offsetY,
            offsetZ
        );

        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
    }

    [Button("현재 위치를 오프셋으로 저장")]
    private void CaptureCurrentOffset()
    {
        // 에디터에서 카메라를 원하는 위치에 놓은 뒤 이 버튼을 눌러 offset 값을 빠르게 설정할 수 있다.
        if (target == null) return;
        offsetX = transform.position.x - target.position.x;
        offsetY = transform.position.y;
        offsetZ = transform.position.z;
    }
}
