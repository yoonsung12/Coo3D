using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 게임의 계절 상태(봄/여름/가을/겨울)를 관리하는 매니저다.
// 씬에 반드시 하나만 존재해야 한다. CurrentSeason, OnSeasonChanged는 static이라
// 다른 스크립트(얼음/문 같은 퍼즐, UI, 플레이어 디버프, 환경 이펙트)가
// SeasonManager를 Inspector로 직접 연결하지 않아도 계절 상태를 참조할 수 있다.
public enum SeasonType
{
    Spring, // 봄
    Summer, // 여름
    Autumn, // 가을
    Winter  // 겨울
}

public class SeasonManager : MonoBehaviour
{
    [Title("계절 설정")]
    [SerializeField, LabelText("시작 계절")]
    private SeasonType startSeason = SeasonType.Spring;
    // Inspector에서 씬이 시작될 때 적용할 계절을 지정한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 계절")]
    public static SeasonType CurrentSeason { get; private set; }
    // 다른 스크립트에서 SeasonManager.CurrentSeason으로 바로 읽을 수 있는 현재 계절 상태다.

    // 계절이 바뀔 때마다 호출되는 static 이벤트다.
    // 얼음(IMeltable), 문, UI, 디버프 등은 OnEnable에서 이 이벤트를 구독하고
    // OnDisable에서 반드시 구독을 해제해야 한다 (해제하지 않으면 메모리 누수로 이어진다).
    public static event Action<SeasonType> OnSeasonChanged;

    private void Awake()
    {
        CurrentSeason = startSeason;
    }

    // 계절을 전환한다. 같은 계절로 전환을 시도하면 아무 일도 하지 않는다.
    // 실제 전환 트리거(시즌 게이지, 장치 상호작용 등)는 이후 별도 스크립트에서
    // 이 메서드를 호출하는 방식으로 연결한다.
    public static void SetSeason(SeasonType newSeason)
    {
        if (newSeason == CurrentSeason) return;

        CurrentSeason = newSeason;
        OnSeasonChanged?.Invoke(newSeason);
    }

    [Title("테스트")]
    [Button("봄으로 전환")]
    private void TestSpring() => SetSeason(SeasonType.Spring);

    [Button("여름으로 전환")]
    private void TestSummer() => SetSeason(SeasonType.Summer);

    [Button("가을로 전환")]
    private void TestAutumn() => SetSeason(SeasonType.Autumn);

    [Button("겨울로 전환")]
    private void TestWinter() => SetSeason(SeasonType.Winter);
}
