using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 계절 게이지를 관리한다. 4개 계절 게이지 합산이 10칸이 되면 가장 많이 쌓인 계절의
// 디버프를 발동한다. 동률이면 해당 계절들 중 랜덤으로 하나를 선택한다.
// SeasonManager와 마찬가지로 씬에 반드시 하나만 존재해야 하며, AddGauge 등은 static으로
// 노출해 다른 스크립트(낙하물 등)가 Inspector 참조 없이 바로 호출할 수 있게 한다.
public class SeasonGaugeManager : MonoBehaviour
{
    [Title("게이지 데이터")]
    [SerializeField, LabelText("게이지 모델")]
    private GaugeModel gaugeModel;
    // Inspector에서 GaugeModel ScriptableObject 에셋을 연결한다.

    [Title("디버프 설정")]
    [SerializeField, LabelText("디버프 지속 시간")]
    private float debuffDuration = 5f;
    // 디버프가 발동된 뒤 유지되는 시간(초)이다. 이 시간이 지나면 디버프가 자동 해제된다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 디버프")]
    public static DebuffType CurrentDebuff { get; private set; } = DebuffType.None;

    [ReadOnly, ShowInInspector, LabelText("봄 게이지")]
    private int SpringSlots => _model != null ? _model.Spring : 0;

    [ReadOnly, ShowInInspector, LabelText("여름 게이지")]
    private int SummerSlots => _model != null ? _model.Summer : 0;

    [ReadOnly, ShowInInspector, LabelText("가을 게이지")]
    private int AutumnSlots => _model != null ? _model.Autumn : 0;

    [ReadOnly, ShowInInspector, LabelText("겨울 게이지")]
    private int WinterSlots => _model != null ? _model.Winter : 0;

    // 디버프 발동/해제 시 발행된다. DebuffController 등에서 구독한다.
    public static event Action<DebuffType> OnDebuffTriggered;

    // 게이지 칸이 추가되거나 리셋될 때마다 발행된다. UI에서 구독해 게이지바를 갱신한다.
    public static event Action OnGaugeChanged;

    private static GaugeModel _model;

    // TriggerDebuff가 static이라 새 디버프가 발동될 때 타이머를 함께 리셋하려면 static이어야 한다.
    private static float _debuffTimer;
    // 현재 디버프가 발동된 이후 경과 시간(초)이다.

    private void Awake()
    {
        _model = gaugeModel;

        // ScriptableObject는 에디터 플레이 세션 사이에 값이 남아있으므로 시작 시 초기화한다.
        _model.ResetAll();
        CurrentDebuff = DebuffType.None;
        _debuffTimer = 0f;
    }

    private void Update()
    {
        TickDebuffTimer();
    }

    // 디버프 지속 시간을 체크해 debuffDuration이 지나면 디버프를 해제한다.
    private void TickDebuffTimer()
    {
        if (CurrentDebuff == DebuffType.None) return;

        _debuffTimer += Time.deltaTime;
        if (_debuffTimer >= debuffDuration)
        {
            CurrentDebuff = DebuffType.None;
            _debuffTimer = 0f;

            // None을 전달해 DebuffController가 기존 디버프 효과를 원래대로 되돌리게 한다.
            OnDebuffTriggered?.Invoke(DebuffType.None);
        }
    }

    // 특정 계절 게이지를 slots 칸만큼 증가시킨다. 합산 10칸이 되면 즉시 디버프를 발동하고 게이지를 리셋한다.
    public static void AddGauge(SeasonType season, int slots = 1)
    {
        if (_model == null) return;

        _model.Add(season, slots);
        OnGaugeChanged?.Invoke();

        if (_model.IsFull)
            TriggerDebuff();
    }

    private static void TriggerDebuff()
    {
        SeasonType dominant = _model.DominantSeason();
        CurrentDebuff = SeasonToDebuff(dominant);
        // 디버프가 활성 상태에서 게이지가 다시 가득 차 새 디버프로 갱신되는 경우를 대비해
        // 지속 시간을 항상 0부터 다시 세도록 명시적으로 리셋한다.
        _debuffTimer = 0f;

        OnDebuffTriggered?.Invoke(CurrentDebuff);
        Debug.Log($"[SeasonGaugeManager] 디버프 발동: {CurrentDebuff} ({dominant})");

        _model.ResetAll();
        OnGaugeChanged?.Invoke();
    }

    // 계절 → 디버프 타입 매핑
    private static DebuffType SeasonToDebuff(SeasonType season) => season switch
    {
        SeasonType.Spring => DebuffType.Bound,
        SeasonType.Summer => DebuffType.Slow,
        SeasonType.Autumn => DebuffType.Reverse,
        SeasonType.Winter => DebuffType.Frozen,
        _ => DebuffType.None
    };

    [Title("테스트")]
    [Button("봄 게이지 1칸 추가")]
    private void TestAddSpring() => AddGauge(SeasonType.Spring);

    [Button("여름 게이지 1칸 추가")]
    private void TestAddSummer() => AddGauge(SeasonType.Summer);

    [Button("가을 게이지 1칸 추가")]
    private void TestAddAutumn() => AddGauge(SeasonType.Autumn);

    [Button("겨울 게이지 1칸 추가")]
    private void TestAddWinter() => AddGauge(SeasonType.Winter);
}
