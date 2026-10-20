
using UnityEngine;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ActivationWaveEffect : MonoBehaviour
{
    [Header("Wave Renderers")]
    [SerializeField] private Renderer[] waveRenderers;

    [Header("Animation")]
    [SerializeField] private float startScale = 0.25f;
    [SerializeField] private float endScale = 1.8f;
    [SerializeField] private float duration = 0.45f;

    [SerializeField, Range(0f, 1f)]
    private float maxAlpha = 0.75f;

    [Header("Glow")]
    [SerializeField, Range(0f, 10f)]
    private float glowIntensity = 3f;

    [Header("Test Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool enableTestKey = true;
    [SerializeField] private bool loopTest = false;
    [SerializeField] private float loopInterval = 1.5f;

    private static readonly int BaseColorID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int GlowIntensityID =
        Shader.PropertyToID("_GlowIntensity");

    private MaterialPropertyBlock propertyBlock;
    private Sequence sequence;
    private Vector3 originalScale;
    private float currentAlpha;

    private float loopTimer;

    private void Awake()
    {
        originalScale = transform.localScale;

        propertyBlock = new MaterialPropertyBlock();

        if (waveRenderers == null ||
            waveRenderers.Length == 0)
        {
            waveRenderers =
                GetComponentsInChildren<Renderer>(true);
        }

        SetVisual(0f);
        SetRenderers(false);
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (enableTestKey && TestKeyPressed())
        {
            Play();
        }

        if (loopTest)
        {
            loopTimer += Time.deltaTime;

            if (loopTimer >= Mathf.Max(0.1f, loopInterval))
            {
                loopTimer = 0f;
                Play();
            }
        }
    }

    private bool TestKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.tKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.T);
#else
        return false;
#endif
    }

    [ContextMenu("Test Activation Wave")]
    public void TestWave()
    {
        if (Application.isPlaying)
            Play();
    }


    public void Play()
    {
        sequence?.Kill();
        sequence = null;

        loopTimer = 0f;

        transform.localScale = originalScale * startScale;

        SetVisual(0f);
        SetRenderers(true);

        // 총 재생 시간은 Inspector의 duration 사용
        float fadeIn = duration * 0.2f;
        float hold = duration * 0.35f;
        float fadeOut = duration - fadeIn - hold;

        sequence = DOTween.Sequence();

        // 파동은 전체 시간 동안 확산
        sequence.Join(
            transform.DOScale(
                originalScale * endScale,
                duration
            ).SetEase(Ease.OutSine)
        );

        // 빠르게 밝아짐
        sequence.Append(
            DOVirtual.Float(
                0f, maxAlpha, fadeIn, SetVisual
            )
        );

        // 밝은 상태 유지
        sequence.AppendInterval(hold);

        // 서서히 사라짐
        sequence.Append(
            DOVirtual.Float(
                maxAlpha, 0f, fadeOut, SetVisual
            ).SetEase(Ease.InSine)
        );

        sequence.OnComplete(() =>
        {
            SetVisual(0f);
            SetRenderers(false);

            transform.localScale = originalScale;
            sequence = null;
        });
    }


    private void SetVisual(float alpha)
    {
        currentAlpha = alpha;

        foreach (Renderer r in waveRenderers)
        {
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);

            Color color = Color.white;
            color.a = currentAlpha;

            propertyBlock.SetColor(
                BaseColorID,
                color
            );

            propertyBlock.SetFloat(
                GlowIntensityID,
                glowIntensity
            );

            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetRenderers(bool visible)
    {
        foreach (Renderer r in waveRenderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private void OnDisable()
    {
        sequence?.Kill();
        sequence = null;
        SetRenderers(false);
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}
