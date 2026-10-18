using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 지상 열선 가로등이다. 터빈이 작동하면(TurbineGenerator.onActivated) PowerOn()이 호출되어
// 먼저 가로등이 붉게 달아오르고, 이후 연결된 두꺼운 얼음벽에 열을 계속 가해 녹인다.
// 두꺼운 얼음벽은 횃불로 녹일 수 없는 레이어에 두므로, 이 가로등이 유일한 해법이다.
public class HeatLampPost : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("녹일 얼음벽")]
    private IceBlock targetIce;
    // 가열 대상이 되는 두꺼운 얼음벽의 IceBlock을 연결한다.

    [SerializeField, LabelText("달아오를 렌더러들")]
    private Renderer[] glowRenderers;
    // 전원이 켜질 때 붉게 빛날 기둥/램프 헤드의 Renderer를 연결한다.

    [Title("달아오름 연출 (DOTween)")]
    [SerializeField, LabelText("달아오른 색")]
    private Color hotColor = new Color(1f, 0.35f, 0.1f);

    [SerializeField, LabelText("발광 세기")]
    private float emissionIntensity = 3f;
    // 값이 클수록 더 밝게 빛난다.

    [SerializeField, LabelText("달아오르는 시간")]
    private float glowDuration = 1.5f;

    [SerializeField, LabelText("달아오름 Ease")]
    private Ease glowEase = Ease.InOutQuad;

    [Title("가열 설정")]
    [SerializeField, LabelText("초당 열량")]
    private float heatPerSecond = 10f;
    // 얼음 maxHeat이 30이면 기본값 10으로 약 3초 가열하면 녹는다.

    [FoldoutGroup("얼음 녹는 카메라 연출")]
    [InfoBox("카메라를 연결하면 전원이 켜질 때 화면을 페이드로 넘겨 얼음벽을 비추고, 다 녹으면 다시 플레이어에게 돌아간다.\n비워두면 기존처럼 연출 없이 녹기만 한다.")]
    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;
    // 씬의 Main Camera에 붙은 SideViewCamera를 연결한다.

    [FoldoutGroup("얼음 녹는 카메라 연출")]
    [SerializeField, LabelText("카메라가 비출 지점")]
    private Transform focusPoint;
    // 가로등과 얼음벽이 함께 보이는 위치에 빈 오브젝트를 두고 연결한다. 비워두면 얼음벽 위치를 비춘다.

    [FoldoutGroup("얼음 녹는 카메라 연출")]
    [SerializeField, LabelText("조작 잠글 플레이어")]
    private PlayerController player;
    // 연출 동안 enabled를 꺼서 조작을 잠근다(EntrancePortal, Drawbridge와 같은 방식). 비워두면 잠그지 않는다.

    [FoldoutGroup("얼음 녹는 카메라 연출")]
    [SerializeField, LabelText("시작 지연(초)")]
    private float cutsceneStartDelay = 1f;
    // 터빈이 돌기 시작하는 모습을 이 시간만큼 먼저 보여준 뒤 화면을 넘긴다.

    [FoldoutGroup("얼음 녹는 카메라 연출")]
    [SerializeField, LabelText("페이드 시간")]
    private float fadeDuration = 0.5f;
    // 화면이 어두워지거나 밝아지는 데 걸리는 시간이다. 지하에서 지상으로 멀리 넘어가므로 카메라를 날리지 않고 페이드로 끊는다.

    [FoldoutGroup("얼음 녹는 카메라 연출")]
    [SerializeField, LabelText("다 녹은 뒤 머무는 시간")]
    private float holdAfterMelted = 1.2f;
    // 얼음이 녹기 시작해 무너지는 연출(약 0.55초)까지 포함해, 결과를 보여 주고 플레이어에게 돌아가기 전까지 기다리는 시간이다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("전원 켜짐")]
    private bool _isPowered;

    [ReadOnly, ShowInInspector, LabelText("가열 중")]
    private bool _isHeating;

    [ReadOnly, ShowInInspector, LabelText("카메라 연출 중")]
    private bool _isCutscene;

    private bool _isEndingCutscene;
    // 다 녹은 뒤 복귀 연출을 한 번만 시작하기 위한 플래그다.

    private Sequence _glowSequence;
    // 달아오름 연출 전체를 담는 Sequence다. 끝나면 가열을 시작한다.

    private Sequence _cutsceneSequence;
    // 시작 지연, 카메라 이동 대기, 복귀 전 대기를 담는 Sequence다. 파괴 시 정리하기 위해 저장한다.

    private void OnDestroy()
    {
        _glowSequence?.Kill();
        _cutsceneSequence?.Kill();
    }

    // 터빈의 onActivated 이벤트에 연결해 호출한다. 한 번만 동작한다.
    public void PowerOn()
    {
        if (_isPowered) return;
        _isPowered = true;

        // 카메라가 연결돼 있으면 얼음벽을 비춘 뒤에 달아오르기 시작해, 달궈지는 과정부터 보여준다.
        if (sideViewCamera != null && targetIce != null && !targetIce.IsMelted)
            StartMeltCutscene();
        else
            StartGlow();
    }

    // 가로등이 붉게 달아오르고, 다 달아오르면 가열을 시작한다.
    private void StartGlow()
    {
        _glowSequence?.Kill();
        _glowSequence = DOTween.Sequence();

        foreach (Renderer r in glowRenderers)
        {
            if (r == null) continue;

            // renderer.material은 이 오브젝트 전용 인스턴스를 만들어 다른 오브젝트에 영향이 없다.
            Material mat = r.material;
            mat.EnableKeyword("_EMISSION");
            _glowSequence.Join(mat.DOColor(hotColor, "_BaseColor", glowDuration).SetEase(glowEase));
            _glowSequence.Join(mat.DOColor(hotColor * emissionIntensity, "_EmissionColor", glowDuration).SetEase(glowEase));
        }

        // 다 달아오른 뒤에야 열을 가하기 시작해 "달궈진 다음 녹인다"는 순서를 보여준다.
        _glowSequence.OnComplete(() => _isHeating = true);
    }

    private void Update()
    {
        if (!_isHeating) return;

        if (targetIce == null || targetIce.IsMelted)
        {
            _isHeating = false;

            // 다 녹았으면 무너지는 모습을 잠깐 보여 준 뒤 플레이어에게 돌아간다.
            if (_isCutscene)
                EndMeltCutscene();
            return;
        }

        // 프레임 속도와 무관하게 초당 일정한 열량을 주기 위해 Time.deltaTime을 곱한다.
        targetIce.OnMelted(heatPerSecond * Time.deltaTime);
    }

    // 조작을 잠그고, 시작 지연 뒤 화면을 페이드로 넘겨 얼음벽을 비춘다. 밝아지면 가로등이 달아오르기 시작한다.
    private void StartMeltCutscene()
    {
        _isCutscene = true;

        if (player != null)
            player.enabled = false;
        // 카메라가 얼음벽을 비추는 동안 플레이어가 화면 밖에서 움직이지 않게 잠근다.

        _cutsceneSequence?.Kill();
        _cutsceneSequence = DOTween.Sequence()
            .AppendInterval(cutsceneStartDelay)
            .AppendCallback(() => FadeOutThen(MoveCameraToIce));
    }

    // 화면이 완전히 어두워진 상태에서 카메라를 얼음벽 앞으로 옮기고, 다시 밝힌 뒤 달아오르기 시작한다.
    private void MoveCameraToIce()
    {
        Vector3 point = focusPoint != null ? focusPoint.position : targetIce.transform.position;
        sideViewCamera.StartFocus(point, 0.01f, Ease.Linear);

        // StartFocus의 Tween은 다음 프레임에 끝나므로 잠깐 기다린 뒤 SnapToTarget으로 카메라를 바로 옮긴다.
        // (기다리지 않으면 아직 플레이어 기준 위치로 옮겨져, 밝아진 뒤 카메라가 쓸려 가는 모습이 보인다.)
        _cutsceneSequence?.Kill();
        _cutsceneSequence = DOTween.Sequence()
            .AppendInterval(0.05f)
            .AppendCallback(() =>
            {
                sideViewCamera.SnapToTarget();
                FadeInThen(StartGlow);
            });
    }

    // 무너지는 모습을 보여 준 뒤 화면을 페이드로 넘겨 플레이어에게 돌아가고, 조작을 풀어 준다.
    private void EndMeltCutscene()
    {
        if (_isEndingCutscene) return;
        _isEndingCutscene = true;

        _cutsceneSequence?.Kill();
        _cutsceneSequence = DOTween.Sequence()
            .AppendInterval(holdAfterMelted)
            .AppendCallback(() => FadeOutThen(() =>
                sideViewCamera.EndFocus(0.01f, Ease.Linear, () =>
                {
                    sideViewCamera.SnapToTarget();
                    FadeInThen(RestorePlayerControl);
                })));
    }

    private void RestorePlayerControl()
    {
        if (player != null)
            player.enabled = true;
        _isCutscene = false;
    }

    // ScreenFader가 없는 씬에서도 연출이 멈추지 않도록, 없으면 바로 다음 단계로 넘어간다.
    private void FadeOutThen(System.Action next)
    {
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeOut(next, fadeDuration);
        else
            next();
    }

    private void FadeInThen(System.Action next)
    {
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeIn(next, fadeDuration);
        else
            next();
    }

    [Button("전원 켜기 테스트 (Play Mode)")]
    private void TestPowerOn()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HeatLampPost] Play Mode에서만 테스트할 수 있습니다."); return; }
        PowerOn();
    }
}
