
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class SeasonGaugeUI : MonoBehaviour
{
    private const int DISPLAY_SLOTS = 5;

    [Title("연결")]
    [SerializeField, LabelText("게이지 모델")]
    private GaugeModel gaugeModel;

    [SerializeField, LabelText("게이지 칸 부모")]
    private RectTransform slotContainer;

    [Title("게이지 설정")]
    [SerializeField, LabelText("칸 크기")]
    private Vector2 slotSize = new Vector2(40f, 40f);

    [Title("계절별 아이콘")]
     [SerializeField, LabelText("빈칸 이미지"), PreviewField(60)]
    private Sprite emptySlotSprite;


    [Title("게이지 이미지")]
   [SerializeField, LabelText("봄 아이콘"), PreviewField(60)]
    private Sprite springSprite;
    [SerializeField, LabelText("여름 아이콘"), PreviewField(60)]
    private Sprite summerSprite;

    [SerializeField, LabelText("가을 아이콘"), PreviewField(60)]
    private Sprite autumnSprite;

    [SerializeField, LabelText("겨울 아이콘"), PreviewField(60)]
    private Sprite winterSprite;

    [Title("게이지 채워짐 애니메이션")]
    [SerializeField, LabelText("등장 확대 크기")]
    private float fillStartScale = 1.35f;

    [SerializeField, LabelText("등장 시간")]
    private float fillDuration = 0.2f;

    [Title("디버프 활성화 연출")]
    [SerializeField, LabelText("활성화 확대 크기")]
    private float activeScale = 1.2f;

    [SerializeField, LabelText("활성화 확대 시간")]
    private float activeDuration = 0.25f;

    [SerializeField, LabelText("반짝임 속도")]
    private float blinkDuration = 0.55f;

    [SerializeField, LabelText("반짝임 최소 밝기")]
    [Range(0f, 1f)]
    private float blinkMinAlpha = 0.55f;

    [SerializeField, LabelText("반짝임 크기 변화")]
    private float blinkScale = 0.07f;

    [SerializeField, LabelText("칸별 반짝임 간격")]
    private float blinkDelay = 0.08f;

    [Title("디버프 패널 연결")]
    [SerializeField, LabelText("디버프 패널")]
    private CanvasGroup debuffPanel;

    [SerializeField, LabelText("디버프 이름")]
    private Text debuffLabel;

    [SerializeField, LabelText("디버프 남은 시간 바")]
    private Image debuffTimerFill;

    [Title("디버프 패널 연출")]
    [SerializeField, LabelText("페이드 시간")]
    private float panelFadeDuration = 0.25f;

    private Image[] _slots;
    private Tween[] _slotTweens;
    private Sequence[] _blinkSequences;

    private Tween _panelFadeTween;

    private int _previousFilledCount;
    private bool _isDebuffActive;
    private DebuffType _activeDebuff = DebuffType.None;

    private void Awake()
    {
        BuildSlots();

        if (debuffPanel != null)
            debuffPanel.alpha = 0f;
    }

    private void OnEnable()
    {
        SeasonGaugeManager.OnGaugeChanged += RefreshSlots;
        SeasonGaugeManager.OnDebuffTriggered +=
            HandleDebuffTriggered;

        if (_slots == null)
            BuildSlots();

        if (SeasonGaugeManager.CurrentDebuff !=
            DebuffType.None)
        {
            HandleDebuffTriggered(
                SeasonGaugeManager.CurrentDebuff
            );
        }
        else
        {
            RefreshSlots(false);
        }
    }

    private void OnDisable()
    {
        SeasonGaugeManager.OnGaugeChanged -= RefreshSlots;
        SeasonGaugeManager.OnDebuffTriggered -=
            HandleDebuffTriggered;

        StopAllAnimations();
    }

    private void OnDestroy()
    {
        StopAllAnimations();
    }

    // 게이지 5칸 자동 생성
    private void BuildSlots()
    {
        if (slotContainer == null)
            return;

        ClearSlots();

        _slots = new Image[DISPLAY_SLOTS];
        _slotTweens = new Tween[DISPLAY_SLOTS];
        _blinkSequences = new Sequence[DISPLAY_SLOTS];

        for (int i = 0; i < DISPLAY_SLOTS; i++)
        {
            GameObject slotGo = new GameObject(
                $"Slot_{i}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement)
            );

            slotGo.transform.SetParent(
                slotContainer, false
            );

            RectTransform rect =
                slotGo.GetComponent<RectTransform>();

            rect.sizeDelta = slotSize;

            LayoutElement layout =
                slotGo.GetComponent<LayoutElement>();

            layout.preferredWidth = slotSize.x;
            layout.preferredHeight = slotSize.y;

            Image img = slotGo.GetComponent<Image>();

            img.sprite = emptySlotSprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;

            _slots[i] = img;
        }

        _previousFilledCount = 0;
    }

    private void ClearSlots()
    {
        if (slotContainer == null)
            return;

        for (int i = slotContainer.childCount - 1;
             i >= 0; i--)
        {
            GameObject child =
                slotContainer.GetChild(i).gameObject;

            // 기존 칸만 삭제
            if (!child.name.StartsWith("Slot_"))
                continue;

            child.transform.SetParent(null);

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    // 디버프 남은 시간 표시
    private void Update()
    {
        if (debuffTimerFill == null)
            return;

        if (SeasonGaugeManager.CurrentDebuff ==
            DebuffType.None)
            return;

        if (SeasonGaugeManager.DebuffDuration <= 0f)
            return;

        debuffTimerFill.fillAmount =
            SeasonGaugeManager.DebuffTimeRemaining /
            SeasonGaugeManager.DebuffDuration;
    }

    // 게이지 변경 이벤트
    private void RefreshSlots()
    {
        RefreshSlots(true);
    }

    private void RefreshSlots(bool playAnimation)
    {
        if (_slots == null || gaugeModel == null)
            return;

        // 디버프 중에는 전체 아이콘 유지
        if (_isDebuffActive)
            return;

        var slotOrder = gaugeModel.SlotOrder;

        int filledCount = Mathf.Min(
            slotOrder.Count,
            DISPLAY_SLOTS
        );

        for (int i = 0; i < DISPLAY_SLOTS; i++)
        {
            Image img = _slots[i];

            if (img == null)
                continue;

            // 채워진 칸
            if (i < filledCount)
            {
                Sprite sprite =
                    GetSeasonSprite(slotOrder[i]);

                img.sprite = sprite;
                img.color = sprite != null
                    ? Color.white
                    : Color.clear;
            }
            // 빈 칸
            else
            {
                img.sprite = emptySlotSprite;
                img.color = Color.white;
            }
        }

        // 새롭게 채워진 칸만 등장 애니메이션
        if (playAnimation &&
            filledCount > _previousFilledCount)
        {
            for (int i = _previousFilledCount;
                 i < filledCount; i++)
            {
                PlayFillAnimation(i);
            }
        }

        _previousFilledCount = filledCount;
    }

    // 계절별 아이콘 반환
    private Sprite GetSeasonSprite(SeasonType season)
    {
        return season switch
        {
            SeasonType.Spring => springSprite,
            SeasonType.Summer => summerSprite,
            SeasonType.Autumn => autumnSprite,
            SeasonType.Winter => winterSprite,
            _ => null
        };
    }

    // 게이지가 한 칸 채워질 때
    private void PlayFillAnimation(int index)
    {
        if (!IsValidSlot(index))
            return;

        _slotTweens[index]?.Kill();

        Transform target = _slots[index].transform;

        target.localScale =
            Vector3.one * fillStartScale;

        _slotTweens[index] = target
            .DOScale(Vector3.one, fillDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (target != null)
                    target.localScale = Vector3.one;
            });
    }

    // 디버프 발동 및 해제
    private void HandleDebuffTriggered(
        DebuffType debuff)
    {
        _panelFadeTween?.Kill();

        if (debuff == DebuffType.None)
        {
            _isDebuffActive = false;
            _activeDebuff = DebuffType.None;

            StopSlotAnimations();
            ResetSlotTransforms();

            RefreshSlots(false);

            if (debuffPanel != null)
            {
                _panelFadeTween = debuffPanel
                    .DOFade(0f, panelFadeDuration);
            }

            return;
        }

        _isDebuffActive = true;
        _activeDebuff = debuff;

        if (debuffLabel != null)
            debuffLabel.text =
                DebuffLabelText(debuff);

        if (debuffTimerFill != null)
            debuffTimerFill.fillAmount = 1f;

        if (debuffPanel != null)
        {
            _panelFadeTween = debuffPanel
                .DOFade(1f, panelFadeDuration);
        }

        // 5칸을 모두 계절 아이콘으로 변경
        ActivateAllSlots(debuff);
    }

    private void ActivateAllSlots(
        DebuffType debuff)
    {
        if (_slots == null)
            return;

        StopSlotAnimations();

        SeasonType season =
            SeasonForDebuff(debuff);

        Sprite activeSprite =
            GetSeasonSprite(season);

        for (int i = 0; i < DISPLAY_SLOTS; i++)
        {
            Image img = _slots[i];

            if (img == null)
                continue;

            img.sprite = activeSprite;
            img.color = activeSprite != null
                ? Color.white
                : Color.clear;

            Transform target = img.transform;
            target.localScale = Vector3.one;

            int slotIndex = i;

            // 5칸 순차 확대
            _slotTweens[i] = target
                .DOScale(
                    Vector3.one * activeScale,
                    activeDuration
                )
                .SetDelay(i * 0.06f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (this != null &&
                        _isDebuffActive &&
                        _activeDebuff == debuff)
                    {
                        StartBlink(slotIndex);
                    }
                });
        }
    }

    // 활성화 중 반짝임 반복
    private void StartBlink(int index)
    {
        if (!IsValidSlot(index))
            return;

        Image img = _slots[index];

        if (img.sprite == null)
            return;

        _blinkSequences[index]?.Kill();

        float minAlpha =
            Mathf.Clamp01(blinkMinAlpha);

        float minScale =
            Mathf.Max(
                0.01f,
                activeScale - blinkScale
            );

        img.color = Color.white;

        Sequence sequence = DOTween.Sequence();

        // 밝아졌다 어두워지는 효과
        sequence.Join(
            img.DOFade(minAlpha, blinkDuration)
                .SetEase(Ease.InOutSine)
        );

        // 미세하게 커졌다 작아지는 효과
        sequence.Join(
            img.transform
                .DOScale(
                    Vector3.one * minScale,
                    blinkDuration
                )
                .SetEase(Ease.InOutSine)
        );

        sequence.SetLoops(
            -1,
            LoopType.Yoyo
        );

        sequence.SetDelay(index * blinkDelay);

        _blinkSequences[index] = sequence;
    }

    private SeasonType SeasonForDebuff(
        DebuffType debuff)
    {
        return debuff switch
        {
            DebuffType.Bound => SeasonType.Spring,
            DebuffType.Slow => SeasonType.Summer,
            DebuffType.Reverse => SeasonType.Autumn,
            DebuffType.Frozen => SeasonType.Winter,
            _ => SeasonType.Spring
        };
    }

    private string DebuffLabelText(
        DebuffType debuff)
    {
        return debuff switch
        {
            DebuffType.Bound => "속박",
            DebuffType.Slow => "이속 저하",
            DebuffType.Reverse => "방향 반전",
            DebuffType.Frozen => "빙결",
            _ => ""
        };
    }

    private bool IsValidSlot(int index)
    {
        return _slots != null &&
               index >= 0 &&
               index < _slots.Length &&
               _slots[index] != null;
    }

    private void StopSlotAnimations()
    {
        if (_slotTweens != null)
        {
            for (int i = 0;
                 i < _slotTweens.Length; i++)
            {
                _slotTweens[i]?.Kill();
                _slotTweens[i] = null;
            }
        }

        if (_blinkSequences != null)
        {
            for (int i = 0;
                 i < _blinkSequences.Length; i++)
            {
                _blinkSequences[i]?.Kill();
                _blinkSequences[i] = null;
            }
        }
    }

    private void ResetSlotTransforms()
    {
        if (_slots == null)
            return;

        foreach (Image img in _slots)
        {
            if (img == null)
                continue;

            img.transform.localScale = Vector3.one;

            if (img.sprite != null)
                img.color = Color.white;
        }
    }

    private void StopAllAnimations()
    {
        StopSlotAnimations();
        _panelFadeTween?.Kill();

        ResetSlotTransforms();
    }
}
