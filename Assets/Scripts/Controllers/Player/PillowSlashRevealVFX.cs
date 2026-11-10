using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

// Shader Graph 없이 위->아래 메시 등장.
// 리빌 단계별 Mesh를 한 번만 미리 만들고, 공격 중에는 Mesh 참조만 바꿉니다.
// 기존 SwordAttack의 Play(slashDuration), Stop() 호출과 호환됩니다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PillowSlashRevealVFX : MonoBehaviour
{
    public enum RevealAxis { X, Y, Z }

    [Header("방향 (Mesh 로컬 축 기준)")]
    [SerializeField] private RevealAxis axis = RevealAxis.Y;
    [SerializeField] private bool reverse = false;

    [Header("모양 / 시간")]
    [SerializeField, Range(4, 24)] private int revealSteps = 12;
    [SerializeField, Min(0.01f)] private float defaultRevealDuration = 0.13f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;
    [SerializeField, Min(0f)] private float fullVisibleHold = 0.22f;
    [SerializeField] private bool logPlayDiagnostics = true;
    [SerializeField] private bool forceFullMeshForTesting = false;

    private MeshFilter filter;
    private MeshRenderer rend;
    private Mesh source;
    private Mesh[] frames;
    private Sequence sequence;
    private float progress;
    private MaterialPropertyBlock props;
    private int colorId;
    private Color baseColor;
    private bool hasColor;

    private struct Vertex
    {
        public Vector3 p;
        public Vector3 n;
        public Vector2 uv;
    }

    private void Awake()
    {
        filter = GetComponent<MeshFilter>();
        rend = GetComponent<MeshRenderer>();
        source = filter.sharedMesh;

        if (source == null || !source.isReadable)
        {
            Debug.LogError("[PillowSlashRevealVFX] Mesh가 없거나 Read/Write Enabled가 꺼져 있습니다.", this);
            enabled = false;
            return;
        }

        Material mat = rend.sharedMaterial;
        string colorName = mat != null && mat.HasProperty("_BaseColor") ? "_BaseColor" :
                           mat != null && mat.HasProperty("_Color") ? "_Color" : null;
        hasColor = colorName != null;
        colorId = hasColor ? Shader.PropertyToID(colorName) : -1;
        baseColor = hasColor ? mat.GetColor(colorId) : Color.white;
        props = new MaterialPropertyBlock();

        if (!hasColor)
            Debug.LogWarning("[PillowSlashRevealVFX] Material에 _BaseColor 또는 _Color가 없어 페이드를 적용할 수 없습니다.", this);
        // 투명도는 셰이더의 Transparent/Fade 설정이 있어야 실제로 보입니다.

        CacheFrames();
        rend.enabled = false;
    }

    private float Coord(Vector3 p)
    {
        float v = axis == RevealAxis.X ? p.x : axis == RevealAxis.Y ? p.y : p.z;
        return reverse ? -v : v;
    }

    private static Vertex Interpolate(Vertex a, Vertex b, float t)
    {
        return new Vertex
        {
            p = Vector3.Lerp(a.p, b.p, t),
            n = Vector3.Lerp(a.n, b.n, t).normalized,
            uv = Vector2.Lerp(a.uv, b.uv, t)
        };
    }

    private void CacheFrames()
    {
        int steps = Mathf.Clamp(revealSteps, 4, 24);
        frames = new Mesh[steps + 1];

        // 원본 데이터를 한 번만 읽음.
        Vector3[] positions = source.vertices;
        Vector3[] normals = source.normals;
        Vector2[] uv = source.uv;
        int[][] triangles = new int[source.subMeshCount][];
        for (int s = 0; s < triangles.Length; s++) triangles[s] = source.GetTriangles(s);

        Bounds bounds = source.bounds;
        float low = Mathf.Min(Coord(bounds.min), Coord(bounds.max));
        float high = Mathf.Max(Coord(bounds.min), Coord(bounds.max));

        // 0 프레임은 비어있게, 1~steps는 점차 전체가 보이게.
        for (int step = 0; step <= steps; step++)
        {
            float cutoff = Mathf.Lerp(high, low, (float)step / steps);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var subIndices = new List<int>[triangles.Length];

            for (int s = 0; s < triangles.Length; s++)
            {
                subIndices[s] = new List<int>();
                if (step == 0) continue;
                int[] tris = triangles[s];
                for (int k = 0; k < tris.Length; k += 3)
                {
                    Vertex[] tri = new Vertex[3];
                    for (int j = 0; j < 3; j++)
                    {
                        int idx = tris[k + j];
                        tri[j] = new Vertex
                        {
                            p = positions[idx],
                            n = normals.Length == positions.Length ? normals[idx] : Vector3.forward,
                            uv = uv.Length == positions.Length ? uv[idx] : Vector2.zero
                        };
                    }

                    var clipped = new List<Vertex>(4);
                    for (int j = 0; j < 3; j++)
                    {
                        Vertex a = tri[j], b = tri[(j + 1) % 3];
                        float da = Coord(a.p) - cutoff, db = Coord(b.p) - cutoff;
                        bool insideA = da >= -0.00001f, insideB = db >= -0.00001f;
                        if (insideA) clipped.Add(a);
                        if (insideA != insideB)
                            clipped.Add(Interpolate(a, b, Mathf.Clamp01(da / (da - db))));
                    }
                    if (clipped.Count < 3) continue;
                    int first = verts.Count;
                    foreach (Vertex v in clipped)
                    {
                        verts.Add(v.p);
                        norms.Add(v.n);
                        uvs.Add(v.uv);
                    }
                    for (int j = 1; j < clipped.Count - 1; j++)
                    {
                        subIndices[s].Add(first);
                        subIndices[s].Add(first + j);
                        subIndices[s].Add(first + j + 1);
                    }
                }
            }

            Mesh m = new Mesh { name = source.name + "_Reveal_" + step };
            if (verts.Count > 65535) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.subMeshCount = subIndices.Length;
            for (int s = 0; s < subIndices.Length; s++) m.SetTriangles(subIndices[s], s, false);
            // 비어있거나 잘린 상태에서도 원래 bounds 유지. 카메라 컬링 시 깜빡임 방지.
            m.bounds = source.bounds;
            frames[step] = m;
        }
    }

    public void Play(float attackSlashDuration)
    {
        if (!enabled || frames == null) return;
        Stop();
        progress = 0f;
        SetAlpha(1f);
        SetProgress(0f);
        rend.enabled = true;

        float duration = attackSlashDuration > 0f ? attackSlashDuration : defaultRevealDuration;
        if (logPlayDiagnostics)
        {
            Debug.Log($"[PillowSlashVFX] Play 호출됨 | 오브젝트={name} | 프레임={Time.frameCount} | 위치={transform.position} | 부모활성={gameObject.activeInHierarchy} | 카메라표시={rend.isVisible}", this);
        }

        if (forceFullMeshForTesting)
        {
            SetProgress(1f);
            sequence = DOTween.Sequence()
                .AppendInterval(duration + fullVisibleHold)
                .Append(DOTween.To(() => 1f, SetAlpha, 0f, fadeDuration).SetEase(Ease.OutQuad))
                .OnComplete(Hide);
        }
        else
        {
            sequence = DOTween.Sequence()
                .Append(DOTween.To(() => progress, SetProgress, 1f, duration).SetEase(Ease.Linear))
                .AppendInterval(fullVisibleHold)
                .Append(DOTween.To(() => 1f, SetAlpha, 0f, fadeDuration).SetEase(Ease.OutQuad))
                .OnComplete(Hide);
        }
    }

    private void SetProgress(float value)
    {
        progress = value;
        if (filter != null && frames != null)
            filter.sharedMesh = frames[Mathf.Clamp(Mathf.CeilToInt(value * (frames.Length - 1)), 0, frames.Length - 1)];
    }

    private void SetAlpha(float alpha)
    {
        if (!hasColor || rend == null) return;
        props.Clear();
        Color c = baseColor;
        c.a *= Mathf.Clamp01(alpha);
        props.SetColor(colorId, c);
        rend.SetPropertyBlock(props);
    }

    public void Stop()
    {
        if (sequence != null && sequence.IsActive()) sequence.Kill();
        sequence = null;
        Hide();
    }

    private void Hide()
    {
        if (rend != null) rend.enabled = false;
    }

    [ContextMenu("Test Slash (Play Mode)")]
    private void TestSlash()
    {
        if (Application.isPlaying) Play(defaultRevealDuration);
    }

    private void OnDisable()
    {
        if (Application.isPlaying) Stop();
    }

    private void OnDestroy()
    {
        if (sequence != null && sequence.IsActive()) sequence.Kill();
        if (frames != null)
            foreach (Mesh m in frames) if (m != null) Destroy(m);
    }
}
