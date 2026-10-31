using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 수문(TimedGate)을 여는 레버다. 베개(SwordHitbox)가 이 오브젝트의 콜라이더에서 IHittable을 찾아 OnHit()을 호출한다.
// 맞으면 레버 막대가 펀치로 꺾이고, 문이 열려 있는 동안 색이 바뀐다.
[RequireComponent(typeof(Collider))]
public class TimedGateLever : MonoBehaviour, IHittable
{
    [Title("연결")]
    [SerializeField, LabelText("열 문")]
    private TimedGate gate;

    [SerializeField, LabelText("레버 모델")]
    private Transform handle;
    // 펀치 회전을 줄 자식 막대다. 콜라이더가 붙은 루트는 움직이지 않게 따로 둔다.

    [SerializeField, LabelText("색을 바꿀 렌더러")]
    private Renderer handleRenderer;

    [SerializeField, LabelText("AudioSource (선택)")]
    private AudioSource audioSource;

    [SerializeField, LabelText("타격 소리 (선택)")]
    private AudioClip hitClip;

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("펀치 회전")]
    private Vector3 punchRotation = new Vector3(0f, 0f, 45f);

    [SerializeField, LabelText("펀치 시간")]
    private float punchDuration = 0.4f;

    [SerializeField, LabelText("대기 색")]
    private Color idleColor = new Color(0.9f, 0.2f, 0.15f, 1f);

    [SerializeField, LabelText("열림 색")]
    private Color openColor = new Color(0.2f, 1f, 0.4f, 1f);

    [SerializeField, LabelText("색 전환 시간")]
    private float colorDuration = 0.2f;

    private Tween _punchTween;
    private Tween _colorTween;
    private Material _material;

    private void Awake()
    {
        if (handleRenderer != null)
        {
            _material = handleRenderer.material;
            _material.SetColor("_BaseColor", idleColor);
        }
    }

    private void OnEnable()
    {
        if (gate != null) gate.OnOpenChanged += OnGateChanged;
    }

    private void OnDisable()
    {
        if (gate != null) gate.OnOpenChanged -= OnGateChanged;
        _punchTween?.Kill();
        _colorTween?.Kill();
    }

    private void OnDestroy()
    {
        _punchTween?.Kill();
        _colorTween?.Kill();
    }

    // SwordHitbox가 호출한다.
    public void OnHit()
    {
        if (gate != null) gate.Open();

        if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip);
        if (handle != null)
        {
            _punchTween?.Kill();
            _punchTween = handle.DOPunchRotation(punchRotation, punchDuration, 6, 0.5f);
        }
    }

    private void OnGateChanged(bool open)
    {
        if (_material == null) return;
        _colorTween?.Kill();
        _colorTween = _material.DOColor(open ? openColor : idleColor, "_BaseColor", colorDuration);
    }

    [Button("열기 테스트")]
    private void TestHit()
    {
        if (Application.isPlaying) OnHit();
    }
}
