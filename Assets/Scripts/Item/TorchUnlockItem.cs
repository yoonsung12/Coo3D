using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 퍼즐②(은행나무 사당) 받침대에 놓이는 "횃불" 아이템이다.
// 흡수/획득 흐름은 BasePickupItem을 그대로 쓰고, 먹으면 ToolManager의 횃불을 해금한 뒤 화면에 획득 안내를 띄운다.
// 받침대 위에서 계속 기다려야 하므로 Inspector의 "자동 소멸 시간"은 0(소멸 안 함)으로 둔다.
public class TorchUnlockItem : BasePickupItem
{
    [Title("횃불 해금")]
    [SerializeField, LabelText("Tool Manager")]
    private ToolManager toolManager;
    // Inspector에서 플레이어의 ToolManager 컴포넌트를 연결한다.

    [Title("대기 연출")]
    [SerializeField, LabelText("회전 주기(초)")]
    private float spinDuration = 3f;
    // 받침대 위에서 한 바퀴 도는 데 걸리는 시간이다. 위치는 건드리지 않아서 흡수 이동과 부딪히지 않는다.

    [Title("획득 안내 연출")]
    [SerializeField, LabelText("획득 안내 패널")]
    private CanvasGroup noticePanel;
    // Inspector에서 "횃불을 얻었다!" 문구가 담긴 UI 패널의 CanvasGroup을 연결한다. 비워두면 안내 없이 해금만 된다.

    [SerializeField, LabelText("안내 페이드 시간")]
    private float noticeFadeDuration = 0.3f;

    [SerializeField, LabelText("안내 표시 시간")]
    private float noticeHoldDuration = 2f;
    // 안내 문구가 완전히 보인 채 머무는 시간이다.

    [SerializeField, LabelText("안내 Ease")]
    private Ease noticeEase = Ease.OutQuad;

    private Tween _spinTween;

    protected override void Start()
    {
        base.Start();

        _spinTween = transform
            .DORotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
            .SetRelative()
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
        // 받침대 위에서 천천히 돌게 해 "주울 수 있는 물건"임을 알린다.
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _spinTween?.Kill();
    }

    protected override void ApplyEffect()
    {
        if (toolManager != null)
            toolManager.UnlockTorch();

        ShowNotice();
    }

    // 이 아이템은 획득 직후 파괴되므로, 안내 연출은 패널 쪽에 묶어(SetLink) 패널과 함께 정리되게 한다.
    private void ShowNotice()
    {
        if (noticePanel == null) return;

        noticePanel.gameObject.SetActive(true);
        noticePanel.alpha = 0f;

        DOTween.Sequence()
            .Append(noticePanel.DOFade(1f, noticeFadeDuration).SetEase(noticeEase))
            .AppendInterval(noticeHoldDuration)
            .Append(noticePanel.DOFade(0f, noticeFadeDuration).SetEase(noticeEase))
            .OnComplete(() => noticePanel.gameObject.SetActive(false))
            .SetLink(noticePanel.gameObject);
    }

    [Button("획득 안내 테스트")]
    private void TestNotice() => ShowNotice();
}
