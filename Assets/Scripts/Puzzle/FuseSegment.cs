using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 도화선 에스코트 퍼즐에서 도화선을 이루는 개별 세그먼트다. 성냥(TorchTool)으로 직접
// 점화되는 것은 체인의 첫 세그먼트뿐이고, 나머지는 FuseChain이 순서/속도를 계산해
// BeginBurn()으로 점화시킨다. 은행 열매(LeafDrop)가 타는 중인 세그먼트에 떨어지면 불이
// 꺼지고, FuseChain이 일정 시간 뒤 남은 구간부터 다시 타도록 재점화한다.
[RequireComponent(typeof(Collider))]
public class FuseSegment : MonoBehaviour, IIgnitable
{
    public enum FuseState { Unlit, Burning, Extinguished, Burned }

    [Title("연출")]
    [SerializeField, LabelText("타는 스파클 파티클")]
    private ParticleSystem burnSparkParticle;

    [SerializeField, LabelText("축소 Ease")]
    private Ease shrinkEase = Ease.Linear;
    // 세그먼트 메시의 진행 방향이 로컬 X축이라고 가정하고 X 스케일만 줄인다.
    // 메시 방향이 다르면 이 값과 별개로 DOScaleX 대상 축을 실제 메시에 맞게 조정해야 한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    public FuseState CurrentState { get; private set; } = FuseState.Unlit;

    // 다 타서 다음 세그먼트로 넘어갈 시점을 알리기 위한 소속 체인 참조다.
    // FuseChain.Awake()에서 Init()을 통해 자동으로 연결되므로 Inspector에서 직접 연결할 필요는 없다.
    private FuseChain _chain;

    private float _totalDuration;
    private float _burnStartTime;
    private Tween _shrinkTween;

    // 소화 시점까지 덜 탄 만큼 남은 연소 시간이다. FuseChain이 재점화할 때 이 값을 그대로 사용한다.
    public float RemainingDuration { get; private set; }

    private void Awake()
    {
        // 바닥에 깔린 도화선 장식이라 플레이어 이동을 막지 않아야 하고, 은행 열매(LeafDrop)와의
        // 접촉도 트리거로 판정해야 하므로 콜라이더를 트리거로 강제한다.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnDestroy()
    {
        _shrinkTween?.Kill();
    }

    // FuseChain이 씬 시작 시 각 세그먼트에 자기 자신을 등록한다.
    public void Init(FuseChain chain)
    {
        _chain = chain;
    }

    // 성냥(TorchTool)이 체인의 첫 세그먼트를 직접 점화할 때만 호출된다.
    // 실제 연소 시작/시간 계산은 FuseChain에 위임한다.
    public void OnIgnited()
    {
        if (CurrentState != FuseState.Unlit) return;
        _chain?.NotifyIgnited(this);
    }

    // FuseChain이 계산한 시간(duration) 동안 진행 방향(X축)으로 스케일을 줄이며 타들어간다.
    // 처음 점화든 소화 후 재점화든 이 메서드 하나로 처리한다 — 현재 크기에서 0까지 duration만큼 줄인다.
    public void BeginBurn(float duration)
    {
        CurrentState = FuseState.Burning;
        _totalDuration = duration;
        _burnStartTime = Time.time;

        if (burnSparkParticle != null) burnSparkParticle.Play();

        _shrinkTween?.Kill();
        _shrinkTween = transform
            .DOScaleX(0f, duration)
            .SetEase(shrinkEase)
            .OnComplete(() =>
            {
                // 다 타서 완전히 소진된 상태다. Burning으로 남아있으면 이미 사라진 세그먼트에
                // 은행 열매가 떨어졌을 때 다시 Extinguish()가 반응해버리는 문제가 생기므로
                // 별도의 최종 상태(Burned)로 고정해 더 이상 반응하지 않게 한다.
                CurrentState = FuseState.Burned;
                if (burnSparkParticle != null) burnSparkParticle.Stop();
                _chain?.NotifyBurnedOut(this);
            });
    }

    // 은행 열매가 타는 중인 세그먼트에 닿으면 호출된다. 진행 중이던 축소를 멈추고
    // 남은 연소 시간을 계산해둔 뒤 체인에 소화 사실을 알린다.
    public void Extinguish()
    {
        if (CurrentState != FuseState.Burning) return;

        float elapsed = Time.time - _burnStartTime;
        RemainingDuration = Mathf.Max(0f, _totalDuration - elapsed);

        _shrinkTween?.Kill();
        if (burnSparkParticle != null) burnSparkParticle.Stop();

        CurrentState = FuseState.Extinguished;
        _chain?.NotifyExtinguished(this);
    }

    // 은행 열매(LeafDrop)와의 접촉을 감지한다. 타는 중일 때만 반응한다.
    private void OnTriggerEnter(Collider other)
    {
        if (CurrentState != FuseState.Burning) return;
        if (other.GetComponent<LeafDrop>() == null) return;

        Extinguish();
    }

    [Button("이 세그먼트만 점화 테스트")]
    private void TestIgnite() => OnIgnited();
}
