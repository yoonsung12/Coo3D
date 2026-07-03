using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 먹으면 누적 점수와 자판기용 코인을 함께 지급하는 아이템이다.
public class CoinItem : BasePickupItem
{
    [Title("보상")]
    [SerializeField, LabelText("지급 점수")]
    private int scoreAmount = 10;

    [SerializeField, LabelText("지급 코인")]
    private int coinAmount = 1;

    [Title("회전 연출")]
    [SerializeField, LabelText("한 바퀴 도는 시간")]
    private float spinDuration = 1.5f;
    // 코인이 Y축으로 계속 제자리 회전하면서 입체감 있게 보이도록 만든다.
    // 값이 작을수록 더 빠르게 돈다.

    private Tween _spinTween;

    // base.Start()를 반드시 호출해야 BasePickupItem의 스폰 팝인 연출/자동 소멸 타이머가 정상 동작한다.
    // override 없이 그냥 Start()만 새로 선언하면 Unity가 부모 쪽 Start()를 아예 호출하지 않아
    // 코인이 크기 0에서 벗어나지 못해 화면에 보이지 않는 문제가 생긴다.
    protected override void Start()
    {
        base.Start();

        // DOTween으로 Y축 기준 360도 회전을 무한 반복시켜 코인이 빙글빙글 도는 느낌을 낸다.
        _spinTween = transform
            .DORotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _spinTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    protected override void ApplyEffect()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreAmount);

        if (CoinManager.Instance != null)
            CoinManager.Instance.AddCoin(coinAmount);
    }
}
