
using UnityEngine;
using DG.Tweening;

public class SeasonAuraController : MonoBehaviour
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    [Header("계절별 PNG")]
    public SpriteRenderer auraRenderer;
    public Sprite[] seasonSprites = new Sprite[4];

    [Header("일반 충전 - 중앙 파동")]
    public float chargeDuration = 0.8f;
    public float chargeStartScale = 0.35f;
    public float chargeEndScale = 1.8f;

    [Range(0f, 1f)]
    public float chargeAlpha = 1f;

    [Header("최종 디버프 - 등장")]
    public float debuffIntro = 0.5f;
    public float debuffIntroScale = 1.2f;

    [Header("최종 디버프 - 회전 유지")]
    public float orbitRadius = 0.35f;

    // 한 바퀴 공전하는 시간
    public float orbitDuration = 2.5f;

    // 공전 한 바퀴 동안 PNG 자체 회전 횟수
    public float spinTurns = 2f;

    [Header("최종 디버프 - 투명도 반복")]
    [Range(0f, 1f)]
    public float debuffMinAlpha = 0f;

    [Range(0f, 1f)]
    public float debuffMaxAlpha = 1f;

    // 사라졌다 다시 나타나는 전체 주기
    public float alphaCycleDuration = 1.2f;

    [Header("최종 디버프 - 종료")]
    public float debuffOutro = 0.5f;
    public float debuffEndScale = 2f;

    [Header("계절별 파티클 - 선택")]
    public ParticleSystem[] debuffParticles =
        new ParticleSystem[4];

    private Sequence sequence;

    private Tween orbitTween;
    private Tween spinTween;
    private Tween alphaTween;

    private Vector3 basePosition;
    private Vector3 baseScale;
    private Vector3 baseRotation;

    private float currentAngle;

    private ParticleSystem playingParticles;

    private bool isDebuffPlaying;

    private void Awake()
    {
        if (auraRenderer == null)
        {
            Debug.LogWarning(
                "[SeasonAuraController] Aura Renderer가 연결되지 않았습니다."
            );
            return;
        }

        Transform ring = auraRenderer.transform;

        basePosition = ring.localPosition;
        baseScale = ring.localScale;
        baseRotation = ring.localEulerAngles;

        auraRenderer.enabled = false;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        SeasonGaugeManager.OnDebuffTriggered += HandleDebuff;
    }

    private void OnDisable()
    {
        SeasonGaugeManager.OnDebuffTriggered -= HandleDebuff;

        StopAllTweens();
        isDebuffPlaying = false;
        Finish();
    }

    private void OnDestroy()
    {
        StopAllTweens();
    }

    // ========================================
    // 일반 게이지 충전
    // ========================================

    public void PlayCharge(Season season)
    {
        // 최종 디버프 연출 중에는 일반 충전 파동이
        // 기존 아우라를 중단시키지 않도록 한다.
        if (isDebuffPlaying)
            return;

        if (!Prepare(season))
            return;

        Transform ring = auraRenderer.transform;

        ring.localScale =
            baseScale * chargeStartScale;

        float totalDuration =
            Mathf.Max(0.02f, chargeDuration);

        float appear = totalDuration * 0.3f;
        float spread = totalDuration - appear;

        sequence = DOTween.Sequence();

        // 1. 중앙에서 나타남
        sequence.Append(
            auraRenderer.DOFade(
                chargeAlpha,
                appear
            )
        );

        sequence.Join(
            ring.DOScale(
                baseScale * 0.85f,
                appear
            ).SetEase(Ease.OutCubic)
        );

        // 2. 커지면서 사라짐
        sequence.Append(
            ring.DOScale(
                baseScale * chargeEndScale,
                spread
            ).SetEase(Ease.OutCubic)
        );

        sequence.Join(
            auraRenderer.DOFade(
                0f,
                spread
            ).SetEase(Ease.InQuad)
        );

        sequence.OnComplete(() =>
        {
            sequence = null;
            Finish();
        });
    }

    // ========================================
    // 최종 디버프 시작
    // ========================================

    public void PlayDebuff(Season season)
    {
        if (!Prepare(season))
            return;

        isDebuffPlaying = true;

        Transform ring = auraRenderer.transform;

        // 처음에는 작은 크기로 시작
        ring.localScale = baseScale * 0.25f;

        ring.localPosition = basePosition;

        SetAlpha(0f);

        sequence = DOTween.Sequence();

        // 1. 등장
        sequence.Append(
            ring.DOScale(
                baseScale * debuffIntroScale,
                Mathf.Max(0.01f, debuffIntro)
            ).SetEase(Ease.OutBack)
        );

        sequence.Join(
            auraRenderer.DOFade(
                debuffMaxAlpha,
                Mathf.Max(0.01f, debuffIntro)
            )
        );

        // 2. 등장 완료 후 회전 시작
        sequence.AppendCallback(() =>
        {
            if (!isDebuffPlaying)
                return;

            PlayParticles(season);
            StartDebuffLoop();
        });

        // 등장 시퀀스만 종료
        // 공전과 투명도 반복은 별도 Tween으로 유지
    }

    // ========================================
    // 최종 디버프 무한 회전
    // ========================================


    private void StartDebuffLoop()
    {
        if (auraRenderer == null)
            return;

        Transform ring = auraRenderer.transform;

        orbitTween?.Kill();
        spinTween?.Kill();
        alphaTween?.Kill();

        float duration = Mathf.Max(0.01f, orbitDuration);

        // 위치는 절대 움직이지 않음
        ring.localPosition = basePosition;

        // 크기 고정
        ring.localScale = baseScale * debuffIntroScale;

        // PNG 자체만 제자리 회전
        spinTween = ring.DOLocalRotate(
            baseRotation + new Vector3(0f, 0f, 360f),
            duration,
            RotateMode.FastBeyond360
        )
        .SetEase(Ease.Linear)
        .SetLoops(-1, LoopType.Incremental);

        // 서서히 사라졌다 다시 나타나는 효과
        SetAlpha(debuffMaxAlpha);

        float halfDuration =
            Mathf.Max(0.01f, alphaCycleDuration * 0.5f);

        alphaTween = auraRenderer.DOFade(
            debuffMinAlpha,
            halfDuration
        )
        .SetEase(Ease.InOutSine)
        .SetLoops(-1, LoopType.Yoyo);
    }


    // ========================================
    // 플레이어 중심 공전 위치 계산
    // ========================================

    private void UpdateOrbit()
    {
        if (auraRenderer == null)
            return;

        float radians =
            currentAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            Mathf.Sin(radians) * orbitRadius,
            0f
        );

        auraRenderer.transform.localPosition =
            basePosition + offset;
    }

    // ========================================
    // 디버프 종료 이벤트 처리
    // ========================================

    private void HandleDebuff(DebuffType debuff)
    {
        if (debuff == DebuffType.None)
        {
            StopDebuff();
        }
    }

    public void StopDebuff()
    {
        if (!isDebuffPlaying)
            return;

        isDebuffPlaying = false;

        StopAllTweens();

        if (auraRenderer == null)
            return;

        Transform ring = auraRenderer.transform;

        float outro =
            Mathf.Max(0.01f, debuffOutro);

        sequence = DOTween.Sequence();

        // 최종 종료 시 한 번만 커지며 사라짐
        sequence.Append(
            ring.DOScale(
                baseScale * debuffEndScale,
                outro
            ).SetEase(Ease.OutCubic)
        );

        sequence.Join(
            auraRenderer.DOFade(
                0f,
                outro
            )
        );

        sequence.OnComplete(() =>
        {
            sequence = null;
            Finish();
        });
    }

    // ========================================
    // Tween 정리
    // ========================================

    private void StopAllTweens()
    {
        sequence?.Kill();
        orbitTween?.Kill();
        spinTween?.Kill();
        alphaTween?.Kill();

        sequence = null;
        orbitTween = null;
        spinTween = null;
        alphaTween = null;
    }

    // ========================================
    // 공통 초기화
    // ========================================

    private bool Prepare(Season season)
    {
        if (auraRenderer == null)
            return false;

        int index = (int)season;

        if (seasonSprites == null ||
            index < 0 ||
            index >= seasonSprites.Length ||
            seasonSprites[index] == null)
        {
            Debug.LogWarning(
                "[SeasonAuraController] 계절 Sprite가 없습니다: "
                + season
            );
            return false;
        }

        StopAllTweens();
        StopParticles();

        isDebuffPlaying = false;

        Transform ring = auraRenderer.transform;

        ring.localPosition = basePosition;
        ring.localScale = baseScale;
        ring.localEulerAngles = baseRotation;

        auraRenderer.sprite = seasonSprites[index];
        auraRenderer.enabled = true;
        auraRenderer.color = Color.white;

        SetAlpha(0f);

        return true;
    }

    private void SetAlpha(float alpha)
    {
        if (auraRenderer == null)
            return;

        Color color = auraRenderer.color;
        color.a = alpha;
        auraRenderer.color = color;
    }

    // ========================================
    // 파티클
    // ========================================

    private void PlayParticles(Season season)
    {
        int index = (int)season;

        if (debuffParticles == null ||
            index < 0 ||
            index >= debuffParticles.Length)
            return;

        playingParticles = debuffParticles[index];

        if (playingParticles == null)
            return;

        playingParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        playingParticles.Play();
    }

    private void StopParticles()
    {
        if (playingParticles == null)
            return;

        playingParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        playingParticles = null;
    }

    // ========================================
    // 연출 종료 후 원상복구
    // ========================================

    private void Finish()
    {
        StopParticles();

        if (auraRenderer == null)
            return;

        Transform ring = auraRenderer.transform;

        ring.localPosition = basePosition;
        ring.localScale = baseScale;
        ring.localEulerAngles = baseRotation;

        SetAlpha(0f);
        auraRenderer.enabled = false;
    }

    // ========================================
    // 일반 충전 테스트
    // ========================================

    [ContextMenu("충전 / 봄")]
    public void TestSpringCharge()
    {
        PlayCharge(Season.Spring);
    }

    [ContextMenu("충전 / 여름")]
    public void TestSummerCharge()
    {
        PlayCharge(Season.Summer);
    }

    [ContextMenu("충전 / 가을")]
    public void TestAutumnCharge()
    {
        PlayCharge(Season.Autumn);
    }

    [ContextMenu("충전 / 겨울")]
    public void TestWinterCharge()
    {
        PlayCharge(Season.Winter);
    }

    // ========================================
    // 최종 디버프 테스트
    // ========================================

    [ContextMenu("디버프 발동 / 봄")]
    public void TestSpringDebuff()
    {
        PlayDebuff(Season.Spring);
    }

    [ContextMenu("디버프 발동 / 여름")]
    public void TestSummerDebuff()
    {
        PlayDebuff(Season.Summer);
    }

    [ContextMenu("디버프 발동 / 가을")]
    public void TestAutumnDebuff()
    {
        PlayDebuff(Season.Autumn);
    }

    [ContextMenu("디버프 발동 / 겨울")]
    public void TestWinterDebuff()
    {
        PlayDebuff(Season.Winter);
    }

    [ContextMenu("디버프 종료 테스트")]
    public void TestStopDebuff()
    {
        StopDebuff();
    }
}
