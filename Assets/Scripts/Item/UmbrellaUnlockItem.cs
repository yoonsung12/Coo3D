using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 여름 Coos99 앞의 "우산 문 비둘기"를 잡으면 튀어나오는 우산 아이템이다.
// 흡수/획득 흐름은 BasePickupItem을 그대로 쓰고, 먹으면 ToolManager의 우산을 해금한 뒤 화면에 획득 안내를 띄운다.
// 처치 후 줍기 전에 사라지면 우산을 영영 못 얻으므로, 프리팹의 "자동 소멸 시간"은 반드시 0(소멸 안 함)으로 둔다.
// 씬에 미리 놓이지 않고 UmbrellaCarrier가 생성하므로, 씬 참조(ToolManager, 안내 패널)는 Setup()으로 넘겨받는다.
public class UmbrellaUnlockItem : BasePickupItem
{
    [Title("대기 연출")]
    [SerializeField, LabelText("회전 주기(초)")]
    private float spinDuration = 3f;
    // 바닥 위에서 한 바퀴 도는 데 걸리는 시간이다. 위치는 건드리지 않아서 흡수 이동과 부딪히지 않는다.

    [Title("획득 안내 연출")]
    [SerializeField, LabelText("안내 페이드 시간")]
    private float noticeFadeDuration = 0.3f;

    [SerializeField, LabelText("안내 표시 시간")]
    private float noticeHoldDuration = 2f;
    // 안내 문구가 완전히 보인 채 머무는 시간이다.

    [SerializeField, LabelText("안내 Ease")]
    private Ease noticeEase = Ease.OutQuad;

    [Title("런타임 연결 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("Tool Manager")]
    private ToolManager _toolManager;

    [ReadOnly, ShowInInspector, LabelText("획득 안내 패널")]
    private CanvasGroup _noticePanel;

    private Tween _spinTween;

    // UmbrellaCarrier가 아이템을 만든 직후 호출해 씬에 있는 참조를 넘겨준다.
    public void Setup(ToolManager toolManager, CanvasGroup noticePanel)
    {
        _toolManager = toolManager;
        _noticePanel = noticePanel;
    }

    protected override void Start()
    {
        base.Start();

        _spinTween = transform
            .DORotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
            .SetRelative()
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
        // 바닥 위에서 천천히 돌게 해 "주울 수 있는 물건"임을 알린다.
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _spinTween?.Kill();
    }

    protected override void ApplyEffect()
    {
        // Setup이 빠졌더라도(테스트로 직접 배치 등) 플레이어에게서 ToolManager를 찾아 해금이 누락되지 않게 한다.
        if (_toolManager == null && PlayerHealth.Instance != null)
            _toolManager = PlayerHealth.Instance.GetComponent<ToolManager>();

        if (_toolManager != null)
            _toolManager.UnlockUmbrella();

        ShowNotice();
    }

    // 이 아이템은 획득 직후 파괴되므로, 안내 연출은 패널 쪽에 묶어(SetLink) 패널과 함께 정리되게 한다.
    private void ShowNotice()
    {
        if (_noticePanel == null) return;

        _noticePanel.gameObject.SetActive(true);
        _noticePanel.alpha = 0f;

        DOTween.Sequence()
            .Append(_noticePanel.DOFade(1f, noticeFadeDuration).SetEase(noticeEase))
            .AppendInterval(noticeHoldDuration)
            .Append(_noticePanel.DOFade(0f, noticeFadeDuration).SetEase(noticeEase))
            .OnComplete(() => _noticePanel.gameObject.SetActive(false))
            .SetLink(_noticePanel.gameObject);
    }
}
