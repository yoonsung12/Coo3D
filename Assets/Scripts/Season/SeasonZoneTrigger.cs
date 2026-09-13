using Sirenix.OdinInspector;
using UnityEngine;

// 데모 씬에서 계절 구역을 걸어서 지나갈 때 자동으로 계절을 전환하기 위한 트리거다.
// 플레이어가 이 콜라이더 안으로 들어오면 지정된 계절로 SeasonManager.SetSeason()을 호출한다.
[RequireComponent(typeof(Collider))]
public class SeasonZoneTrigger : MonoBehaviour
{
    [Title("구역 계절 설정")]
    [SerializeField, LabelText("이 구역의 계절")]
    private SeasonType targetSeason;
    // Inspector에서 이 구역에 들어왔을 때 전환할 계절(봄/여름/가을/겨울)을 지정한다.

    private void Awake()
    {
        // 플레이어가 그냥 통과해야 하므로 트리거로 강제한다.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 같은 계절로 다시 들어와도 SetSeason 내부에서 무시하므로 중복 호출을 걱정하지 않아도 된다.
        SeasonManager.SetSeason(targetSeason);
    }
}
