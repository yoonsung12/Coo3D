using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 가을 패턴에서 자라나는 은행나무다. 평소엔 작은 크기로 숨어있다가 패턴이 발동하면
// 화면 밖으로 나갈 만큼 거대하게 자라나며, 다 자란 뒤에는 은행열매 낙하(GinkgoSpawner)와
// 반대편 뿌리(FuseLine) 점화를 함께 활성화한다. 실제 사이클 진행(어느 나무를 고를지, 파훼 판정 등)은
// BossAutumnPattern이 담당하고, 이 클래스는 나무 하나의 성장 연출+활성화/비활성화만 책임진다.
public class BossGinkgoTree : MonoBehaviour
{
    [Title("성장 연출 설정")]
    [SerializeField, LabelText("성장 후 크기")]
    private Vector3 grownScale = new Vector3(6f, 6f, 6f);
    // 평소엔 작게 숨어있다가 이 크기까지 커진다. 화면 밖으로 나가는 느낌을 주려면 충분히 크게 잡는다.

    [SerializeField, LabelText("성장/복귀 시간")]
    private float growDuration = 2f;

    [SerializeField, LabelText("성장 Ease")]
    private Ease growEase = Ease.OutSine;
    // OutBack은 목표 크기를 넘었다가 튕겨 돌아오는 효과라 "뿅" 튀어나오는 것처럼 보여서
    // 오버슈트 없이 매끄럽게 커지는 Ease로 변경했다.

    [Title("연결")]
    [SerializeField, LabelText("은행열매 스포너")]
    private GinkgoSpawner ginkgoSpawner;
    // Inspector에서 이 나무 전용 GinkgoSpawner를 연결한다. 평소엔 비활성화해두고,
    // 나무가 다 자란 뒤에만 활성화한다.

    [SerializeField, LabelText("반대편 뿌리(FuseLine)")]
    private FuseLine rootFuse;
    // Inspector에서 이 나무의 반대편 끝에서부터 이어지는 FuseLine을 연결한다. 평소엔
    // 비활성화해두고(점화 불가), 나무가 다 자란 뒤에만 활성화한다.

    private Vector3 _hiddenScale;
    private float _hiddenPositionY;
    private float _heightPerScale;
    // "숨은 상태"의 위치가 이미 밑동이 바닥에 붙어있는 상태라고 보고, 스케일 1당 필요한
    // Y 위치를 역산해둔 값이다. 성장/복귀 내내 이 비율로 위치를 같이 움직여야 나무 밑동이
    // 바닥에 고정된 채 위쪽으로만 커지는 것처럼 보인다(스케일만 키우면 중심 기준으로
    // 위아래 동시에 부풀어 올라 "그 자리에서 뿅 나타나는" 느낌이 났다).
    private Tween _growTween;
    private Tween _moveTween;

    // BossAutumnPattern이 이 나무의 뿌리 FuseLine을 구독/판정할 수 있도록 공개한다.
    public FuseLine RootFuse => rootFuse;

    private void Awake()
    {
        _hiddenScale = transform.localScale;
        _hiddenPositionY = transform.position.y;
        _heightPerScale = _hiddenScale.y > 0f ? _hiddenPositionY / _hiddenScale.y : 0f;

        if (ginkgoSpawner != null) ginkgoSpawner.gameObject.SetActive(false);
        if (rootFuse != null) rootFuse.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _growTween?.Kill();
        _moveTween?.Kill();
    }

    // 성장 연출을 시작하고, 끝나면 은행열매 낙하+뿌리 점화를 활성화한다.
    public void Activate(Action onGrown = null)
    {
        _growTween?.Kill();
        _moveTween?.Kill();

        float targetY = _heightPerScale * grownScale.y;

        _growTween = transform.DOScale(grownScale, growDuration).SetEase(growEase);
        _moveTween = transform.DOMoveY(targetY, growDuration)
            .SetEase(growEase)
            .OnComplete(() =>
            {
                if (ginkgoSpawner != null) ginkgoSpawner.gameObject.SetActive(true);

                if (rootFuse != null)
                {
                    // 활성화하는 즉시 FuseLine.Awake()가 전체 길이를 한 번에 그려버리므로,
                    // 같은 프레임 안에서 바로 자라나는 연출을 시작해 화면에는 그 결과만 보이게 한다.
                    rootFuse.gameObject.SetActive(true);

                    // 뿌리에 붙은 잔가지(RootBranchVisual)들도 본선이 그 지점까지 자라난 순간에
                    // 맞춰 함께 나타나도록, 매 프레임 진행률을 그대로 전달해준다.
                    RootBranchVisual[] branches = rootFuse.GetComponentsInChildren<RootBranchVisual>(true);
                    rootFuse.PlayGrowInEffect(onProgress: burnedDistance =>
                    {
                        foreach (RootBranchVisual branch in branches)
                            branch.ApplyGrowProgress(burnedDistance);
                    });
                }

                onGrown?.Invoke();
            });
    }

    // 패턴이 끝나면 은행열매 낙하+뿌리를 비활성화하고 원래 크기/위치로 되돌아간다.
    public void Deactivate()
    {
        if (ginkgoSpawner != null) ginkgoSpawner.gameObject.SetActive(false);
        if (rootFuse != null) rootFuse.gameObject.SetActive(false);

        _growTween?.Kill();
        _moveTween?.Kill();

        _growTween = transform.DOScale(_hiddenScale, growDuration).SetEase(Ease.InSine);
        _moveTween = transform.DOMoveY(_hiddenPositionY, growDuration).SetEase(Ease.InSine);
    }

    [Button("성장 테스트")]
    private void TestGrow() => Activate();

    [Button("복귀 테스트")]
    private void TestShrink() => Deactivate();
}
