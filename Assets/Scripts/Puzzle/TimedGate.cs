using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 레버(TimedGateLever)로 잠깐 열리는 수문이다. BoxCollider가 튜브를 막고 있다가 Open()이 호출되면 위로 올라간다.
// 열려 있는 시간이 끝나도 튜브가 문 자리 안에 있으면 닫히지 않고 기다렸다가, 비워진 뒤에 닫는다.
// (문이 튜브를 끼워 죽이는 것을 막기 위해서다.)
[RequireComponent(typeof(BoxCollider))]
public class TimedGate : MonoBehaviour
{
    [Title("설정")]
    [SerializeField, LabelText("열릴 때 이동 높이(m)")]
    private float liftHeight = 6f;
    // 양수면 위로 올라가며 열리고, 음수면 아래(물속)로 가라앉으며 열린다.
    // 탑다운 카메라가 비스듬히 내려다보는 곳에서는 위로 올라간 수문이 화면을 가리므로 음수로 가라앉게 쓴다.

    [SerializeField, LabelText("열리는 시간(초)")]
    private float openTweenDuration = 0.4f;

    [SerializeField, LabelText("열린 채 유지(초)")]
    private float openDuration = 4f;
    // 값이 길수록 지나가기 쉽다.

    [SerializeField, LabelText("닫히는 시간(초)")]
    private float closeTweenDuration = 0.6f;

    [SerializeField, LabelText("Ease")]
    private Ease ease = Ease.OutQuad;

    [Title("연결")]
    [SerializeField, LabelText("AudioSource (선택)")]
    private AudioSource audioSource;

    [SerializeField, LabelText("열림 소리 (선택)")]
    private AudioClip openClip;

    [SerializeField, LabelText("닫힘 소리 (선택)")]
    private AudioClip closeClip;

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("열림 중")]
    public bool IsOpen { get; private set; }

    [ShowInInspector, ReadOnly, LabelText("남은 열림 시간")]
    private float _remaining;

    private Tween _moveTween;
    private Vector3 _closedPosition;
    private BoxCollider _box;
    private bool _closing;

    // 레버가 구독하는 이벤트다. 열림/닫힘이 바뀔 때 true/false로 알린다.
    public event System.Action<bool> OnOpenChanged;

    private void Awake()
    {
        _closedPosition = transform.position;
        _box = GetComponent<BoxCollider>();
    }

    private void OnDisable()
    {
        _moveTween?.Kill();
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
    }

    // 레버가 호출한다. 이미 열려 있으면 열린 시간만 다시 채운다.
    public void Open()
    {
        _remaining = openDuration;
        if (IsOpen && !_closing) return;

        IsOpen = true;
        _closing = false;
        OnOpenChanged?.Invoke(true);
        if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);

        _moveTween?.Kill();
        _moveTween = transform
            .DOMoveY(_closedPosition.y + liftHeight, openTweenDuration)
            .SetEase(ease);
    }

    private void Update()
    {
        if (!IsOpen || _closing) return;

        _remaining -= Time.deltaTime;
        if (_remaining > 0f) return;

        // 튜브가 문 자리 안에 있으면 닫지 않고 비워질 때까지 기다린다.
        if (IsTubeInFootprint()) { _remaining = 0f; return; }

        Close();
    }

    private void Close()
    {
        _closing = true;
        if (audioSource != null && closeClip != null) audioSource.PlayOneShot(closeClip);

        _moveTween?.Kill();
        _moveTween = transform
            .DOMoveY(_closedPosition.y, closeTweenDuration)
            .SetEase(Ease.InQuad)
            .OnUpdate(() =>
            {
                // 내려오는 도중 튜브가 끼어들면 다시 올려 보낸다.
                if (IsTubeInFootprint())
                {
                    _closing = false;
                    _remaining = 0.5f;
                    _moveTween?.Kill();
                    _moveTween = transform.DOMoveY(_closedPosition.y + liftHeight, openTweenDuration).SetEase(ease);
                }
            })
            .OnComplete(() =>
            {
                IsOpen = false;
                _closing = false;
                OnOpenChanged?.Invoke(false);
            });
    }

    // 닫힌 위치의 문 자리에 FloatTube가 겹쳐 있는지 검사한다. 문이 올라가 있어도 닫힌 자리 기준이다.
    private bool IsTubeInFootprint()
    {
        if (_box == null) return false;
        Vector3 center = new Vector3(transform.position.x, _closedPosition.y, transform.position.z)
                         + Vector3.Scale(_box.center, transform.lossyScale);
        Vector3 half = Vector3.Scale(_box.size, transform.lossyScale) * 0.5f;
        Collider[] hits = Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            if (c.GetComponent<FloatTube>() != null) return true;
        }
        return false;
    }

    [Button("열기 테스트")]
    private void TestOpen()
    {
        if (Application.isPlaying) Open();
    }
}
