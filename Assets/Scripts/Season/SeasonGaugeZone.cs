using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 들어오면 지정한 계절 게이지를 올리는 구역이다. (예: 여름 구역의 물)
// 들어오는 순간 한 번 오르고, 계속 머무르면 일정 간격마다 다시 오른다.
// 낙하물(BaseHazard)과 달리 제자리에 놓인 지형에 붙여 쓰는 용도라 별도 컴포넌트로 분리했다.
[RequireComponent(typeof(Collider))]
public class SeasonGaugeZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고 감지할 범위를 덮는다.\n" +
             "'밟는 표면'을 연결하면 구역 안에서도 그 콜라이더를 실제로 밟고 있을 때만 게이지가 오른다.")]
    [Title("시즌 게이지 설정")]
    [SerializeField, LabelText("계절 타입")]
    private SeasonType seasonType = SeasonType.Summer;
    // 이 구역에 닿았을 때 오를 계절 게이지다.

    [SerializeField, LabelText("게이지 칸 수")]
    private int gaugeSlots = 1;
    // 한 번 오를 때 증가할 칸 수다.

    [SerializeField, LabelText("게이지 상승 간격(초)")]
    private float tickInterval = 1.5f;
    // 계속 머무를 때 이 시간마다 게이지가 다시 오른다. 작을수록 빨리 찬다.

    [SerializeField, LabelText("밟는 표면 (선택)")]
    private Collider surfaceCollider;
    // 물 표면처럼 출렁이는 메쉬는 높이만으로 옆 땅/다리와 구분할 수 없어서, 발밑 콜라이더가 이것일 때만 센다.
    // 비워두면 구역 안에 들어와 있기만 해도 게이지가 오른다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("게이지 적용 중")]
    private bool _isApplying;

    [ReadOnly, ShowInInspector, LabelText("머문 시간")]
    private float _stayTimer;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // 머무는 동안 매 물리 프레임 호출된다. 표면을 밟기 시작한 순간 한 번 올리고, 이후 tickInterval마다 올린다.
    private void OnTriggerStay(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        bool applying = surfaceCollider == null || IsStandingOnSurface(player);

        // 밟기 시작한 순간(이전 프레임엔 안 밟고 있었음): 살짝 밟기만 해도 대가가 있게 바로 한 번 올린다.
        if (applying && !_isApplying)
        {
            _stayTimer = 0f;
            SeasonGaugeManager.AddGauge(seasonType, gaugeSlots);
        }
        else if (applying)
        {
            // Time.deltaTime을 누적해 프레임 수와 상관없이 실제 머문 시간 기준으로 오르게 한다.
            _stayTimer += Time.deltaTime;
            if (_stayTimer >= tickInterval)
            {
                _stayTimer -= tickInterval;
                SeasonGaugeManager.AddGauge(seasonType, gaugeSlots);
            }
        }

        _isApplying = applying;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        _isApplying = false;
        _stayTimer = 0f;
    }

    // 플레이어 발밑(피벗이 발 위치)에서 아래로 짧게 쏴서, 바로 밑 콜라이더가 지정한 표면인지 확인한다.
    // 점프 중이라 발밑이 멀면 밟지 않은 것으로 본다.
    private bool IsStandingOnSurface(PlayerController player)
    {
        Vector3 origin = player.transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return hit.collider == surfaceCollider;
    }
}
