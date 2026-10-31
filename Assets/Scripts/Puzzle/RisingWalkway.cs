using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 버튼이 눌리면 가라앉아 있던 길 조각들이 순서대로 솟아올라 돌아가는 길을 만든다.
// 한 번 올라오면 다시 내려가지 않는다.
public class RisingWalkway : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("트리거 버튼")]
    private PressButton triggerButton;
    // 이 버튼이 Pressed/Locked 상태가 되면 길이 올라온다.

    [SerializeField, LabelText("길 조각 (올라오는 순서)")]
    private Transform[] segments;
    // 씬에 배치된 위치가 "올라온 최종 위치"다. Awake에서 riseHeight만큼 내려둔다.

    [Title("연출 설정")]
    [SerializeField, LabelText("가라앉은 깊이")]
    private float riseHeight = 3.5f;
    // 값이 클수록 시작 시 더 깊이 가라앉아 있다.

    [SerializeField, LabelText("조각당 상승 시간")]
    private float riseDuration = 0.5f;

    [SerializeField, LabelText("조각 간 간격")]
    private float segmentInterval = 0.15f;
    // 값이 클수록 조각이 천천히 차례로 올라온다.

    [SerializeField, LabelText("상승 Ease")]
    private Ease riseEase = Ease.OutBack;

    [Title("튜브 대피")]
    [SerializeField, LabelText("튜브 (선택)")]
    private FloatTube tube;
    // 길이 올라올 자리에 튜브가 있으면 길에 끼이지 않도록 옮기기 위해 연결한다.

    [SerializeField, LabelText("튜브 대피 위치 (선택)")]
    private Transform tubeEvacuatePoint;
    // XZ만 사용한다. 튜브는 Y가 고정이라 Y는 유지한다.

    [Title("사운드")]
    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;

    [SerializeField, LabelText("상승 소리")]
    private AudioClip riseClip;
    // 시작할 때 한 번만 재생한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("올라옴 여부")]
    private bool _isRaised;

    private float[] _raisedY;
    private Sequence _sequence;

    private void Awake()
    {
        // 최종(씬) 높이를 기록한 뒤 가라앉혀서 시작한다.
        if (segments == null) return;
        _raisedY = new float[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            Vector3 p = segments[i].position;
            _raisedY[i] = p.y;
            segments[i].position = new Vector3(p.x, p.y - riseHeight, p.z);
        }
    }

    private void Start()
    {
        if (triggerButton != null)
        {
            triggerButton.OnStateChanged += OnButtonStateChanged;
            // 이미 눌린 상태로 시작하는 경우도 대비한다.
            OnButtonStateChanged(triggerButton);
        }
    }

    private void OnButtonStateChanged(PressButton btn)
    {
        if (_isRaised) return;
        if (btn.State == PressButton.ButtonState.Pressed || btn.State == PressButton.ButtonState.Locked)
            Raise();
    }

    // 외부/테스트에서도 호출 가능. 중복 호출해도 한 번만 실행된다.
    public void Raise()
    {
        if (_isRaised || segments == null || _raisedY == null) return;
        _isRaised = true;

        EvacuateTubeIfNeeded();

        if (audioSource != null && riseClip != null)
            audioSource.PlayOneShot(riseClip);

        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            _sequence.Insert(i * segmentInterval,
                segments[i].DOMoveY(_raisedY[i], riseDuration).SetEase(riseEase));
        }
    }

    // 솟아오를 조각과 겹친 튜브를 안전한 곳으로 옮긴다. 탑승 중이면 건드리지 않는다.
    private void EvacuateTubeIfNeeded()
    {
        if (tube == null || tubeEvacuatePoint == null || tube.IsMounted) return;

        Collider tubeCol = tube.GetComponent<Collider>();
        Rigidbody rb = tube.GetComponent<Rigidbody>();
        if (tubeCol == null || rb == null) return;

        Bounds tb = tubeCol.bounds;
        bool overlap = false;
        for (int i = 0; i < segments.Length && !overlap; i++)
        {
            if (segments[i] == null) continue;
            Collider sc = segments[i].GetComponent<Collider>();
            if (sc == null) continue;
            // 가라앉은 상태의 bounds를 올라온 높이로 환산해 수직 방향을 넉넉히 연다.
            Bounds sb = sc.bounds;
            sb.Expand(new Vector3(0f, riseHeight * 2f + 20f, 0f));
            overlap = sb.Intersects(tb);
        }
        if (!overlap) return;

        rb.linearVelocity = Vector3.zero;
        Vector3 pos = new Vector3(tubeEvacuatePoint.position.x, rb.position.y, tubeEvacuatePoint.position.z);
        rb.position = pos;
        tube.transform.position = pos;
    }

    [Button("길 올리기 테스트")]
    private void TestRaise()
    {
        if (!Application.isPlaying) return;
        Raise();
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        if (triggerButton != null)
            triggerButton.OnStateChanged -= OnButtonStateChanged;
    }
}
