
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

    // 기존 Inspector 값을 보존하기 위해 남겨둠.
    // 이제 크기 랜덤화에는 사용하지 않음.
    [SerializeField] private float scaleVariation = 1.4f;

    [Header("Alpha")]
    [SerializeField, Range(0f, 1f)]
    private float maxAlpha = 0.241f;

    [SerializeField, Range(0f, 1f)]
    private float fadeStart = 0.5f;

    [Header("Test")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool testKey = true;

    private SpriteRenderer[] sprites;
    private Vector3[] basePositions;
    private Vector3[] baseScales;
    private Quaternion[] baseRotations;
    private Color[] baseColors;

    private Sequence sequence;

    private void Awake()
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
            baseColors[i] = t.GetComponent<SpriteRenderer>().color;
        }

        HideAll();
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

        HideAll();

        sequence = DOTween.Sequence();

        // MAIN: Inspector에서 설정한 크기 그대로
        AnimatePuff(0, 0f);

        // SUB: 크기 랜덤화 없이 순차적으로 재생
        float previousDelay = 0f;

        for (int i = 1; i < sprites.Length; i++)
        {
            // 기존 Variation 값은 유지하지만,
            // Sub 순서가 뒤바뀌지 않도록 간격 제한
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
        SpriteRenderer sprite = sprites[index];
        if (sprite == null) return;

        Transform t = sprite.transform;

        Vector3 origin = basePositions[index];

        // 크기는 각 Sprite의 Inspector Scale 기준
        Vector3 originalScale = baseScales[index];

        // Main/Sub 모두 크기 랜덤 적용 안 함
        Vector3 startSize = originalScale * startScale;
        Vector3 finalSize = originalScale * endScale;

        // 랜덤 움직임과 회전은 유지
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

        // 시작 상태 초기화
        t.localPosition = origin;
        t.localScale = startSize;
        t.localRotation = baseRotations[index];

        Color color = baseColors[index];
        color.a = 0f;
        sprite.color = color;
        sprite.enabled = false;

        // 지정된 시간에 등장
        sequence.InsertCallback(delay, () =>
        {
            if (sprite == null) return;

            sprite.enabled = true;

            Color c = baseColors[index];
            c.a = maxAlpha;
            sprite.color = c;
        });

        // 1단계: 빠르게 펑!
        float burstTime = puffDuration * 0.3f;

        sequence.Insert(delay,
            t.DOScale(
                Vector3.Lerp(startSize, finalSize, 0.85f),
                burstTime
            ).SetEase(Ease.OutBack)
        );

        // 2단계: 천천히 부풀어 오름
        sequence.Insert(delay + burstTime,
            t.DOScale(
                finalSize,
                puffDuration - burstTime
            ).SetEase(Ease.OutSine)
        );

        // 불규칙하게 이동
        sequence.Insert(delay,
            t.DOLocalMove(
                destination,
                puffDuration
            ).SetEase(Ease.OutSine)
        );

        // 회전
        sequence.Insert(delay,
            t.DOLocalRotate(
                baseRotations[index].eulerAngles +
                new Vector3(0f, 0f, rotationZ),
                puffDuration
            ).SetEase(Ease.OutSine)
        );

        // 후반부에 서서히 사라짐
        float fadeDelay =
            delay + puffDuration * fadeStart;

        float fadeDuration =
            Mathf.Max(0.01f, puffDuration * (1f - fadeStart));

        sequence.Insert(fadeDelay,
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
