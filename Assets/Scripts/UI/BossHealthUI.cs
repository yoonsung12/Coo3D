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

    private Tween _fillTween;

    private void Awake()
    {
        if (fillImage != null)
            fillImage.fillAmount = 1f;
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
