using Sirenix.OdinInspector;
using UnityEngine;

// 여름 배경 비(파티클)를 항상 카메라 화면 주변에서 내리게 하는 연출 전용 컴포넌트다.
// 게이지/디버프 판정은 없고 보이기만 한다. (판정이 있는 비는 RainController가 따로 담당한다.)
// 카메라의 "위치만" 따라가고 회전은 따라가지 않는다.
// 카메라 자식으로 두면 탑다운/고정 카메라처럼 카메라가 꺾일 때 생성 박스도 같이 기울어 비가 옆으로 날리기 때문이다.
[RequireComponent(typeof(ParticleSystem))]
public class BackgroundRainFollower : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("따라갈 카메라")]
    private Transform targetCamera;
    // Main Camera를 연결한다.

    [SerializeField, LabelText("카메라 기준 위치")]
    private Vector3 offset = new Vector3(0f, 12f, 0f);
    // 카메라 위치에서 이만큼 떨어진 곳에 비 생성 박스를 둔다. Y가 클수록 더 높은 곳에서 떨어지기 시작한다.

    [Title("비를 끄는 구역")]
    [SerializeField, LabelText("비 안 오는 구역")]
    private Collider[] noRainAreas;
    // 지하처럼 비가 오면 안 되는 공간을 덮는 콜라이더를 연결한다. (예: Volume_Underground의 BoxCollider)
    // 카메라가 이 안에 있으면 새 빗방울 생성을 멈춘다. 이미 떨어지던 빗방울은 자연스럽게 사라진다.

    [ReadOnly, ShowInInspector, LabelText("비 끄는 구역 안")]
    private bool _isInNoRainArea;

    private ParticleSystem _rain;

    private void Awake()
    {
        _rain = GetComponent<ParticleSystem>();
    }

    // 카메라는 LateUpdate에서 플레이어를 따라 움직이므로, 그 뒤 프레임 끝에 맞춰 위치를 옮긴다.
    private void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 cameraPosition = targetCamera.position;
        transform.position = cameraPosition + offset;

        bool inNoRainArea = IsInNoRainArea(cameraPosition);
        if (inNoRainArea == _isInNoRainArea) return;

        // 구역을 드나든 순간에만 재생/정지를 바꾼다. 매 프레임 Play/Stop을 부르면 파티클이 재시작될 수 있다.
        _isInNoRainArea = inNoRainArea;
        if (inNoRainArea)
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        else
            _rain.Play(true);
    }

    // 트리거 이벤트 대신 위치로 직접 검사한다. 리스폰/텔레포트로 순간이동하면 OnTriggerExit가 오지 않기 때문이다.
    private bool IsInNoRainArea(Vector3 position)
    {
        if (noRainAreas == null) return false;

        foreach (Collider area in noRainAreas)
        {
            if (area != null && area.bounds.Contains(position))
                return true;
        }
        return false;
    }
}
