using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 도화선 에스코트 퍼즐의 도화선이다. 여러 세그먼트를 잇던 예전 방식(FuseSegment/FuseChain) 대신
// 하나의 LineRenderer 경로로 구현한다. 시작점(waypoints[0])에서 성냥(TorchTool)으로 점화하면
// 불이 붙은 지점(_burnedDistance)이 끝을 향해 이동하며 지나온 구간이 사라지듯 줄어든다.
// 은행 열매(GinkgoFruit)가 불이 있는 지점 근처에 떨어지면 그 자리에서 멈추고, 일정 시간 뒤
// 원래 길이(안 탄 상태)로 완전히 복구된다 — 복구 후에는 자동으로 다시 타지 않고 처음부터
// 다시 점화해야 한다.
[RequireComponent(typeof(LineRenderer), typeof(Collider))]
public class FuseLine : MonoBehaviour, IIgnitable
{
    public enum FuseState { Unlit, Burning, Extinguished, Regenerating, Burned }

    [Title("경로 설정")]
    [SerializeField, LabelText("웨이포인트")]
    private List<Transform> waypoints = new List<Transform>();
    // 도화선이 지나가는 경로다. 시작점(첫 번째, 성냥으로 붙이는 지점)부터 끝(문 쪽)까지 순서대로 연결한다.
    // 최소 2개 필요하며, 발판/지형의 높낮이에 맞춰 자유롭게 배치할 수 있다.

    [Title("연소 설정")]
    [SerializeField, LabelText("타는 속도(m/s)"), PropertyRange(0.1f, 10f)]
    private float burnSpeed = 2f;
    // 웨이포인트 개수가 아니라 거리 기준이라, 구간 간격이 달라져도 체감 속도가 일정하게 유지된다.
    // Inspector 슬라이더로 조절하며, 값이 클수록 도화선이 더 빨리 타들어간다.

    [SerializeField, LabelText("소화 감지 반경")]
    private float extinguishCheckRadius = 0.4f;
    // 현재 불이 붙어 있는 지점을 기준으로 이 반경 안에 은행이 들어오면 소화된다.

    [SerializeField, LabelText("은행 감지 레이어")]
    private LayerMask leafLayer;
    // Inspector에서 GinkgoFruit(은행) 오브젝트가 속한 레이어를 지정한다.

    [Title("복구 설정")]
    [SerializeField, LabelText("복구 대기시간(초)")]
    private float regenDelay = 1f;
    // 소화된 뒤 이 시간이 지나면 원래 길이로 되돌아가기 시작한다.

    [SerializeField, LabelText("복구 속도(m/s)"), PropertyRange(0.1f, 15f)]
    private float regenSpeed = 4f;
    // 타는 속도보다 빠르게 잡아야 "주루룩" 복구되는 연출 의도에 맞는다.

    [Title("도착 지점")]
    [SerializeField, LabelText("문 앞 덩굴")]
    private FlammableObject doorVine;
    // 도화선 끝까지 다 타면 이 덩굴에 불을 옮긴다.

    [SerializeField, LabelText("연결된 문")]
    private DoorController targetDoor;

    [Title("연출")]
    [SerializeField, LabelText("타는 스파클 파티클")]
    private ParticleSystem burnSparkParticle;
    // 현재 불이 있는 지점을 따라 매 프레임 위치가 갱신된다.

    [Title("사운드")]
    [SerializeField, LabelText("타는 소리(반복)")]
    private AudioClip burnLoopSound;
    // Inspector에서 타닥거리는 크래클 사운드 클립을 연결한다. 비워두면 소리 없이 동작한다.

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;
    // Inspector에서 이 오브젝트의 AudioSource 컴포넌트를 연결한다.

    [SerializeField, LabelText("소리 페이드 시간")]
    private float audioFadeDuration = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    public FuseState CurrentState { get; private set; } = FuseState.Unlit;

    private LineRenderer _lineRenderer;
    private readonly List<float> _cumulativeLengths = new List<float>();
    // 시작점(waypoints[0])부터 각 웨이포인트까지의 누적 거리다. 경로상의 한 지점을 구할 때 사용한다.
    private float _totalLength;
    private float _burnedDistance;
    // 시작점 기준으로 얼마나 타들어갔는지를 나타낸다. 0이면 안 탄 상태, _totalLength면 끝까지 다 탄 상태다.
    private Tween _burnTween;
    private Coroutine _regenCoroutine;
    private Tween _audioFadeTween;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();

        GetComponent<Collider>().isTrigger = true;
        // 성냥(TorchTool)의 SphereCast 감지용 트리거 콜라이더다. 이 오브젝트를 도화선 시작점
        // (waypoints[0]) 위치에 배치하고, 점화 감지 레이어(TorchTool의 igniteLayer)에 포함시켜야 한다.

        CalcCumulativeLengths();
        RefreshLineRenderer();

        if (doorVine != null && targetDoor != null)
            doorVine.OnBurnedOut += () => targetDoor.OpenDoor();
    }

    private void OnDestroy()
    {
        _burnTween?.Kill();
        _audioFadeTween?.Kill();
        if (_regenCoroutine != null)
            StopCoroutine(_regenCoroutine);
    }

    private void Update()
    {
        if (CurrentState != FuseState.Burning) return;

        CheckExtinguish();

        if (burnSparkParticle != null)
            burnSparkParticle.transform.position = GetPointAtDistance(_burnedDistance);
    }

    // 웨이포인트 사이 거리를 미리 계산해 누적 거리 배열로 저장한다. 경로상의 특정 지점(불이 붙은
    // 위치)을 구할 때마다 전체 경로를 다시 계산하지 않기 위함이다.
    private void CalcCumulativeLengths()
    {
        _cumulativeLengths.Clear();
        _cumulativeLengths.Add(0f);

        float total = 0f;
        for (int i = 1; i < waypoints.Count; i++)
        {
            total += Vector3.Distance(waypoints[i - 1].position, waypoints[i].position);
            _cumulativeLengths.Add(total);
        }

        _totalLength = total;
    }

    // TorchTool의 SphereCast에 감지되면 호출된다. 안 탄 상태에서만 반응한다.
    public void OnIgnited()
    {
        if (CurrentState != FuseState.Unlit) return;
        BeginBurn();
    }

    // 현재 _burnedDistance부터 끝까지 타들어가는 연출을 시작한다.
    private void BeginBurn()
    {
        CurrentState = FuseState.Burning;
        float duration = (_totalLength - _burnedDistance) / Mathf.Max(burnSpeed, 0.01f);

        if (burnSparkParticle != null) burnSparkParticle.Play();
        PlayBurnSound();

        _burnTween?.Kill();
        _burnTween = DOTween.To(() => _burnedDistance, x => _burnedDistance = x, _totalLength, duration)
            .SetEase(Ease.Linear)
            .OnUpdate(RefreshLineRenderer)
            .OnComplete(OnFullyBurned);
    }

    private void OnFullyBurned()
    {
        CurrentState = FuseState.Burned;
        if (burnSparkParticle != null) burnSparkParticle.Stop();
        StopBurnSound();
        if (doorVine != null) doorVine.OnIgnited();
    }

    // 타는 동안 반복 재생할 크래클 사운드를 페이드인하며 시작한다.
    private void PlayBurnSound()
    {
        if (audioSource == null || burnLoopSound == null) return;

        _audioFadeTween?.Kill();

        if (!audioSource.isPlaying || audioSource.clip != burnLoopSound)
        {
            audioSource.clip = burnLoopSound;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.Play();
        }

        _audioFadeTween = audioSource.DOFade(1f, audioFadeDuration);
    }

    // 소화/완전 연소 시 사운드를 페이드아웃하며 정지한다.
    private void StopBurnSound()
    {
        if (audioSource == null) return;

        _audioFadeTween?.Kill();
        _audioFadeTween = audioSource.DOFade(0f, audioFadeDuration)
            .OnComplete(() =>
            {
                if (audioSource != null)
                    audioSource.Stop();
            });
    }

    // 현재 불이 붙은 지점 근처에 은행(GinkgoFruit)이 있는지 검사한다.
    private void CheckExtinguish()
    {
        Vector3 burnPoint = GetPointAtDistance(_burnedDistance);
        Collider[] hits = Physics.OverlapSphere(burnPoint, extinguishCheckRadius, leafLayer);

        foreach (Collider hit in hits)
        {
            GinkgoFruit leaf = hit.GetComponent<GinkgoFruit>();
            if (leaf == null) continue;

            Extinguish();
            // 은행이 그 자리에 남아있으면 복구된 도화선이 같은 위치에서 또 부딪혀 무한히
            // 꺼지는 문제가 생기므로, 소화시킨 은행은 게이지/냄새 없이 바로 사라지게 한다.
            leaf.ExtinguishAndVanish();
            break;
        }
    }

    private void Extinguish()
    {
        _burnTween?.Kill();
        if (burnSparkParticle != null) burnSparkParticle.Stop();
        StopBurnSound();

        CurrentState = FuseState.Extinguished;
        _regenCoroutine = StartCoroutine(RegenerateAfterDelayRoutine());
    }

    private IEnumerator RegenerateAfterDelayRoutine()
    {
        yield return new WaitForSeconds(regenDelay);
        BeginRegenerate();
    }

    // 꺼진 지점에서 시작점 방향으로 되돌아가며 원래 길이로 복구한다. 복구가 끝나도 자동으로
    // 다시 타지 않고 Unlit 상태로 돌아가, 플레이어가 처음부터 다시 점화해야 한다.
    private void BeginRegenerate()
    {
        CurrentState = FuseState.Regenerating;
        float duration = _burnedDistance / Mathf.Max(regenSpeed, 0.01f);

        _burnTween?.Kill();
        _burnTween = DOTween.To(() => _burnedDistance, x => _burnedDistance = x, 0f, duration)
            .SetEase(Ease.Linear)
            .OnUpdate(RefreshLineRenderer)
            .OnComplete(() => CurrentState = FuseState.Unlit);
    }

    // 경로상에서 시작점부터 distance만큼 떨어진 지점의 월드 좌표를 구한다.
    private Vector3 GetPointAtDistance(float distance)
    {
        if (waypoints.Count == 0) return transform.position;

        distance = Mathf.Clamp(distance, 0f, _totalLength);

        for (int i = 1; i < waypoints.Count; i++)
        {
            if (distance <= _cumulativeLengths[i])
            {
                float segStart = _cumulativeLengths[i - 1];
                float segLength = _cumulativeLengths[i] - segStart;
                float t = segLength > 0f ? (distance - segStart) / segLength : 0f;
                return Vector3.Lerp(waypoints[i - 1].position, waypoints[i].position, t);
            }
        }

        return waypoints[waypoints.Count - 1].position;
    }

    // 현재 불이 붙은 지점부터 끝 웨이포인트까지만 선을 그린다. 시작점 쪽(이미 탄 구간)은
    // 자연스럽게 화면에서 사라진 것처럼 보인다.
    private void RefreshLineRenderer()
    {
        if (waypoints.Count == 0) return;

        List<Vector3> points = new List<Vector3> { GetPointAtDistance(_burnedDistance) };
        for (int i = 1; i < waypoints.Count; i++)
        {
            if (_cumulativeLengths[i] > _burnedDistance)
                points.Add(waypoints[i].position);
        }

        _lineRenderer.positionCount = points.Count;
        _lineRenderer.SetPositions(points.ToArray());
    }

    [Button("성냥 테스트 점화")]
    private void TestIgnite() => OnIgnited();

    [Button("강제 소화 테스트")]
    private void TestExtinguish()
    {
        if (CurrentState == FuseState.Burning)
            Extinguish();
    }
}
