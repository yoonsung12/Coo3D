using System.Collections.Generic;
using UnityEngine;

// 4개 계절의 게이지 칸 수를 보관하는 ScriptableObject다.
// 총 10칸이 채워지면 SeasonGaugeManager가 디버프를 발동한다.
[CreateAssetMenu(menuName = "Season/GaugeModel", fileName = "GaugeModel")]
public class GaugeModel : ScriptableObject
{
    public const int TOTAL_SLOTS = 10;
    // 게이지 총 칸 수다. 4개 계절 합산이 이 값에 도달하면 디버프가 발동된다.

    [SerializeField] private int spring;
    [SerializeField] private int summer;
    [SerializeField] private int autumn;
    [SerializeField] private int winter;
    // Inspector에서 현재 각 계절 게이지 칸 수를 실시간으로 확인할 수 있다 (읽기용).

    // 슬롯이 추가된 순서대로 계절 타입을 기록한다. UI에서 순서대로 색을 입힐 때 사용한다.
    private readonly List<SeasonType> slotOrder = new List<SeasonType>();

    public int Spring => spring;
    public int Summer => summer;
    public int Autumn => autumn;
    public int Winter => winter;

    // 4개 계절 게이지의 합계다. TOTAL_SLOTS에 도달하면 디버프 발동 조건이 된다.
    public int TotalFilled => spring + summer + autumn + winter;
    public bool IsFull => TotalFilled >= TOTAL_SLOTS;

    // 추가된 순서가 보존된 슬롯 목록이다. UI가 각 슬롯 색을 정할 때 사용한다.
    public IReadOnlyList<SeasonType> SlotOrder => slotOrder;

    public int Get(SeasonType s) => s switch
    {
        SeasonType.Spring => spring,
        SeasonType.Summer => summer,
        SeasonType.Autumn => autumn,
        SeasonType.Winter => winter,
        _ => 0
    };

    // 해당 계절 게이지를 slots 칸만큼 증가시킨다.
    // 남은 여유 칸을 초과하지 않도록 제한하고, 추가 순서를 slotOrder에 기록한다.
    public void Add(SeasonType s, int slots)
    {
        // 이미 꽉 찼으면 추가하지 않는다.
        int remaining = TOTAL_SLOTS - TotalFilled;
        if (remaining <= 0) return;

        int add = Mathf.Clamp(slots, 0, remaining);
        switch (s)
        {
            case SeasonType.Spring: spring += add; break;
            case SeasonType.Summer: summer += add; break;
            case SeasonType.Autumn: autumn += add; break;
            case SeasonType.Winter: winter += add; break;
        }

        // 추가된 칸 수만큼 slotOrder에 계절을 기록한다.
        for (int i = 0; i < add; i++)
            slotOrder.Add(s);
    }

    // 가장 많이 채워진 계절을 반환한다.
    // 동률인 계절이 여럿이면 그 중 랜덤으로 하나를 선택한다.
    public SeasonType DominantSeason()
    {
        int max = Mathf.Max(spring, summer, autumn, winter);

        // 최댓값과 같은 계절을 모두 후보에 담는다.
        var candidates = new List<SeasonType>(4);
        if (spring == max) candidates.Add(SeasonType.Spring);
        if (summer == max) candidates.Add(SeasonType.Summer);
        if (autumn == max) candidates.Add(SeasonType.Autumn);
        if (winter == max) candidates.Add(SeasonType.Winter);

        return candidates[Random.Range(0, candidates.Count)];
    }

    public void ResetAll()
    {
        spring = summer = autumn = winter = 0;
        // 순서 기록도 함께 초기화해 UI 슬롯이 모두 빈 상태로 돌아가게 한다.
        slotOrder.Clear();
    }
}
