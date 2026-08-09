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

    [Title("게이지 사운드")]
    [SerializeField, LabelText("사운드 재생용 AudioSource")]
    private AudioSource audioSource;
    // 게이지 적립음과 디버프 발동음을 재생한다. 비워두면 소리 없이 넘어간다.

    [SerializeField, LabelText("봄(꽃가루) 접촉음")]
    private AudioClip springGaugeSound;

    [SerializeField, LabelText("여름(빗방울) 접촉음")]
    private AudioClip summerGaugeSound;

    [SerializeField, LabelText("가을(은행) 접촉음")]
    private AudioClip autumnGaugeSound;

    [SerializeField, LabelText("겨울(눈송이) 접촉음")]
    private AudioClip winterGaugeSound;

    [Title("디버프 사운드")]
    [SerializeField, LabelText("속박(봄) 발동음")]
    private AudioClip boundSound;

    [SerializeField, LabelText("이속 저하(여름) 발동음")]
    private AudioClip slowSound;

    [SerializeField, LabelText("방향 반전(가을) 발동음")]
    private AudioClip reverseSound;

    [SerializeField, LabelText("빙결(겨울) 발동음")]
    private AudioClip frozenSound;

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

    // Inspector의 debuffDuration(인스턴스 필드) 값을 그대로 캐시해 static 프로퍼티에서 읽을 수 있게 한다.
    private static float _debuffDuration;

    // AddGauge/TriggerDebuff가 static이라 사운드 재생에 필요한 참조도 _model과 같은 방식으로 static 캐시에 담는다.
    private static AudioSource _audioSource;
    private static AudioClip _springGaugeSound;
    private static AudioClip _summerGaugeSound;
    private static AudioClip _autumnGaugeSound;
    private static AudioClip _winterGaugeSound;
    private static AudioClip _boundSound;
    private static AudioClip _slowSound;
    private static AudioClip _reverseSound;
    private static AudioClip _frozenSound;

    // 디버프 UI(SeasonGaugeUI)가 남은 시간을 표시할 때 사용하는 읽기 전용 프로퍼티다.
    public static float DebuffDuration => _debuffDuration;
    public static float DebuffTimeRemaining => Mathf.Max(0f, _debuffDuration - _debuffTimer);

    private void Awake()
    {
        _model = gaugeModel;
        _debuffDuration = debuffDuration;

        _audioSource = audioSource;
        _springGaugeSound = springGaugeSound;
        _summerGaugeSound = summerGaugeSound;
        _autumnGaugeSound = autumnGaugeSound;
        _winterGaugeSound = winterGaugeSound;
        _boundSound = boundSound;
        _slowSound = slowSound;
        _reverseSound = reverseSound;
        _frozenSound = frozenSound;

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

        int filledBefore = _model.TotalFilled;
        _model.Add(season, slots);
        OnGaugeChanged?.Invoke();

        // 실제로 칸이 늘어난 경우에만 접촉음을 재생한다. 이미 꽉 차 있어서 무시된 경우는 제외한다.
        if (_model.TotalFilled > filledBefore)
            PlayGaugeSound(season);

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
        PlayDebuffSound(CurrentDebuff);
        Debug.Log($"[SeasonGaugeManager] 디버프 발동: {CurrentDebuff} ({dominant})");

        _model.ResetAll();
        OnGaugeChanged?.Invoke();
    }

    // 게이지가 실제로 오른 계절에 맞는 접촉음을 재생한다.
    private static void PlayGaugeSound(SeasonType season)
    {
        AudioClip clip = season switch
        {
            SeasonType.Spring => _springGaugeSound,
            SeasonType.Summer => _summerGaugeSound,
            SeasonType.Autumn => _autumnGaugeSound,
            SeasonType.Winter => _winterGaugeSound,
            _ => null
        };
        PlaySound(clip);
    }

    // 발동된 디버프에 맞는 소리를 재생한다.
    private static void PlayDebuffSound(DebuffType debuff)
    {
        AudioClip clip = debuff switch
        {
            DebuffType.Bound => _boundSound,
            DebuffType.Slow => _slowSound,
            DebuffType.Reverse => _reverseSound,
            DebuffType.Frozen => _frozenSound,
            _ => null
        };
        PlaySound(clip);
    }

    // DoorController/PressButton과 같은 방식으로, AudioSource나 클립이 비어 있으면 조용히 넘어간다.
    private static void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
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
