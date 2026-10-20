
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ObjectActivationPulse : MonoBehaviour
{
    [Header("Auto Generate")]
    [SerializeField] private Transform targetRoot;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Material pulseMaterial;
    [SerializeField] private bool autoCreateLight = true;
    [SerializeField] private Vector3 lightOffset = new Vector3(0, 0, 0.5f);

    [Header("Colors")]
    [SerializeField]
    private Color glowColor =
        new Color(0.35f, 1f, 0.85f, 1f);

    [SerializeField]
    private Color pulseColor =
        new Color(0.35f, 1f, 0.85f, 1f);

    [Header("Glow Settings")]
    [SerializeField] private float maxGlowStrength = 6f;
    [SerializeField] private float glowFadeIn = 1.2f;
    [SerializeField] private float glowHold = 0.4f;
    [SerializeField] private float glowFadeOut = 1.1f;

    [Header("Pulse Settings")]
    [SerializeField] private float maxPulseStrength = 4f;
    [SerializeField] private float pulseDuration = 2f;
    [SerializeField] private float pulseStartScale = 0.28f;
    [SerializeField] private float pulseEndScale = 1.18f;

    [Header("Light Settings")]
    [SerializeField] private float maxLightIntensity = 1f;
    [SerializeField] private float lightFadeIn = 0.4f;
    [SerializeField] private float lightFadeOut = 0.8f;
    [SerializeField] private float lightRange = 5f;

    [Header("Test")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool testKey = true;
    [SerializeField] private KeyCode triggerKey = KeyCode.T;

    private readonly List<MeshRenderer> glowRenderers =
        new List<MeshRenderer>();

    private readonly List<MeshRenderer> pulseRenderers =
        new List<MeshRenderer>();

    private readonly List<Transform> pulseTransforms =
        new List<Transform>();

    private MaterialPropertyBlock propertyBlock;
    private Light activationLight;
    private Sequence currentSequence;

    private static readonly int GlowColorID =
        Shader.PropertyToID("_GlowColor");

    private static readonly int GlowStrengthID =
        Shader.PropertyToID("_GlowStrength");

    private static readonly int PulseColorID =
        Shader.PropertyToID("_PulseColor");

    private static readonly int PulseStrengthID =
        Shader.PropertyToID("_PulseStrength");

    private static readonly int AlphaID =
        Shader.PropertyToID("_Alpha");

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (targetRoot == null)
            targetRoot = transform;

        GenerateEffects();
        ResetVisuals();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (!testKey)
            return;

        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            triggerKey == KeyCode.T)
        {
            pressed = Keyboard.current.tKey.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetKeyDown(triggerKey);
#endif

        if (pressed)
            Play();
    }

    private void GenerateEffects()
    {
        if (glowMaterial == null || pulseMaterial == null)
        {
            Debug.LogError(
                "Glow Material 또는 Pulse Material이 비어 있습니다.",
                this
            );
            return;
        }

        MeshRenderer[] sources =
            targetRoot.GetComponentsInChildren<MeshRenderer>(true);

        Bounds combinedBounds = new Bounds();
        bool hasBounds = false;

        foreach (MeshRenderer source in sources)
        {
            if (source.name.StartsWith("Generated_"))
                continue;

            MeshFilter sourceFilter =
                source.GetComponent<MeshFilter>();

            if (sourceFilter == null ||
                sourceFilter.sharedMesh == null)
                continue;

            // 원본 메시의 Bounds 수집
            if (!hasBounds)
            {
                combinedBounds = source.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(source.bounds);
            }

            // Glow 자동 생성
            MeshRenderer glow = CreateEffectMesh(
                source,
                sourceFilter.sharedMesh,
                glowMaterial,
                "Generated_Glow"
            );

            // Pulse 자동 생성
            MeshRenderer pulse = CreateEffectMesh(
                source,
                sourceFilter.sharedMesh,
                pulseMaterial,
                "Generated_Pulse"
            );

            glowRenderers.Add(glow);
            pulseRenderers.Add(pulse);
            pulseTransforms.Add(pulse.transform);
        }

        // 주변 조명 자동 생성
        if (autoCreateLight)
        {
            GameObject lightObj =
                new GameObject("Generated_Light");

            lightObj.transform.SetParent(
                targetRoot,
                false
            );

            Vector3 center = hasBounds
                ? targetRoot.InverseTransformPoint(
                    combinedBounds.center)
                : Vector3.zero;

            lightObj.transform.localPosition =
                center + lightOffset;

            activationLight =
                lightObj.AddComponent<Light>();

            activationLight.type = LightType.Point;
            activationLight.range = lightRange;
            activationLight.intensity = 0f;
            activationLight.color = glowColor;
            activationLight.shadows = LightShadows.None;
        }

        Debug.Log(
            $"[Activation VFX] 자동 생성 완료 - " +
            $"Glow {glowRenderers.Count}개, " +
            $"Pulse {pulseRenderers.Count}개",
            this
        );
    }

    private MeshRenderer CreateEffectMesh(
        MeshRenderer source,
        Mesh mesh,
        Material material,
        string objectName)
    {
        GameObject obj = new GameObject(objectName);

        // 원본의 자식으로 생성해 움직임을 따라감
        obj.transform.SetParent(source.transform, false);

        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        MeshFilter filter = obj.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer =
            obj.AddComponent<MeshRenderer>();

        // 원본의 모든 SubMesh에 효과 머티리얼 적용
        int count = Mathf.Max(1, mesh.subMeshCount);
        Material[] materials = new Material[count];

        for (int i = 0; i < count; i++)
            materials[i] = material;

        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        renderer.enabled = false;

        return renderer;
    }

    [ContextMenu("Play Activation Pulse")]
    public void Play()
    {
        if (!Application.isPlaying)
            return;

        currentSequence?.Kill();
        currentSequence = null;

        if (glowRenderers.Count == 0 ||
            pulseRenderers.Count == 0)
        {
            Debug.LogWarning(
                "활성화 이펙트 메시가 생성되지 않았습니다.",
                this
            );
            return;
        }

        ResetVisuals();

        SetRenderers(glowRenderers, true);
        SetRenderers(pulseRenderers, true);

        float glowIn = Mathf.Max(0.01f, glowFadeIn);
        float glowOut = Mathf.Max(0.01f, glowFadeOut);
        float pulseTime = Mathf.Max(0.01f, pulseDuration);
        float lightIn = Mathf.Max(0.01f, lightFadeIn);
        float lightOut = Mathf.Max(0.01f, lightFadeOut);

        Sequence seq = DOTween.Sequence();

        // 1. Glow : 테두리 발광
        seq.Insert(0f,
            DOVirtual.Float(
                0f, maxGlowStrength, glowIn,
                value =>
                {
                    float alpha = Mathf.Clamp01(
                        value / Mathf.Max(
                            0.001f, maxGlowStrength));

                    SetGlow(value, alpha);
                }
            ).SetEase(Ease.OutSine)
        );

        // 2. Glow : 밝기 유지 후 감소
        seq.Insert(glowIn + glowHold,
            DOVirtual.Float(
                maxGlowStrength, 0f, glowOut,
                value =>
                {
                    float alpha = Mathf.Clamp01(
                        value / Mathf.Max(
                            0.001f, maxGlowStrength));

                    SetGlow(value, alpha);
                }
            ).SetEase(Ease.InSine)
        );

        // 3. Pulse : 동시에 바깥으로 확산
        seq.Insert(0f,
            DOVirtual.Float(
                pulseStartScale,
                pulseEndScale,
                pulseTime,
                SetPulseScale
            ).SetEase(Ease.OutSine)
        );

        // 4. Pulse : 커지면서 서서히 사라짐
        seq.Insert(0f,
            DOVirtual.Float(
                1f, 0f, pulseTime,
                value =>
                {
                    SetPulse(maxPulseStrength, value);
                }
            ).SetEase(Ease.InQuad)
        );

        // 5. Point Light : 주변 밝아짐
        if (activationLight != null)
        {
            activationLight.color = glowColor;

            seq.Insert(0f,
                DOVirtual.Float(
                    0f, maxLightIntensity, lightIn,
                    value =>
                        activationLight.intensity = value
                )
            );

            seq.Insert(lightIn,
                DOVirtual.Float(
                    maxLightIntensity, 0f, lightOut,
                    value =>
                        activationLight.intensity = value
                )
            );
        }

        seq.OnComplete(() =>
        {
            ResetVisuals();
            currentSequence = null;
        });

        currentSequence = seq;
    }

    private void SetGlow(float strength, float alpha)
    {
        foreach (MeshRenderer r in glowRenderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(
                GlowColorID, glowColor);

            propertyBlock.SetFloat(
                GlowStrengthID, strength);

            propertyBlock.SetFloat(
                AlphaID, alpha);

            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetPulse(float strength, float alpha)
    {
        foreach (MeshRenderer r in pulseRenderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(
                PulseColorID, pulseColor);

            propertyBlock.SetFloat(
                PulseStrengthID, strength);

            propertyBlock.SetFloat(
                AlphaID, alpha);

            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetPulseScale(float scale)
    {
        foreach (Transform t in pulseTransforms)
        {
            if (t != null)
                t.localScale = Vector3.one * scale;
        }
    }

    private void SetRenderers(
        List<MeshRenderer> renderers,
        bool visible)
    {
        foreach (MeshRenderer r in renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private void ResetVisuals()
    {
        SetGlow(0f, 0f);
        SetPulse(0f, 0f);

        SetPulseScale(pulseStartScale);

        SetRenderers(glowRenderers, false);
        SetRenderers(pulseRenderers, false);

        if (activationLight != null)
            activationLight.intensity = 0f;
    }

    private void OnDisable()
    {
        currentSequence?.Kill();
        currentSequence = null;
        ResetVisuals();
    }

    private void OnDestroy()
    {
        currentSequence?.Kill();
    }
}
