using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 가을 패턴(거대해지는 나무 + 뿌리 에스코트)을 담당한다.
// Boss.OnPatternTriggered(Autumn)를 구독해 시작하고, 플레이어 기준 더 먼 쪽 나무
// (BossGinkgoTree)를 골라 성장시키며 동시에 하늘을 어둡게 만든다. 나무가 다 자라면
// 은행열매 낙하(GinkgoSpawner)와 반대편 뿌리(FuseLine) 점화가 활성화되고, 플레이어가
// 성냥으로 뿌리에 불을 붙여 끝까지 태우면(우산으로 낙하 은행열매를 막아가며) 보스에게
// 피해를 주고 패턴이 파훼된다. 제한시간은 두지 않는다 — 중간에 은행열매를 맞아 꺼져도
// FuseLine 자체의 소화→복구→재점화 구조로 자연스럽게 재도전하면 된다.
[RequireComponent(typeof(Boss))]
public class BossAutumnPattern : BossSeasonPatternBase
{
    [Title("연결")]
    [SerializeField, LabelText("왼쪽 은행나무")]
    private BossGinkgoTree treeLeft;

    [SerializeField, LabelText("오른쪽 은행나무")]
    private BossGinkgoTree treeRight;

    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;
    // Boss.cs의 playerTransform과 같은 오브젝트를 연결한다.
    // Boss.cs 쪽 참조는 private이라 이 컴포넌트가 직접 읽을 수 없어 별도로 하나 더 연결해둔다.

    [SerializeField, LabelText("하늘 조명(Directional Light)")]
    private Light skyLight;

    [Title("하늘 어두워짐 설정")]
    [SerializeField, LabelText("어두워지는 목표 밝기")]
    private float darkenedIntensity = 0.15f;

    [SerializeField, LabelText("어두워지기까지 걸리는 시간")]
    private float darkenDuration = 2f;
    // 나무 성장 시간과 체감이 맞도록 조절한다.

    [SerializeField, LabelText("어두워지기 시작 지연 시간")]
    private float darkenDelay = 0f;
    // 나무 성장 시작과 정확히 동시가 아니라 살짝 늦게(또는 빠르게) 어두워지게 하고 싶을 때 조절한다.

    [SerializeField, LabelText("복귀(밝아짐) 시간")]
    private float restoreDuration = 1.5f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("선택된 나무")]
    private string _activeTreeLabel = "-";

    private BossGinkgoTree _activeTree;
    private float _originalIntensity;
    private Tween _skyTween;

    protected override Boss.SeasonPattern Season => Boss.SeasonPattern.Autumn;
    protected override bool IsRunning => _activeTree != null;

    protected override void Awake()
    {
        base.Awake();
        if (skyLight != null) _originalIntensity = skyLight.intensity;
    }

    private void OnDestroy()
    {
        _skyTween?.Kill();
    }

    // 진행 중이던 나무/뿌리를 파훼 성공 없이 즉시 정리하고 원래 상태로 되돌린다.
    // 오브젝트 비활성화(OnDisable)와 다른 계절 패턴에 밀려날 때(HandlePatternTriggered) 둘 다 재사용한다.
    protected override void Abort()
    {
        UnsubscribeActiveRoot();
        RestoreSky();
        _activeTree.Deactivate();
        _activeTree = null;
        _activeTreeLabel = "-";
    }

    protected override void StartCycle()
    {
        StartPattern();
    }

    private void StartPattern()
    {
        _activeTree = ChooseFartherTree();
        _activeTreeLabel = _activeTree == treeLeft ? "Left" : "Right";

        DarkenSky();
        _activeTree.Activate(onGrown: () =>
        {
            if (_activeTree != null && _activeTree.RootFuse != null)
                _activeTree.RootFuse.OnFuseFullyBurned += HandleRootFullyBurned;
        });
    }

    // 플레이어와의 거리가 더 먼 쪽 나무를 고른다.
    private BossGinkgoTree ChooseFartherTree()
    {
        if (playerTransform == null) return treeLeft;

        float distToLeft = Mathf.Abs(playerTransform.position.x - treeLeft.transform.position.x);
        float distToRight = Mathf.Abs(playerTransform.position.x - treeRight.transform.position.x);
        return distToLeft >= distToRight ? treeLeft : treeRight;
    }

    private void DarkenSky()
    {
        if (skyLight == null) return;

        _skyTween?.Kill();
        _skyTween = DOTween.To(() => skyLight.intensity, x => skyLight.intensity = x, darkenedIntensity, darkenDuration)
            .SetDelay(darkenDelay)
            .SetEase(Ease.InOutSine);
    }

    private void RestoreSky()
    {
        if (skyLight == null) return;

        _skyTween?.Kill();
        _skyTween = DOTween.To(() => skyLight.intensity, x => skyLight.intensity = x, _originalIntensity, restoreDuration)
            .SetEase(Ease.InOutSine);
    }

    // 뿌리(FuseLine)가 끝까지 타면 호출된다. 패턴을 파훼 처리한다(데미지는 주지 않는다 —
    // 파훼 직후 Boss.cs의 무방비 시간 동안 일반 공격으로 때려야 실제 피해가 들어간다).
    private void HandleRootFullyBurned()
    {
        _boss.NotifyPatternSolved();

        Abort();
    }

    private void UnsubscribeActiveRoot()
    {
        if (_activeTree != null && _activeTree.RootFuse != null)
            _activeTree.RootFuse.OnFuseFullyBurned -= HandleRootFullyBurned;
    }

    [Button("가을 패턴 강제 발동 테스트")]
    private void TestTrigger() => HandlePatternTriggered(Boss.SeasonPattern.Autumn);
}
