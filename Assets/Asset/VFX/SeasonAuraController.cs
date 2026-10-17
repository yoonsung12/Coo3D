
using UnityEngine;
using DG.Tweening;

public class SeasonAuraController : MonoBehaviour
{
    public enum Season
    {
        Spring, Summer, Autumn, Winter
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
    public float debuffHold = 2.5f;
    public float orbitRadius = 0.35f;
    public float orbitTurns = 2f;
    public float spinTurns = 2f;

    [Header("최종 디버프 - 종료")]
    public float debuffOutro = 0.5f;
    public float debuffEndScale = 2f;

    [Header("계절별 파티클 - 선택")]
    public ParticleSystem[] debuffParticles =
        new ParticleSystem[4];

    private Sequence sequence;
    private Vector3 basePosition;
    private Vector3 baseScale;
    private Vector3 baseRotation;

    private float currentRadius;
    private float currentAngle;
    private ParticleSystem playingParticles;

    private void Awake()
    {
        if (auraRenderer == null) return;

        Transform ring = auraRenderer.transform;

        basePosition = ring.localPosition;
        baseScale = ring.localScale;
        baseRotation = ring.localEulerAngles;

        auraRenderer.enabled = false;
        SetAlpha(0f);
    }

    // 일반 게이지 한 칸 충전
    public void PlayCharge(Season season)
    {
        if (!Prepare(season)) return;

        Transform ring = auraRenderer.transform;

        ring.localScale =
            baseScale * chargeStartScale;

        float appear = chargeDuration * 0.3f;
        float spread = chargeDuration - appear;

        sequence = DOTween.Sequence();

        // 선명하게 등장
        sequence.Append(
            auraRenderer.DOFade(chargeAlpha, appear)
        );

        sequence.Join(
            ring.DOScale(baseScale * 0.85f, appear)
                .SetEase(Ease.OutCubic)
        );

        // 중앙에서 크게 퍼지면서 사라짐
        sequence.Append(
            ring.DOScale(
                baseScale * chargeEndScale, spread)
                .SetEase(Ease.OutCubic)
        );

        sequence.Join(
            auraRenderer.DOFade(0f, spread)
                .SetEase(Ease.InQuad)
        );

        sequence.OnComplete(Finish);
    }

    // 통합 게이지 100% - 최종 디버프
    public void PlayDebuff(Season season)
    {
        if (!Prepare(season)) return;

        Transform ring = auraRenderer.transform;

        ring.localScale = baseScale * 0.25f;

        currentAngle = -90f;
        currentRadius = 0f;

        sequence = DOTween.Sequence();

        // 1. 등장하며 캐릭터 주변으로 이동
        sequence.Append(
            ring.DOScale(
                baseScale * debuffIntroScale,
                debuffIntro
            ).SetEase(Ease.OutBack)
        );

        sequence.Join(
            auraRenderer.DOFade(1f, debuffIntro)
        );

        sequence.Join(
            DOTween.To(
                () => currentRadius,
                SetOrbitRadius,
                orbitRadius,
                debuffIntro
            ).SetEase(Ease.OutCubic)
        );

        // 2. 파티클 터뜨리기
        sequence.AppendCallback(() =>
        {
            PlayParticles(season);
        });

        // 3. 플레이어 주변 공전
        sequence.Append(
            DOTween.To(
                () => currentAngle,
                SetOrbitAngle,
                -90f + 360f * orbitTurns,
                debuffHold
            ).SetEase(Ease.Linear)
        );

        // 링 자체도 회전
        sequence.Join(
            ring.DOLocalRotate(
                baseRotation +
                    new Vector3(0, 0, 360f * spinTurns),
                debuffHold,
                RotateMode.FastBeyond360
            ).SetEase(Ease.Linear)
        );

        // 4. 마지막 큰 파동
        sequence.Append(
            ring.DOScale(
                baseScale * debuffEndScale,
                debuffOutro
            ).SetEase(Ease.OutCubic)
        );

        sequence.Join(
            auraRenderer.DOFade(0f, debuffOutro)
        );

        sequence.Join(
            DOTween.To(
                () => currentRadius,
                SetOrbitRadius,
                0f,
                debuffOutro
            )
        );

        sequence.OnComplete(Finish);
    }

    private void SetOrbitRadius(float value)
    {
        currentRadius = value;
        UpdateOrbit();
    }

    private void SetOrbitAngle(float value)
    {
        currentAngle = value;
        UpdateOrbit();
    }

    private void UpdateOrbit()
    {
        if (auraRenderer == null) return;

        float radians =
            currentAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * currentRadius,
            Mathf.Sin(radians) * currentRadius,
            0f
        );

        auraRenderer.transform.localPosition =
            basePosition + offset;
    }

    private bool Prepare(Season season)
    {
        if (auraRenderer == null) return false;

        int index = (int)season;

        if (seasonSprites == null ||
            index >= seasonSprites.Length ||
            seasonSprites[index] == null)
            return false;

        sequence?.Kill();
        StopParticles();

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
        if (auraRenderer == null) return;

        Color color = auraRenderer.color;
        color.a = alpha;
        auraRenderer.color = color;
    }

    private void PlayParticles(Season season)
    {
        int index = (int)season;

        if (debuffParticles == null ||
            index >= debuffParticles.Length)
            return;

        playingParticles = debuffParticles[index];

        if (playingParticles == null) return;

        playingParticles.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmittingAndClear
        );

        playingParticles.Play();
    }

    private void StopParticles()
    {
        if (playingParticles == null) return;

        playingParticles.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmittingAndClear
        );

        playingParticles = null;
    }

    private void Finish()
    {
        StopParticles();

        if (auraRenderer == null) return;

        Transform ring = auraRenderer.transform;

        ring.localPosition = basePosition;
        ring.localScale = baseScale;
        ring.localEulerAngles = baseRotation;

        SetAlpha(0f);
        auraRenderer.enabled = false;
    }

    private void OnDisable()
    {
        sequence?.Kill();
        Finish();
    }


    // ========== 일반 게이지 충전 테스트 ==========

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

    // ========== 최종 디버프 발동 테스트 ==========

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

}
