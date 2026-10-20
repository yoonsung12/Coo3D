
using UnityEngine;
using DG.Tweening;

public class PoofEffect : MonoBehaviour
{

    [Header("Sprites")]
    [SerializeField] private SpriteRenderer main;
    [SerializeField] private SpriteRenderer[] subPuffs;

    [Header("Animation")]
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 1.4f;
    [SerializeField] private float spreadDistance = 0.6f;

    [Header("Test")]
    [SerializeField] private bool playOnStart = false;

    [Header("Sequential Poof")]
    [SerializeField] private float subDelay = 0.17f;
    [SerializeField] private float subDelayVariation = 0.04f;

    [Header("Alpha")]
    [SerializeField, Range(0f, 1f)]
    private float maxAlpha = 1f;

    [SerializeField, Range(0f, 1f)]
    private float fadeStart = 0.65f;

    private Sequence sequence;
    private Vector3[] originalPositions;
    private Vector3[] originalScales;

    private void Start()
    {
        originalPositions = new Vector3[subPuffs.Length + 1];
        originalScales = new Vector3[subPuffs.Length + 1];

        if (main != null)
        {
            originalPositions[0] = main.transform.localPosition;
            originalScales[0] = main.transform.localScale;
        }

        for (int i = 0; i < subPuffs.Length; i++)
        {
            if (subPuffs[i] == null) continue;

            originalPositions[i + 1] =
                subPuffs[i].transform.localPosition;

            originalScales[i + 1] =
                subPuffs[i].transform.localScale;
        }

        HideAll();

        if (playOnStart)
            Play();
    }

    [ContextMenu("Test Poof")]

    [ContextMenu("Test Poof")]
    public void Play()
    {
        if (!Application.isPlaying)
            return;

        sequence?.Kill();

        sequence = DOTween.Sequence();

        // 메인 펑은 즉시 발생
        AnimateSprite(
            main,
            Vector3.zero,
            0f,
            1f
        );

        // 서브 펑은 순서대로 발생
        for (int i = 0; i < subPuffs.Length; i++)
        {
            float delay = subDelay * (i + 1);

            delay += Random.Range(
                -subDelayVariation,
                subDelayVariation
            );

            delay = Mathf.Max(0f, delay);

            float angle =
                (360f / Mathf.Max(1, subPuffs.Length)) * i
                + Random.Range(-25f, 25f);

            Vector3 direction = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad),
                0f
            );

            AnimateSprite(
                subPuffs[i],
                direction * spreadDistance,
                delay,
                1f
            );
        }

        sequence.OnComplete(() =>
        {
            HideAll();
            sequence = null;
        });
    }




    private void AnimateSprite(
        SpriteRenderer sprite,
        Vector3 destination,
        float delay,
        float sizeMultiplier)
    {
        if (sprite == null) return;

        int index = sprite == main
            ? 0
            : System.Array.IndexOf(subPuffs, sprite) + 1;

        if (index < 0 || index >= originalPositions.Length)
            return;

        Transform t = sprite.transform;

        Vector3 basePosition = originalPositions[index];
        Vector3 baseScale = originalScales[index];

        // Inspector의 위치와 크기를 기준으로 초기화
        t.localPosition = basePosition;
        t.localScale =
            baseScale * startScale * sizeMultiplier;

        // 이전 재생의 투명도 초기화
        Color c = sprite.color;
        c.a = 0f;
        sprite.color = c;

        sprite.enabled = false;

        // 각 Sprite가 정해진 시간에 등장
        sequence.InsertCallback(delay, () =>
        {
            if (sprite == null) return;

            sprite.enabled = true;

            Color color = sprite.color;
            color.a = maxAlpha;
            sprite.color = color;
        });

        // 등장 시점부터 크기 확대
        sequence.Insert(delay,
            t.DOScale(
                baseScale * endScale * sizeMultiplier,
                duration
            ).SetEase(Ease.OutBack)
        );

        // 각자의 위치에서 바깥으로 이동
        sequence.Insert(delay,
            t.DOLocalMove(
                basePosition + destination,
                duration
            ).SetEase(Ease.OutQuad)
        );

        // 처음에는 선명하게 유지
        float fadeDuration =
            duration * (1f - fadeStart);

        float fadeDelay =
            delay + duration * fadeStart;

        // 마지막 구간에만 투명해짐
        sequence.Insert(fadeDelay,
            sprite.DOFade(
                0f,
                Mathf.Max(0.01f, fadeDuration)
            ).SetEase(Ease.InQuad)
        );
    }



    private void HideAll()
    {
        if (main != null)
            main.enabled = false;

        foreach (SpriteRenderer sprite in subPuffs)
        {
            if (sprite != null)
                sprite.enabled = false;
        }
    }

    private void OnDisable()
    {
        sequence?.Kill();
        HideAll();
    }
}
