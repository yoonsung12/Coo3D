using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 칼바람 패턴의 바닥 예고 표시다. 보스에서 뻗는 붉은 부채꼴과 밝은 테두리, 그리고 경로 끝(섬 가장자리~강물)의
// 진한 띠를 그린다. 바위에 가려진 방향은 부채꼴이 바위 앞에서 잘려서 안전지대가 바닥에 그대로 보인다.
// 플레이어가 그림자 안에 있는지도 이 클래스의 IsBlockedByRock으로 판정해, 보이는 모양과 실제 판정을 일치시킨다.
public class WindFanTelegraph : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("예고 머티리얼")]
    private Material material;
    // BI_Telegraph(URP Unlit 투명)를 연결한다. 색은 MaterialPropertyBlock으로만 바꾼다.

    [Title("모양")]
    [SerializeField, LabelText("바닥 위 높이")]
    private float groundY = 0.03f;
    // 지면과 같은 높이면 z-fighting으로 깜빡이므로 살짝 띄운다.

    [SerializeField, LabelText("분할 수"), Range(12, 96)]
    private int segments = 48;
    // 부채꼴을 나누는 칸 수다. 클수록 바위 그림자 경계가 매끈해지지만 매 프레임 계산이 늘어난다.

    [SerializeField, LabelText("테두리 두께")]
    private float outlineWidth = 0.14f;

    [Title("섬 모양")]
    [SerializeField, LabelText("섬 반축 X")]
    private float islandSemiAxisX = 13.5f;

    [SerializeField, LabelText("섬 반축 Z")]
    private float islandSemiAxisZ = 9f;
    // 진한 띠가 섬 가장자리부터 시작하도록 섬 타원 크기를 넣는다.

    [Title("색")]
    [SerializeField, LabelText("경로 색")]
    private Color fanColor = new Color(0.9f, 0.1f, 0.08f, 0.38f);

    [SerializeField, LabelText("경로 끝 강조 색")]
    private Color edgeBandColor = new Color(0.8f, 0f, 0f, 0.7f);

    [SerializeField, LabelText("테두리 색")]
    private Color outlineColor = new Color(1f, 0.97f, 0.9f, 0.95f);

    [Title("DOTween 설정")]
    [SerializeField, LabelText("나타나는 시간")]
    private float fadeInDuration = 0.2f;

    [SerializeField, LabelText("사라지는 시간")]
    private float fadeOutDuration = 0.25f;

    [SerializeField, LabelText("고정 점멸 한 번 시간")]
    private float lockFlashDuration = 0.08f;

    [SerializeField, LabelText("고정 점멸 횟수")]
    private int lockFlashCount = 3;

    [SerializeField, LabelText("바람이 부는 동안 밝기")]
    private float windAlpha = 0.5f;
    // 바람 입자가 잘 보이도록 경로를 조금 흐리게 둔다. 그림자는 계속 보이게 끄지는 않는다.

    [Title("이동 화살표")]
    [SerializeField, LabelText("화살촉 길이")]
    private float sweepArrowHeadLength = 1.1f;

    private float _orbitArrowAngle;
    // 0이면 화살표를 그리지 않는다. 부호가 보스의 이동 방향이다.

    private Vector3 _orbitArrowStart;
    private Vector3 _orbitArrowCenter;

    private Mesh _fanMesh;
    private Mesh _bandMesh;
    private Mesh _outlineMesh;
    private MeshRenderer _fanRenderer;
    private MeshRenderer _bandRenderer;
    private MeshRenderer _outlineRenderer;
    private MaterialPropertyBlock _block;
    private float _alpha;
    private Tween _alphaTween;

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<int> _triangles = new List<int>();
    private readonly List<Vector3> _rim = new List<Vector3>();
    private readonly List<Collider> _rockColliders = new List<Collider>();
    private readonly List<IslandRock> _rockOwners = new List<IslandRock>();

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _fanMesh = CreateLayer("Fan", 0, out _fanRenderer);
        _bandMesh = CreateLayer("EdgeBand", 1, out _bandRenderer);
        _outlineMesh = CreateLayer("Outline", 2, out _outlineRenderer);
        SetLayersActive(false);
    }

    private void OnDestroy()
    {
        _alphaTween?.Kill();
        if (_fanMesh != null) Destroy(_fanMesh);
        if (_bandMesh != null) Destroy(_bandMesh);
        if (_outlineMesh != null) Destroy(_outlineMesh);
    }

    // 그림자를 만들 바위를 등록한다. 부서졌거나 땅속에 있는 바위는 판정할 때 걸러진다.
    public void SetRocks(IReadOnlyList<IslandRock> rocks)
    {
        _rockColliders.Clear();
        _rockOwners.Clear();
        if (rocks == null) return;

        foreach (IslandRock rock in rocks)
        {
            if (rock == null) continue;
            foreach (Collider col in rock.GetComponentsInChildren<Collider>(true))
            {
                _rockColliders.Add(col);
                _rockOwners.Add(rock);
            }
        }
    }

    // 예고를 페이드인으로 보여 준다. 모양은 UpdateFan으로 채운다.
    public void Show()
    {
        SetLayersActive(true);
        FadeTo(1f, fadeInDuration);
    }

    // 방향이 고정됐다는 신호로 빠르게 깜빡인다.
    public void PlayLockFlash()
    {
        _alphaTween?.Kill();
        _alpha = 1f;
        ApplyColors();
        _alphaTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColors(); }, 0.35f, lockFlashDuration)
            .SetLoops(lockFlashCount * 2, LoopType.Yoyo);
    }

    // 바람이 시작되면 경로를 조금 흐리게 만든다.
    public void DimForWind()
    {
        FadeTo(windAlpha, fadeInDuration);
    }

    // 다음 UpdateFan부터 보스가 어느 쪽으로 이동할지 보여 주는 호를 함께 그린다. signedAngle이 0이면 그리지 않는다.
    public void SetOrbitArrow(Vector3 start, Vector3 center, float signedAngle)
    {
        _orbitArrowStart = new Vector3(start.x, 0f, start.z);
        _orbitArrowCenter = new Vector3(center.x, 0f, center.z);
        _orbitArrowAngle = signedAngle;
    }

    public void ClearOrbitArrow()
    {
        _orbitArrowAngle = 0f;
    }

    public void Hide()
    {
        _alphaTween?.Kill();
        _alphaTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColors(); }, 0f, fadeOutDuration)
            .OnComplete(() => SetLayersActive(false));
    }

    public void HideImmediate()
    {
        _alphaTween?.Kill();
        _alpha = 0f;
        SetLayersActive(false);
    }

    // origin에서 direction으로 벌어진 부채꼴 모양을 다시 만든다. 바위가 움직이지 않는 동안에는 방향이 바뀔 때만 부르면 된다.
    public void UpdateFan(Vector3 origin, Vector3 direction, float angle, float radius, Vector3 islandCenter)
    {
        transform.position = new Vector3(origin.x, groundY, origin.z);
        transform.rotation = Quaternion.identity;

        Vector3 flatOrigin = new Vector3(origin.x, 0f, origin.z);
        float baseAngle = Mathf.Atan2(direction.z, direction.x);
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;

        _rim.Clear();
        List<float> edgeStart = new List<float>(segments + 1);
        List<bool> reachesEdge = new List<bool>(segments + 1);

        for (int i = 0; i <= segments; i++)
        {
            float a = baseAngle - halfAngle + (halfAngle * 2f) * i / segments;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            bool blocked = TryGetRockDistance(flatOrigin, dir, radius, out float rockDistance);
            float length = blocked ? rockDistance : radius;
            _rim.Add(dir * length);

            float edge = GetDistanceToIslandEdge(flatOrigin, dir, islandCenter);
            edgeStart.Add(edge);
            reachesEdge.Add(!blocked && edge < radius);
        }

        BuildFanMesh();
        BuildBandMesh(edgeStart, reachesEdge);
        BuildOutlineMesh();
    }

    // 보스(origin)에서 target까지 사이를 솟은 바위가 가리는지 확인한다. 플레이어의 바람 판정에 쓴다.
    public bool IsBlockedByRock(Vector3 origin, Vector3 target)
    {
        Vector3 flatOrigin = new Vector3(origin.x, 0f, origin.z);
        Vector3 toTarget = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
        float distance = toTarget.magnitude;
        if (distance < 0.001f) return false;

        return TryGetRockDistance(flatOrigin, toTarget / distance, distance, out _);
    }

    // 솟아 있는 바위 콜라이더에 직접 광선을 쏴서 가장 가까운 거리를 구한다.
    // 바위만 검사하므로 나무나 플레이어에 가려 안전지대가 잘못 생기는 일이 없다.
    private bool TryGetRockDistance(Vector3 flatOrigin, Vector3 dir, float maxDistance, out float distance)
    {
        distance = maxDistance;
        bool found = false;
        Ray ray = new Ray(flatOrigin + Vector3.up * 0.5f, dir);

        for (int i = 0; i < _rockColliders.Count; i++)
        {
            IslandRock owner = _rockOwners[i];
            if (owner == null || !owner.IsRisen || owner.IsBroken) continue;

            if (_rockColliders[i].Raycast(ray, out RaycastHit hit, maxDistance) && hit.distance < distance)
            {
                distance = hit.distance;
                found = true;
            }
        }
        return found;
    }

    // 타원 (x/a)^2 + (z/b)^2 = 1 과 광선이 만나는 먼 쪽 교점까지의 거리다. 진한 띠의 시작 위치로 쓴다.
    private float GetDistanceToIslandEdge(Vector3 origin, Vector3 dir, Vector3 center)
    {
        float px = (origin.x - center.x) / islandSemiAxisX;
        float pz = (origin.z - center.z) / islandSemiAxisZ;
        float dx = dir.x / islandSemiAxisX;
        float dz = dir.z / islandSemiAxisZ;

        float a = dx * dx + dz * dz;
        float b = 2f * (px * dx + pz * dz);
        float c = px * px + pz * pz - 1f;
        float disc = b * b - 4f * a * c;
        if (a < 0.0001f || disc < 0f) return 0f;

        return Mathf.Max((-b + Mathf.Sqrt(disc)) / (2f * a), 0f);
    }

    private void BuildFanMesh()
    {
        _vertices.Clear();
        _triangles.Clear();
        _vertices.Add(Vector3.zero);
        _vertices.AddRange(_rim);

        for (int i = 0; i < segments; i++)
        {
            _triangles.Add(0);
            _triangles.Add(i + 2);
            _triangles.Add(i + 1);
        }
        ApplyMesh(_fanMesh);
    }

    private void BuildBandMesh(List<float> edgeStart, List<bool> reachesEdge)
    {
        _vertices.Clear();
        _triangles.Clear();

        for (int i = 0; i < segments; i++)
        {
            if (!reachesEdge[i] || !reachesEdge[i + 1]) continue;

            Vector3 outerA = _rim[i];
            Vector3 outerB = _rim[i + 1];
            Vector3 innerA = outerA.normalized * edgeStart[i];
            Vector3 innerB = outerB.normalized * edgeStart[i + 1];

            int start = _vertices.Count;
            _vertices.Add(innerA);
            _vertices.Add(innerB);
            _vertices.Add(outerB);
            _vertices.Add(outerA);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 1);
            _triangles.Add(start);
            _triangles.Add(start + 3);
            _triangles.Add(start + 2);
        }
        ApplyMesh(_bandMesh);
    }

    // 부채꼴 둘레(보스 -> 가장자리 -> 보스)를 얇은 띠로 그린다. 바위에 잘린 윤곽도 같이 따라가 그림자가 또렷해진다.
    private void BuildOutlineMesh()
    {
        _vertices.Clear();
        _triangles.Clear();

        AddOutlineSegment(Vector3.zero, _rim[0]);
        for (int i = 0; i < segments; i++)
            AddOutlineSegment(_rim[i], _rim[i + 1]);
        AddOutlineSegment(_rim[segments], Vector3.zero);
        AddOrbitArrow();

        ApplyMesh(_outlineMesh);
    }

    // 이동 회차의 전조에서 보스가 지나갈 길을 호와 화살촉으로 보여 준다.
    private void AddOrbitArrow()
    {
        if (Mathf.Approximately(_orbitArrowAngle, 0f)) return;

        // 메시는 부채꼴 시작점(보스 위치) 기준 좌표라서 월드 좌표에서 그만큼 빼서 쓴다.
        Vector3 localOrigin = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 radiusVector = _orbitArrowStart - _orbitArrowCenter;

        const int arcSegments = 12;
        Vector3 previous = _orbitArrowStart - localOrigin;
        Vector3 tip = previous;
        for (int i = 1; i <= arcSegments; i++)
        {
            tip = _orbitArrowCenter + Quaternion.AngleAxis(_orbitArrowAngle * i / arcSegments, Vector3.up) * radiusVector - localOrigin;
            AddOutlineSegment(previous, tip);
            previous = i == arcSegments ? previous : tip;
        }

        Vector3 along = (tip - previous).normalized;
        Vector3 side = new Vector3(along.z, 0f, -along.x);
        AddOutlineSegment(tip, tip + (-along + side * 0.6f) * sweepArrowHeadLength);
        AddOutlineSegment(tip, tip + (-along - side * 0.6f) * sweepArrowHeadLength);
    }

    private void AddOutlineSegment(Vector3 from, Vector3 to)
    {
        Vector3 along = to - from;
        if (along.sqrMagnitude < 0.0001f) return;

        Vector3 side = new Vector3(along.z, 0f, -along.x).normalized * (outlineWidth * 0.5f);
        int start = _vertices.Count;
        _vertices.Add(from - side);
        _vertices.Add(from + side);
        _vertices.Add(to + side);
        _vertices.Add(to - side);
        _triangles.Add(start);
        _triangles.Add(start + 1);
        _triangles.Add(start + 2);
        _triangles.Add(start);
        _triangles.Add(start + 2);
        _triangles.Add(start + 3);
    }

    private void ApplyMesh(Mesh mesh)
    {
        mesh.Clear();
        mesh.SetVertices(_vertices);
        mesh.SetTriangles(_triangles, 0);
        mesh.RecalculateBounds();
    }

    private Mesh CreateLayer(string layerName, int sortingOrder, out MeshRenderer meshRenderer)
    {
        GameObject go = new GameObject(layerName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * (sortingOrder * 0.005f);
        // 겹치는 층끼리 z-fighting이 나지 않도록 층마다 조금씩 높인다.

        Mesh mesh = new Mesh { name = "WindFan_" + layerName };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sortingOrder = sortingOrder;
        return mesh;
    }

    private void SetLayersActive(bool active)
    {
        if (_fanRenderer != null) _fanRenderer.gameObject.SetActive(active);
        if (_bandRenderer != null) _bandRenderer.gameObject.SetActive(active);
        if (_outlineRenderer != null) _outlineRenderer.gameObject.SetActive(active);
    }

    private void FadeTo(float target, float duration)
    {
        _alphaTween?.Kill();
        _alphaTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColors(); }, target, duration);
    }

    private void ApplyColors()
    {
        ApplyColor(_fanRenderer, fanColor);
        ApplyColor(_bandRenderer, edgeBandColor);
        ApplyColor(_outlineRenderer, outlineColor);
    }

    private void ApplyColor(MeshRenderer target, Color color)
    {
        if (target == null) return;
        color.a *= _alpha;
        _block.SetColor("_BaseColor", color);
        target.SetPropertyBlock(_block);
    }
}
