using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 지하 상자에 놓여 있는 불꽃놀이 묶음이다. 플레이어가 다가가 줍으면 FireworkBag에 개수가 더해진다.
// 흡수 이동/스폰 팝인은 BasePickupItem이 처리한다. BasePickupItem은 시작할 때 스케일을 0에서 1로
// 키우므로, 이 오브젝트의 루트 스케일은 1이어야 하고 모양은 자식 오브젝트에 둬야 한다.
// 씬에 배치할 때는 부모 클래스의 "자동 소멸 시간"을 0으로 두어 사라지지 않게 한다.
public class FireworkItem : BasePickupItem
{
    [Title("불꽃놀이 설정")]
    [SerializeField, LabelText("지급 개수")]
    private int amount = 3;
    // 한 번 주울 때 가방에 들어가는 불꽃놀이 수다. 발사대가 3곳이라 기본 3개다.

    [Title("획득 안내 연출")]
    [SerializeField, LabelText("획득 안내 패널")]
    private CanvasGroup noticePanel;
    // Inspector에서 "불꽃놀이를 얻었다!"와 사용법 문구가 담긴 UI 패널의 CanvasGroup을 연결한다.
    // 플레이어가 어디에 쓰는지 모르는 문제를 막기 위한 안내이며, 비워두면 안내 없이 개수만 더해진다.

    [SerializeField, LabelText("안내 페이드 시간")]
    private float noticeFadeDuration = 0.3f;

    [SerializeField, LabelText("안내 표시 시간")]
    private float noticeHoldDuration = 3f;
    // 안내 문구가 완전히 보인 채 머무는 시간이다. 사용법 문장이 길어서 횃불(2초)보다 길게 둔다.

    [SerializeField, LabelText("안내 Ease")]
    private Ease noticeEase = Ease.OutQuad;

    protected override void ApplyEffect()
    {
        if (FireworkBag.Instance != null)
            FireworkBag.Instance.Add(amount);

        ShowNotice();
    }

    // 이 아이템은 획득 직후 파괴되므로, 안내 연출은 패널 쪽에 묶어(SetLink) 패널과 함께 정리되게 한다.
    // TorchUnlockItem의 안내 연출과 같은 흐름(페이드 인 → 머무름 → 페이드 아웃)이다.
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
