
using UnityEngine;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PoofEffect : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private SpriteRenderer main;
    [SerializeField] private SpriteRenderer[] subPuffs;

    [Header("Animation")]
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 1.48f;
    [SerializeField] private float spreadDistance = 0.18f;

    [Header("Sequential Poof")]
    [SerializeField] private float subDelay = 0.14f;
    [SerializeField] private float subDelayVariation = 0.6f;

    [Header("Smoke Motion")]
    [SerializeField] private float upwardDrift = 1.26f;
    [SerializeField] private float randomMovement = 1.28f;
    [SerializeField] private float randomRotation = 37.6f;

    // 기존 Inspector 값 보존
    [SerializeField] private float scaleVariation = 1.4f;

    [Header("Alpha")]
    [SerializeField, Range(0f, 1f)]
    private float maxAlpha = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float fadeStart = 0.5f;

    [Header("Test")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool testKey = false;

    private SpriteRenderer[] sprites;
    private Vector3[] basePositions;
    private Vector3[] baseScales;
    private Quaternion[] baseRotations;
    private Color[] baseColors;

    private Sequence sequence;

    private void Awake()
    {
        InitializeSprites();
        HideAll();
    }

    private void InitializeSprites()
    {
        int subCount = subPuffs == null ? 0 : subPuffs.Length;
        int count = subCount + 1;

        sprites = new SpriteRenderer[count];
        basePositions = new Vector3[count];
        baseScales = new Vector3[count];
        baseRotations = new Quaternion[count];
        baseColors = new Color[count];

        sprites[0] = main;

        for (int i = 0; i < subCount; i++)
            sprites[i + 1] = subPuffs[i];

        for (int i = 0; i < count; i++)
        {
            if (sprites[i] == null) continue;

            Transform t = sprites[i].transform;

            basePositions[i] = t.localPosition;
            baseScales[i] = t.localScale;
            baseRotations[i] = t.localRotation;
            baseColors[i] = sprites[i].color;
        }
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (!testKey) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.tKey.wasPressedThisFrame)
        {
            Play();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.T))
        {
            Play();
        }
#endif
    }

    [ContextMenu("Test Poof")]
    public void Play()
    {
        if (!Application.isPlaying) return;

        sequence?.Kill();
        sequence = null;

        // 배열이 아직 초기화되지 않았거나 크기가 다른 경우 재설정
        int expectedCount =
            (subPuffs == null ? 0 : subPuffs.Length) + 1;

        if (sprites == null ||
            basePositions == null ||
            baseScales == null ||
            baseRotations == null ||
            baseColors == null ||
            sprites.Length != expectedCount ||
            basePositions.Length != expectedCount ||
            baseScales.Length != expectedCount ||
            baseRotations.Length != expectedCount ||
            baseColors.Length != expectedCount)
        {
            InitializeSprites();
        }

        HideAll();

        if (sprites == null || sprites.Length == 0)
            return;

        sequence = DOTween.Sequence();

        // 메인 펑
        AnimatePuff(0, 0f);

        // 서브 펑 순차 재생
        float previousDelay = 0f;

        for (int i = 1; i < sprites.Length; i++)
        {
            float variation = Random.Range(
                -Mathf.Min(subDelayVariation, subDelay * 0.4f),
                 Mathf.Min(subDelayVariation, subDelay * 0.4f)
            );

            float interval = Mathf.Max(
                0.02f,
                subDelay + variation
            );

            previousDelay += interval;

            AnimatePuff(i, previousDelay);
        }

        sequence.OnComplete(() =>
        {
            HideAll();
            sequence = null;
        });
    }

    private void AnimatePuff(int index, float delay)
    {
        // 배열 범위 오류 방지
        if (sprites == null ||
            basePositions == null ||
            baseScales == null ||
            baseRotations == null ||
            baseColors == null)
            return;

        if (index < 0 ||
            index >= sprites.Length ||
            index >= basePositions.Length ||
            index >= baseScales.Length ||
            index >= baseRotations.Length ||
            index >= baseColors.Length)
        {
            Debug.LogWarning(
                "[PoofEffect] 배열 인덱스 오류 방지: " + index
            );
            return;
        }

        SpriteRenderer sprite = sprites[index];
        if (sprite == null) return;

        Transform t = sprite.transform;

        Vector3 origin = basePositions[index];
        Vector3 originalScale = baseScales[index];

        Vector3 startSize = originalScale * startScale;
        Vector3 finalSize = originalScale * endScale;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        Vector3 direction = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f
        );

        Vector3 destination =
            origin +
            direction * spreadDistance +
            new Vector3(
                Random.Range(-randomMovement, randomMovement),
                upwardDrift + Random.Range(0f, randomMovement),
                0f
            );

        float rotationZ = Random.Range(
            -randomRotation,
            randomRotation
        );

        float puffDuration = Mathf.Max(0.01f, duration);

        t.localPosition = origin;
        t.localScale = startSize;
        t.localRotation = baseRotations[index];

        Color color = baseColors[index];
        color.a = 0f;
        sprite.color = color;
        sprite.enabled = false;

        sequence.InsertCallback(delay, () =>
        {
            if (sprite == null) return;

            sprite.enabled = true;

            Color c = baseColors[index];
            c.a = maxAlpha;
            sprite.color = c;
        });

        float burstTime = puffDuration * 0.3f;

        sequence.Insert(
            delay,
            t.DOScale(
                Vector3.Lerp(startSize, finalSize, 0.85f),
                burstTime
            ).SetEase(Ease.OutBack)
        );

        sequence.Insert(
            delay + burstTime,
            t.DOScale(
                finalSize,
                puffDuration - burstTime
            ).SetEase(Ease.OutSine)
        );

        sequence.Insert(
            delay,
            t.DOLocalMove(
                destination,
                puffDuration
            ).SetEase(Ease.OutSine)
        );

        sequence.Insert(
            delay,
            t.DOLocalRotate(
                baseRotations[index].eulerAngles +
                new Vector3(0f, 0f, rotationZ),
                puffDuration
            ).SetEase(Ease.OutSine)
        );

        float fadeDelay = delay + puffDuration * fadeStart;

        float fadeDuration = Mathf.Max(
            0.01f,
            puffDuration * (1f - fadeStart)
        );

        sequence.Insert(
            fadeDelay,
            sprite.DOFade(
                0f,
                fadeDuration
            ).SetEase(Ease.InSine)
        );
    }

    private void HideAll()
    {
        if (sprites == null) return;

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null) continue;

            sprite.enabled = false;

            Transform t = sprite.transform;

            t.localPosition = basePositions[i];
            t.localScale = baseScales[i];
            t.localRotation = baseRotations[i];

            sprite.color = baseColors[i];
        }
    }

    private void OnDisable()
    {
        sequence?.Kill();
        sequence = null;
        HideAll();
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}
