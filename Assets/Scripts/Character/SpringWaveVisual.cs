using Sirenix.OdinInspector;
using UnityEngine;

// 봄 파도(BossSpringWave)의 시각 메쉬를 코드로 만들어 "파도처럼" 보이게 하는 컴포넌트다.
// 단면(뒤쪽 완만한 경사 -> 볼록한 마루 -> 앞쪽 가파른 면)을 X축으로 늘려 만들고,
// 정점 색을 아래(짙은 파랑) -> 위(흰 거품)로 칠한다. 마루 부분은 X를 따라 출렁이게 움직인다.
//
// 부모 visual의 스케일이 (길이, 높이, 1)로 걸리므로 메쉬는 정규화해서 만든다.
//  X: -0.5~0.5 (스케일 곱 = 파도 길이), Y: -0.5~0.5 (아래가 지면, 위가 판정 높이),
//  Z: 앞쪽 끝이 +0.5(히트박스 앞면), 뒤쪽 경사는 약 -2.5m까지 이어진다(z 스케일이 1이라 미터 단위).
// 이 오브젝트에는 콜라이더를 두지 않는다 — 판정은 BossSpringWave가 OverlapBox로 따로 한다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpringWaveVisual : MonoBehaviour
{
    [Title("메쉬 설정")]
    [SerializeField, LabelText("길이 방향 분할 수")]
    private int segments = 48;
    // 많을수록 출렁임이 부드럽지만 정점이 늘어난다. 파도 길이 32m 기준 48이면 충분하다.

    [Title("색상 (정점 색)")]
    [SerializeField, LabelText("바닥 색 (짙은 파랑)")]
    private Color bottomColor = new Color(0.05f, 0.25f, 0.6f, 0.9f);

    [SerializeField, LabelText("중간 색 (청록)")]
    private Color midColor = new Color(0.25f, 0.75f, 0.9f, 0.9f);

    [SerializeField, LabelText("마루 색 (흰 거품)")]
    private Color crestColor = new Color(1f, 1f, 1f, 0.95f);

    [Title("출렁임")]
    [SerializeField, LabelText("출렁임 세기 (정규화 높이)")]
    private float undulationAmplitude = 0.08f;
    // 값이 클수록 마루가 위아래로 크게 출렁인다. 1이 파도 전체 높이다.

    [SerializeField, LabelText("출렁임 속도")]
    private float undulationSpeed = 4f;

    [SerializeField, LabelText("출렁임 파장 (정규화 길이)")]
    private float undulationWavelength = 0.12f;
    // 1이 파도 전체 길이다. 0.12면 길이 32m 기준 약 3.8m마다 한 번 출렁인다.

    // 단면 정점 (z, y). 뒤쪽 바닥에서 시작해 마루를 지나 앞쪽 바닥으로 내려온다.
    private static readonly Vector2[] Profile =
    {
        new Vector2(-2.5f, -0.5f),
        new Vector2(-1.8f, -0.36f),
        new Vector2(-1.0f, -0.12f),
        new Vector2(-0.4f, 0.2f),
        new Vector2(0.0f, 0.42f),
        new Vector2(0.25f, 0.5f),   // 마루
        new Vector2(0.45f, 0.42f),  // 앞으로 말려 나온 끝
        new Vector2(0.5f, 0.24f),
        new Vector2(0.4f, -0.05f),
        new Vector2(0.38f, -0.3f),
        new Vector2(0.45f, -0.5f),
    };

    private Mesh _mesh;
    private Vector3[] _vertices;
    private float[] _baseY;      // 출렁이기 전 원래 Y
    private float[] _weight;     // 위쪽 정점일수록 크게 출렁이도록 하는 가중치(0~1)
    private float[] _phaseX;     // 정점 X를 위상으로 바꾼 값(라디안)
    private bool _built;

    private void Awake()
    {
        BuildMesh();
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    [Button("메쉬 다시 만들기 (테스트)")]
    public void BuildMesh()
    {
        int rows = Profile.Length;
        int cols = Mathf.Max(segments, 1) + 1;
        int count = rows * cols;

        _vertices = new Vector3[count];
        _baseY = new float[count];
        _weight = new float[count];
        _phaseX = new float[count];
        Color[] colors = new Color[count];
        Vector2[] uvs = new Vector2[count];

        for (int j = 0; j < cols; j++)
        {
            float x = (float)j / (cols - 1) - 0.5f;
            for (int i = 0; i < rows; i++)
            {
                int idx = j * rows + i;
                float y = Profile[i].y;
                float h01 = y + 0.5f; // 0(바닥) ~ 1(마루)

                _vertices[idx] = new Vector3(x, y, Profile[i].x);
                _baseY[idx] = y;
                _weight[idx] = Mathf.Clamp01((h01 - 0.4f) / 0.6f); // 아래쪽은 지면에 붙어 있어야 하므로 출렁이지 않는다.
                _phaseX[idx] = x / Mathf.Max(undulationWavelength, 0.001f) * Mathf.PI * 2f;
                colors[idx] = h01 < 0.5f
                    ? Color.Lerp(bottomColor, midColor, h01 / 0.5f)
                    : Color.Lerp(midColor, crestColor, (h01 - 0.5f) / 0.5f);
                uvs[idx] = new Vector2((float)j / (cols - 1), (float)i / (rows - 1));
            }
        }

        // 양면을 그리는 머티리얼(Cull Off)이므로 한쪽 방향 삼각형만 만든다.
        int[] tris = new int[(cols - 1) * (rows - 1) * 6];
        int t = 0;
        for (int j = 0; j < cols - 1; j++)
        {
            for (int i = 0; i < rows - 1; i++)
            {
                int a = j * rows + i;
                int b = a + 1;
                int c = a + rows;
                int d = c + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = c; tris[t++] = b; tris[t++] = d;
            }
        }

        if (_mesh == null) _mesh = new Mesh { name = "SpringWaveMesh" };
        else _mesh.Clear();

        _mesh.MarkDynamic(); // 매 프레임 정점을 갱신하므로 GPU에 동적 버퍼로 알려 준다.
        _mesh.vertices = _vertices;
        _mesh.colors = colors;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        // 출렁임 때문에 정점이 조금 움직여도 컬링되지 않도록 경계를 여유 있게 고정한다.
        _mesh.bounds = new Bounds(new Vector3(0f, 0f, -1f), new Vector3(1.2f, 1.4f, 4f));

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _built = true;
    }

    // 정점 단위 애니메이션은 DOTween으로 만들 수 없어서(오브젝트/값 하나가 아니라 정점 배열 전체를 갱신)
    // Update에서 캐시한 배열을 재사용해 직접 갱신한다. 매 프레임 새 배열을 만들지 않는다.
    private void Update()
    {
        if (!_built) return;

        float time = Time.time * undulationSpeed;
        for (int k = 0; k < _vertices.Length; k++)
        {
            float w = _weight[k];
            if (w <= 0f) continue;
            Vector3 v = _vertices[k];
            v.y = _baseY[k] + Mathf.Sin(_phaseX[k] + time) * undulationAmplitude * w;
            _vertices[k] = v;
        }
        _mesh.vertices = _vertices;
    }
}
