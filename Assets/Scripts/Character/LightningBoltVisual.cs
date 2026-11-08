using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 번개 한 줄을 LineRenderer로 그리는 연출용 컴포넌트다. 피해 판정은 하지 않는다(판정은 패턴 쪽이 한다).
// Play(from, to)를 부르면 지그재그 선을 만들고, 밝게 번쩍인 뒤 알파가 줄어들며 사라진다.
// 같은 인스턴스를 다시 Play해도 되고, destroyAfterPlay를 켜면 끝난 뒤 스스로 파괴된다.
[RequireComponent(typeof(LineRenderer))]
public class LightningBoltVisual : MonoBehaviour
{
    [Title("모양 설정")]
    [SerializeField, LabelText("분할 개수")]
    private int segments = 10;
    // 많을수록 선이 더 잘게 꺾인다.

    [SerializeField, LabelText("옆으로 흔들리는 폭")]
    private float jitter = 0.6f;
    // 클수록 번개가 크게 구불거린다.

    [SerializeField, LabelText("선 굵기")]
    private float width = 0.25f;

    [SerializeField, LabelText("번쩍 색")]
    private Color boltColor = new Color(1f, 0.95f, 0.5f, 1f);

    [Title("DOTween 설정")]
    [SerializeField, LabelText("번쩍이는 시간")]
    private float flashDuration = 0.08f;

    [SerializeField, LabelText("사라지는 시간")]
    private float fadeDuration = 0.25f;

    [SerializeField, LabelText("끝나면 파괴")]
    private bool destroyAfterPlay = true;

    private LineRenderer _line;
    private Sequence _sequence;
    private float _alpha;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.enabled = false;
    }

    private void OnDisable()
    {
        _sequence?.Kill();
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    // from -> to 지그재그 선을 만들고 번쩍였다가 사라진다.
    public void Play(Vector3 from, Vector3 to)
    {
        _sequence?.Kill();
        BuildPolyline(from, to);

        _line.startWidth = width;
        _line.endWidth = width;
        _alpha = 1f;
        ApplyAlpha();
        _line.enabled = true;

        // 짧게 유지한 뒤 알파를 0으로 낮춘다.
        _sequence = DOTween.Sequence()
            .AppendInterval(flashDuration)
            .Append(DOTween.To(() => _alpha, a => { _alpha = a; ApplyAlpha(); }, 0f, fadeDuration))
            .OnComplete(Finish)
            .SetLink(gameObject);
    }

    private void BuildPolyline(Vector3 from, Vector3 to)
    {
        int count = Mathf.Max(segments, 1) + 1;
        _line.positionCount = count;

        Vector3 dir = to - from;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(dir, Vector3.right);
        side.Normalize();
        Vector3 side2 = Vector3.Cross(dir.normalized, side);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            Vector3 p = Vector3.Lerp(from, to, t);

            // 양 끝은 고정하고 중간만 흔들어 시작점과 맞은 자리가 정확히 이어지게 한다.
            if (i > 0 && i < count - 1)
                p += side * Random.Range(-jitter, jitter) + side2 * Random.Range(-jitter, jitter);

            _line.SetPosition(i, p);
        }
    }

    private void ApplyAlpha()
    {
        Color c = boltColor;
        c.a = _alpha;
        _line.startColor = c;
        _line.endColor = c;
    }

    private void Finish()
    {
        _line.enabled = false;
        if (destroyAfterPlay) Destroy(gameObject);
    }

    [Button("테스트 번개 (Play Mode)")]
    private void TestPlay()
    {
        if (Application.isPlaying) Play(transform.position + Vector3.up * 10f, transform.position);
    }
}
