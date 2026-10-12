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

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("전원 켜짐")]
    private bool _isPowered;

    [ReadOnly, ShowInInspector, LabelText("가열 중")]
    private bool _isHeating;

    private Sequence _glowSequence;
    // 달아오름 연출 전체를 담는 Sequence다. 끝나면 가열을 시작한다.

    private void OnDestroy()
    {
        _glowSequence?.Kill();
    }

    // 터빈의 onActivated 이벤트에 연결해 호출한다. 한 번만 동작한다.
    public void PowerOn()
    {
        if (_isPowered) return;
        _isPowered = true;

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
            return;
        }

        // 프레임 속도와 무관하게 초당 일정한 열량을 주기 위해 Time.deltaTime을 곱한다.
        targetIce.OnMelted(heatPerSecond * Time.deltaTime);
    }

    [Button("전원 켜기 테스트 (Play Mode)")]
    private void TestPowerOn()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HeatLampPost] Play Mode에서만 테스트할 수 있습니다."); return; }
        PowerOn();
    }
}
