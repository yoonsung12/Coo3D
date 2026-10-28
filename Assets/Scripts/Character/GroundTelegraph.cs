using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 바닥에 깔리는 공격 예고 표시다. 직사각형 레인(ShowLane)과 원(ShowCircle) 두 가지 모양을 지원한다.
// Awake에서 납작한 메쉬 자식 2개를 직접 만들고, URP Unlit 투명 머티리얼의 색만 MaterialPropertyBlock으로
// 바꿔서 쓴다(매 프레임 머티리얼 인스턴스가 새로 생기는 누수를 막기 위해서다).
// 탑다운 아레나 전용 예고이며, 기존 사이드뷰 예고(조준선 LineRenderer 등)와는 별개다.
public class GroundTelegraph : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("예고 머티리얼")]
    private Material material;
    // BI_Telegraph(URP Unlit 투명)를 연결한다. 모든 예고가 같은 머티리얼을 공유한다.

    [Title("모양/색 설정")]
    [SerializeField, LabelText("바닥 위 높이")]
    private float groundY = 0.02f;
    // 지면과 같은 높이면 깜빡이는 z-fighting이 생기므로 살짝 띄운다.

    [SerializeField, LabelText("평상시 색")]
    private Color normalColor = new Color(1f, 0.2f, 0.1f, 0.35f);

    [SerializeField, LabelText("고정(Lock) 시 색")]
    private Color lockColor = new Color(1f, 0.85f, 0.2f, 0.75f);

    [Title("DOTween 설정")]
    [SerializeField, LabelText("나타나는 시간")]
    private float fadeInDuration = 0.15f;

    [SerializeField, LabelText("사라지는 시간")]
    private float fadeOutDuration = 0.2f;

    [SerializeField, LabelText("깜빡임(펄스) 주기")]
    private float pulseDuration = 0.35f;

    [SerializeField, LabelText("펄스 최저 알파 비율")]
    private float pulseMinRatio = 0.55f;

    [SerializeField, LabelText("Lock 점멸 횟수")]
    private int lockFlashCount = 4;

    [SerializeField, LabelText("Lock 점멸 한 번 시간")]
    private float lockFlashDuration = 0.07f;

    private Transform _laneRoot;
    private Transform _circleRoot;
    private MeshRenderer _laneRenderer;
    private MeshRenderer _circleRenderer;
    private MaterialPropertyBlock _block;
    private Color _color;
    private float _alpha; // 색 알파에 곱해지는 0~1 배율
    private bool _visible;
    private bool _locked;
    private Tween _fadeTween;
    private Tween _pulseTween;
    private Tween _flashTween;
    private bool _warning; // true면 평상시/Lock 색 대신 경고색을 쓴다.
    private Color _warningColor;

    private static Mesh _laneMesh;
    private static Mesh _circleMesh;

    public bool IsVisible => _visible;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _laneRoot = CreateChild("Lane", GetLaneMesh(), out _laneRenderer);
        _circleRoot = CreateChild("Circle", GetCircleMesh(), out _circleRenderer);
        _laneRoot.gameObject.SetActive(false);
        _circleRoot.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    // from -> to 방향으로 폭 width의 직사각형 레인을 보여준다. 이미 보이는 중이면 위치/길이만 갱신한다(플레이어 추적용).
    public void ShowLane(Vector3 from, Vector3 to, float width)
    {
        Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
        float length = Mathf.Max(flat.magnitude, 0.01f);
        Vector3 dir = flat / length;

        transform.position = new Vector3(from.x, groundY, from.z);
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        _laneRoot.localScale = new Vector3(width, 1f, length);
        _laneRoot.gameObject.SetActive(true);
        _circleRoot.gameObject.SetActive(false);

        BeginShowIfNeeded();
    }

    // center 중심, 반지름 radius의 원을 보여준다. 이미 보이는 중이면 위치/크기만 갱신한다.
    public void ShowCircle(Vector3 center, float radius)
    {
        transform.position = new Vector3(center.x, groundY, center.z);
        transform.rotation = Quaternion.identity;
        _circleRoot.localScale = new Vector3(radius, 1f, radius);
        _circleRoot.gameObject.SetActive(true);
        _laneRoot.gameObject.SetActive(false);

        BeginShowIfNeeded();
    }

    // 예고를 고정하고 밝게 점멸시킨다. "이제 더 이상 따라가지 않는다"는 신호다.
    public void Lock()
    {
        if (!_visible || _locked) return;
        _locked = true;

        KillTweens();

        _color = lockColor;
        _alpha = 1f;
        ApplyColor();

        // 알파를 1 -> 0.4 -> 1 로 빠르게 반복해서 점멸시킨다.
        _flashTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColor(); }, 0.4f, lockFlashDuration)
            .SetLoops(lockFlashCount * 2, LoopType.Yoyo);
    }

    // 바위 충돌 경고처럼 예고 색을 통째로 바꾸고 싶을 때 쓴다. 알파/펄스/점멸은 기존 그대로 곱해진다.
    // 값이 같으면 다시 적용하지 않아 매 프레임 호출돼도 부담이 없다.
    public void SetWarningColor(Color c)
    {
        if (_warning && _warningColor == c) return;
        _warning = true;
        _warningColor = c;
        ApplyColor();
    }

    // 경고색을 해제하고 평상시/Lock 색으로 되돌린다.
    public void ClearWarningColor()
    {
        if (!_warning) return;
        _warning = false;
        ApplyColor();
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;

        KillTweens();
        _fadeTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColor(); }, 0f, fadeOutDuration)
            .OnComplete(HideImmediate);
    }

    public void HideImmediate()
    {
        KillTweens();
        _visible = false;
        _locked = false;
        _warning = false; // 숨겨질 때 경고색이 다음 예고로 새지 않게 한다.
        if (_laneRoot != null) _laneRoot.gameObject.SetActive(false);
        if (_circleRoot != null) _circleRoot.gameObject.SetActive(false);
    }

    private void BeginShowIfNeeded()
    {
        if (_visible) return;
        _visible = true;
        _locked = false;

        KillTweens();
        _color = normalColor;
        _alpha = 0f;
        ApplyColor();

        // 나타난 뒤에는 알파가 일정 주기로 깜빡여서 "곧 공격"임을 눈에 띄게 한다.
        _fadeTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColor(); }, 1f, fadeInDuration)
            .OnComplete(() =>
            {
                _pulseTween = DOTween.To(() => _alpha, a => { _alpha = a; ApplyColor(); }, pulseMinRatio, pulseDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            });
    }

    private void ApplyColor()
    {
        Color c = _warning ? _warningColor : _color;
        c.a *= _alpha;
        _block.SetColor("_BaseColor", c);
        if (_laneRenderer != null) _laneRenderer.SetPropertyBlock(_block);
        if (_circleRenderer != null) _circleRenderer.SetPropertyBlock(_block);
    }

    private void KillTweens()
    {
        if (_fadeTween != null) _fadeTween.Kill();
        if (_pulseTween != null) _pulseTween.Kill();
        if (_flashTween != null) _flashTween.Kill();
    }

    private Transform CreateChild(string childName, Mesh mesh, out MeshRenderer meshRenderer)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return go.transform;
    }

    // X: -0.5~0.5(폭), Z: 0~1(길이)인 위쪽을 보는 단위 사각형이다. 스케일로 폭/길이를 조절한다.
    private static Mesh GetLaneMesh()
    {
        if (_laneMesh != null) return _laneMesh;
        _laneMesh = new Mesh { name = "TelegraphLane" };
        _laneMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f),
            new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 0f, 0f)
        };
        _laneMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _laneMesh.RecalculateBounds();
        return _laneMesh;
    }

    // 반지름 1짜리 원판이다. 스케일 X/Z로 반지름을 조절한다.
    private static Mesh GetCircleMesh()
    {
        if (_circleMesh != null) return _circleMesh;
        const int segments = 48;
        Vector3[] verts = new Vector3[segments + 1];
        int[] tris = new int[segments * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            tris[i * 3] = 0;
            tris[i * 3 + 1] = (i + 1) % segments + 1;
            tris[i * 3 + 2] = i + 1;
        }
        _circleMesh = new Mesh { name = "TelegraphCircle", vertices = verts, triangles = tris };
        _circleMesh.RecalculateBounds();
        return _circleMesh;
    }
}
