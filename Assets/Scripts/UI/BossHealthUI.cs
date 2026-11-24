using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 화면 상단 보스 체력바다. PlayerHealthUI(하트 개수 방식)와 달리 보스는 fillAmount 기반
// 게이지 바로 표시한다. Boss가 데미지를 받을 때마다 발행하는 OnDamageTaken을 구독해
// 그 시점의 HealthRatio로 fillAmount를 부드럽게 채워 넣는다.
public class BossHealthUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("Boss")]
    private Boss boss;
    // Inspector에서 씬의 Boss 컴포넌트를 연결한다.

    [SerializeField, LabelText("체력 게이지 Image (Fill 타입)")]
    private Image fillImage;
    // Image의 Image Type을 Filled로 설정하고 Fill Method는 Horizontal로 맞춘다.

    [Title("DOTween 연출 설정")]
    [SerializeField, LabelText("변화 시간")]
    private float tweenDuration = 0.3f;
    // 체력바가 목표 값까지 부드럽게 줄어드는 데 걸리는 시간이다.

    [SerializeField, LabelText("Ease 타입")]
    private Ease easeType = Ease.OutQuad;

    [Title("페이즈 구간 마커")]
    [SerializeField, LabelText("마커 두께(px)")]
    private float markerWidth = 4f;
    // Boss.PatternThresholds(75/50/25/10%)를 체력바 위에 사선으로 표시해, 다음 계절 패턴이
    // 언제 발동하는지 한눈에 보이게 한다.

    [SerializeField, LabelText("마커 색상")]
    private Color markerColor = Color.white;

    [SerializeField, LabelText("마커 기울기(도)")]
    private float markerTiltDegrees = 15f;

    [Title("무적 표시")]
    [SerializeField, LabelText("무적일 때 게이지 색")]
    private Color invincibleColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    // 계절 패턴 중(Boss.IsInvincible) 게이지를 이 색으로 바꿔 "지금은 때려도 안 닳는다"는 걸 보여 준다.

    [SerializeField, LabelText("색 전환 시간")]
    private float colorTweenDuration = 0.3f;

    private Tween _fillTween;
    private Tween _colorTween;
    // 게이지 색 전환 Tween. 무적이 빠르게 켜졌다 꺼져도 겹치지 않게 저장해 두고 끊는다.
    private Color _normalColor;
    // 무적이 풀리면 되돌아갈 원래 게이지 색이다. Awake에서 Image의 현재 색을 기억해 둔다.
    private bool _isInvincibleShown;
    // 지금 게이지가 무적 색으로 표시돼 있는지 여부다.

    private void Awake()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
            _normalColor = fillImage.color;
        }

        BuildThresholdMarkers();
    }

    // Boss.PatternThresholds를 순회하며 체력바 위에 얇은 사선 마커를 하나씩 만든다.
    // PlayerHealthUI.BuildHearts()와 동일하게 별도 프리팹 없이 코드로 생성한다.
    private void BuildThresholdMarkers()
    {
        if (fillImage == null || boss == null) return;

        RectTransform barRoot = fillImage.rectTransform.parent as RectTransform;
        if (barRoot == null) return;

        foreach (float threshold in boss.PatternThresholds)
        {
            GameObject markerGo = new GameObject("PhaseMarker_" + threshold.ToString("P0"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerGo.transform.SetParent(barRoot, false);

            RectTransform rect = markerGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(threshold, 0f);
            rect.anchorMax = new Vector2(threshold, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(markerWidth, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, markerTiltDegrees);

            Image img = markerGo.GetComponent<Image>();
            img.color = markerColor;
            img.raycastTarget = false;
        }
    }

    private void OnEnable()
    {
        if (boss == null) return;

        boss.OnDamageTaken += HandleDamageTaken;
        boss.OnDied += HandleBossDied;
    }

    private void OnDisable()
    {
        if (boss == null) return;

        boss.OnDamageTaken -= HandleDamageTaken;
        boss.OnDied -= HandleBossDied;
    }

    private void OnDestroy()
    {
        _fillTween?.Kill();
        _colorTween?.Kill();
    }

    private void Update()
    {
        if (boss == null || fillImage == null) return;

        // 매 프레임 bool 하나만 비교하고, 무적 상태가 바뀐 순간에만 색 Tween을 만든다.
        // Boss는 무적을 켜고 끄는 경로가 여러 곳이라 이벤트 대신 상태를 직접 본다.
        bool invincible = boss.IsInvincible;
        if (invincible == _isInvincibleShown) return;

        _isInvincibleShown = invincible;
        _colorTween?.Kill();
        _colorTween = fillImage.DOColor(invincible ? invincibleColor : _normalColor, colorTweenDuration);
    }

    private void HandleDamageTaken(float amount) => SetFill(boss.HealthRatio);

    private void HandleBossDied() => SetFill(0f);

    private void SetFill(float ratio)
    {
        if (fillImage == null) return;

        _fillTween?.Kill();
        _fillTween = fillImage.DOFillAmount(Mathf.Clamp01(ratio), tweenDuration).SetEase(easeType);
    }

    [Button("50% 데미지 테스트")]
    private void TestHalfDamage() => SetFill(0.5f);
}
