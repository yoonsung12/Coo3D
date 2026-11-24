using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어에게 길/목표를 알려 주는 월드 공간 안내 표시(화살표, 표지판 등)에 붙이는 컴포넌트다.
// 연출은 자식(visualRoot)만 움직이고, 도착 감지용 트리거 콜라이더는 루트에 두어 움직이지 않게 한다.
public class GuideArrow : MonoBehaviour
{
    // 반복 연출의 종류다.
    public enum MotionType { None, Bob, Pulse }

    [Title("연출 대상")]
    [SerializeField, LabelText("연출할 비주얼")]
    private Transform visualRoot;
    // 움직이거나 커졌다 작아질 자식 오브젝트를 연결한다. 트리거 콜라이더가 있는 루트는 연결하지 않는다.

    [Title("반복 연출")]
    [SerializeField, LabelText("연출 종류"), EnumToggleButtons]
    private MotionType motion = MotionType.Bob;
    // Bob: 위아래로 둥실, Pulse: 크기가 커졌다 작아짐, None: 가만히 있음.

    [SerializeField, LabelText("둥실 높이"), ShowIf("@motion == MotionType.Bob")]
    private float bobHeight = 0.3f;
    // 값이 클수록 위아래로 더 크게 움직인다(미터).

    [SerializeField, LabelText("커지는 배율"), ShowIf("@motion == MotionType.Pulse")]
    private float pulseScale = 1.15f;
    // 원래 크기의 몇 배까지 커질지 정한다.

    [SerializeField, LabelText("한 방향 시간"), ShowIf("@motion != MotionType.None")]
    private float loopDuration = 0.8f;
    // 값이 클수록 천천히 움직인다.

    [SerializeField, LabelText("Ease"), ShowIf("@motion != MotionType.None")]
    private Ease loopEase = Ease.InOutSine;

    [Title("숨김 조건")]
    [SerializeField, LabelText("플레이어 도착 시 숨김")]
    private bool hideOnPlayerReach = false;
    // 켜면 루트의 트리거 콜라이더에 플레이어가 들어올 때 숨긴다(다시 나타나지 않는다).

    [SerializeField, LabelText("꺼지면 숨길 오브젝트")]
    private GameObject hideWhenInactive;
    // 이 오브젝트가 비활성(퍼즐 해결 등)이면 숨기고, 다시 켜지면 다시 보인다. 비워 두면 사용하지 않는다.

    [SerializeField, LabelText("숨김/표시 시간")]
    private float hideDuration = 0.25f;

    [Title("상태")]
    [ShowInInspector, ReadOnly, LabelText("현재 숨김")]
    private bool _isHidden;

    private Vector3 _originLocalPosition;
    private Vector3 _originLocalScale;
    // 반복 연출이 끝나도 원래 자리/크기로 복원하기 위해 Awake에 저장해 둔다.

    private Tween _loopTween;
    // 반복 연출 Tween. 숨길 때 멈추고 다시 보일 때 재시작한다.
    private Tween _hideTween;
    // 숨김/표시 Tween. 중복 실행을 막기 위해 저장한다.
    private bool _reachedHidden;
    // 플레이어 도착으로 영구히 숨겨졌는지 여부다.
    private bool _lastTargetActive = true;

    private void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        _originLocalPosition = visualRoot.localPosition;
        _originLocalScale = visualRoot.localScale;
    }

    private void OnEnable()
    {
        _lastTargetActive = hideWhenInactive == null || hideWhenInactive.activeInHierarchy;
        // 처음 켜질 때 대상이 이미 꺼져 있으면 연출 없이 바로 숨긴다.
        if (!_lastTargetActive)
        {
            _isHidden = true;
            visualRoot.localScale = Vector3.zero;
            visualRoot.gameObject.SetActive(false);
            return;
        }
        if (!_isHidden) StartLoop();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void Update()
    {
        if (hideWhenInactive == null || _reachedHidden) return;

        // 매 프레임 bool 하나만 비교하고, 상태가 바뀐 순간에만 Tween을 만든다.
        bool active = hideWhenInactive.activeInHierarchy;
        if (active == _lastTargetActive) return;
        _lastTargetActive = active;
        if (active) Show();
        else Hide();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hideOnPlayerReach || _reachedHidden) return;
        // 다른 트리거 스크립트들과 같은 방식으로 플레이어를 판별한다.
        if (other.GetComponent<PlayerController>() == null) return;
        _reachedHidden = true;
        Hide();
    }

    // 반복 연출(둥실/맥동)을 시작한다.
    private void StartLoop()
    {
        _loopTween?.Kill();
        visualRoot.localPosition = _originLocalPosition;
        visualRoot.localScale = _originLocalScale;

        if (motion == MotionType.Bob)
        {
            _loopTween = visualRoot
                .DOLocalMoveY(_originLocalPosition.y + bobHeight, loopDuration)
                .SetEase(loopEase)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (motion == MotionType.Pulse)
        {
            _loopTween = visualRoot
                .DOScale(_originLocalScale * pulseScale, loopDuration)
                .SetEase(loopEase)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    // 크기를 0으로 줄이며 숨긴다.
    [Button("숨기기 테스트")]
    private void Hide()
    {
        if (_isHidden) return;
        _isHidden = true;
        _loopTween?.Kill();
        _hideTween?.Kill();
        _hideTween = visualRoot
            .DOScale(Vector3.zero, hideDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => visualRoot.gameObject.SetActive(false));
    }

    // 원래 크기로 되살린다.
    [Button("보이기 테스트")]
    private void Show()
    {
        if (!_isHidden) return;
        _isHidden = false;
        _hideTween?.Kill();
        _loopTween?.Kill();
        visualRoot.localPosition = _originLocalPosition;
        visualRoot.localScale = Vector3.zero;
        visualRoot.gameObject.SetActive(true);
        _hideTween = visualRoot
            .DOScale(_originLocalScale, hideDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(StartLoop);
    }

    private void KillTweens()
    {
        _loopTween?.Kill();
        _hideTween?.Kill();
    }
}
