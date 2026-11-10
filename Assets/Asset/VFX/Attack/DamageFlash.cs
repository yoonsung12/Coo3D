using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// Replaces a character's visible materials briefly with a solid red material.
// This makes the ENTIRE character red, including previously textured areas.
// Attach this component to the character's visual root (not the weapon/VFX root).
public class DamageFlash : MonoBehaviour
{
    [Title("Damage Red Color")]
    [SerializeField, LabelText("Solid red material")]
    private Material redMaterial;

    [SerializeField, LabelText("Red duration (seconds)"), Min(0.01f)]
    private float redDuration = 0.14f;

    [SerializeField, LabelText("Include inactive children")]
    private bool includeInactiveChildren = false;

    private struct RendererEntry
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] redMaterials;
    }

    private readonly List<RendererEntry> entries = new List<RendererEntry>();
    private Tween restoreTween;
    private bool applied;

    private void Awake()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        entries.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
        foreach (Renderer r in renderers)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            Material[] originals = r.sharedMaterials;
            if (originals == null || originals.Length == 0) continue;

            Material[] reds = new Material[originals.Length];
            for (int i = 0; i < reds.Length; i++)
                reds[i] = redMaterial != null && originals[i] != null ? redMaterial : originals[i];

            entries.Add(new RendererEntry
            {
                renderer = r,
                originalMaterials = (Material[])originals.Clone(),
                redMaterials = reds
            });
        }
    }

    public void PlayFlash()
    {
        if (redMaterial == null)
        {
            Debug.LogWarning("[DamageFlash] Assign a red material in the Inspector.", this);
            return;
        }

        restoreTween?.Kill();
        for (int i = 0; i < entries.Count; i++)
        {
            RendererEntry e = entries[i];
            if (e.renderer != null) e.renderer.sharedMaterials = e.redMaterials;
        }
        applied = true;

        // Unscaled time: restores correctly during hit stop.
        restoreTween = DOVirtual.DelayedCall(redDuration, RestoreMaterials, true)
            .SetUpdate(true);
    }

    private void RestoreMaterials()
    {
        if (!applied) return;
        for (int i = 0; i < entries.Count; i++)
        {
            RendererEntry e = entries[i];
            if (e.renderer != null) e.renderer.sharedMaterials = e.originalMaterials;
        }
        applied = false;
        restoreTween = null;
    }

    private void OnDisable()
    {
        restoreTween?.Kill();
        RestoreMaterials();
    }

    private void OnDestroy()
    {
        restoreTween?.Kill();
        RestoreMaterials();
    }

    [Button("Test solid red (Play Mode)")]
    private void TestRed()
    {
        if (Application.isPlaying) PlayFlash();
    }
}
