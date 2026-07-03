using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// SeasonGaugeManager/GaugeModel의 게이지·디버프 상태를 화면에 표시한다.
// 게이지는 10칸으로 표시하며, GaugeModel.SlotOrder(채워진 순서)를 읽어 각 칸을 해당 계절 색으로 칠한다.
// 디버프가 발동되면 별도 패널이 나타나 어떤 디버프인지와 남은 시간을 보여준다.
// [ExecuteAlways]로 Play Mode에 들어가지 않아도 에디터에서 칸 배치를 바로 확인할 수 있다.
[ExecuteAlways]
public class SeasonGaugeUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("게이지 모델")]
    private GaugeModel gaugeModel;
    // Inspector에서 SeasonGaugeManager와 동일한 GaugeModel 에셋을 연결한다.

    [SerializeField, LabelText("게이지 칸이 배치될 부모 (HorizontalLayoutGroup)")]
    private RectTransform slotContainer;

    [Title("게이지 칸 설정")]
    [SerializeField, LabelText("칸 크기")]
    private Vector2 slotSize = new Vector2(28f, 28f);

    [SerializeField, LabelText("빈 칸 색상")]
    private Color emptySlotColor = new Color(1f, 1f, 1f, 0.15f);

    [Title("계절별 색상")]
    [SerializeField, LabelText("봄")]
    private Color springColor = new Color(1f, 0.72f, 0.78f);

    [SerializeField, LabelText("여름")]
    private Color summerColor = new Color(1f, 0.62f, 0.25f);

    [SerializeField, LabelText("가을")]
    private Color autumnColor = new Color(0.71f, 0.4f, 0.11f);

    [SerializeField, LabelText("겨울")]
    private Color winterColor = new Color(0.66f, 0.85f, 1f);

    [Title("칸 채워짐 연출")]
    [SerializeField, LabelText("펀치 강도")]
    private float punchScale = 0.3f;

    [SerializeField, LabelText("펀치 지속 시간")]
    private float punchDuration = 0.2f;

    [Title("디버프 패널 연결")]
    [SerializeField, LabelText("디버프 패널 (CanvasGroup)")]
    private CanvasGroup debuffPanel;
    // 디버프가 없을 때는 알파 0으로 숨긴다. Inspector에서 패널 루트의 CanvasGroup을 연결한다.

    [SerializeField, LabelText("디버프 이름 텍스트")]
    private Text debuffLabel;

    [SerializeField, LabelText("디버프 남은시간 바 (Fill)")]
    private Image debuffTimerFill;

    [Title("디버프 패널 연출")]
    [SerializeField, LabelText("페이드 시간")]
    private float panelFadeDuration = 0.25f;

    private Image[] _slots;
    private Tween[] _slotTweens;
    private int _previousFilledCount;
    private Tween _panelFadeTween;

    private void Awake()
    {
        BuildSlots();

        if (debuffPanel != null)
            debuffPanel.alpha = 0f;
    }

    // slotContainer 아래에 이미 있던 칸을 지우고 GaugeModel.TOTAL_SLOTS개만큼 새로 생성한다.
    private void BuildSlots()
    {
        if (slotContainer == null) return;

        ClearSlots();

        int count = GaugeModel.TOTAL_SLOTS;
        _slots = new Image[count];
        _slotTweens = new Tween[count];

        for (int i = 0; i < count; i++)
        {
            var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slotGo.transform.SetParent(slotContainer, false);

            var rect = slotGo.GetComponent<RectTransform>();
            rect.sizeDelta = slotSize;

            var img = slotGo.GetComponent<Image>();
            img.color = emptySlotColor;

            _slots[i] = img;
        }

        _previousFilledCount = 0;
    }

    private void ClearSlots()
    {
        for (int i = slotContainer.childCount - 1; i >= 0; i--)
        {
            var child = slotContainer.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void OnEnable()
    {
        SeasonGaugeManager.OnGaugeChanged += RefreshSlots;
        SeasonGaugeManager.OnDebuffTriggered += HandleDebuffTriggered;
    }

    private void OnDisable()
    {
        SeasonGaugeManager.OnGaugeChanged -= RefreshSlots;
        SeasonGaugeManager.OnDebuffTriggered -= HandleDebuffTriggered;
    }

    private void OnDestroy()
    {
        if (_slotTweens != null)
            foreach (var t in _slotTweens) t?.Kill();

        _panelFadeTween?.Kill();
    }

    // 디버프 패널이 보이는 동안 매 프레임 남은 시간 비율을 갱신한다.
    private void Update()
    {
        if (debuffTimerFill == null) return;
        if (SeasonGaugeManager.CurrentDebuff == DebuffType.None) return;
        if (SeasonGaugeManager.DebuffDuration <= 0f) return;

        debuffTimerFill.fillAmount = SeasonGaugeManager.DebuffTimeRemaining / SeasonGaugeManager.DebuffDuration;
    }

    // GaugeModel.SlotOrder를 읽어 채워진 칸은 해당 계절 색으로, 나머지는 빈 색으로 칠한다.
    private void RefreshSlots()
    {
        if (_slots == null || gaugeModel == null) return;

        var slotOrder = gaugeModel.SlotOrder;

        for (int i = 0; i < _slots.Length; i++)
        {
            bool isFilled = i < slotOrder.Count;
            _slots[i].color = isFilled ? ColorFor(slotOrder[i]) : emptySlotColor;
        }

        // 새로 채워진 칸에만 펀치 연출을 재생한다 (게이지가 리셋될 때는 재생하지 않는다).
        if (slotOrder.Count > _previousFilledCount)
        {
            for (int i = _previousFilledCount; i < slotOrder.Count; i++)
                PlaySlotPunch(i);
        }

        _previousFilledCount = slotOrder.Count;
    }

    private void PlaySlotPunch(int index)
    {
        _slotTweens[index]?.Kill();
        _slots[index].transform.localScale = Vector3.one;
        _slotTweens[index] = _slots[index].transform
            .DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 6, elasticity: 0.6f);
    }

    private Color ColorFor(SeasonType season) => season switch
    {
        SeasonType.Spring => springColor,
        SeasonType.Summer => summerColor,
        SeasonType.Autumn => autumnColor,
        SeasonType.Winter => winterColor,
        _ => emptySlotColor
    };

    // 디버프가 발동/해제될 때마다 SeasonGaugeManager가 호출한다.
    private void HandleDebuffTriggered(DebuffType debuff)
    {
        if (debuffPanel == null) return;

        _panelFadeTween?.Kill();

        if (debuff == DebuffType.None)
        {
            _panelFadeTween = debuffPanel.DOFade(0f, panelFadeDuration);
            return;
        }

        if (debuffLabel != null)
            debuffLabel.text = DebuffLabelText(debuff);

        if (debuffTimerFill != null)
            debuffTimerFill.fillAmount = 1f;

        _panelFadeTween = debuffPanel.DOFade(1f, panelFadeDuration);
    }

    private string DebuffLabelText(DebuffType debuff) => debuff switch
    {
        DebuffType.Bound => "속박",
        DebuffType.Slow => "이속 저하",
        DebuffType.Reverse => "방향 반전",
        DebuffType.Frozen => "빙결",
        _ => ""
    };
}
