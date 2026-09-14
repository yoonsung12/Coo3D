using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 환풍기 입구. 선풍기 바람을 감지하면 연결된 환풍기 출구(VentOut)를 전부 활성화한다.
// IBlowable을 구현해 FanTool의 BoxCast/OverlapBox에 자동으로 감지된다.
// Rigidbody가 없으므로 실제로 밀리지 않고 감지 신호만 받는다.
[RequireComponent(typeof(Collider))]
public class VentIn : MonoBehaviour, IBlowable
{
    [Title("연결 설정")]
    [InfoBox("선풍기 바람이 감지되면 아래 VentOut 목록이 모두 활성화됩니다.")]
    [SerializeField, LabelText("연결된 환풍기 출구 목록")]
    private VentOut[] connectedVentOuts;
    // Inspector에서 이 환풍기 입구에 연결할 VentOut 컴포넌트를 모두 등록한다.

    [Title("바람 감지 설정")]
    [SerializeField, LabelText("바람 감지 유지 시간 (초)")]
    private float windTimeout = 0.1f;
    // 마지막으로 OnBlown()이 호출된 물리 시각 기준으로 이 시간이 지나면 비활성화한다.
    // FanTool은 FixedUpdate(0.02초)마다 호출하므로 0.1초(5틱)면 안정적이다.

    [Title("색상 설정")]
    [SerializeField, LabelText("환풍기 Renderer")]
    private Renderer ventRenderer;
    // Inspector에서 색상을 변경할 MeshRenderer를 연결한다.

    [SerializeField, LabelText("비활성 색상")]
    private Color inactiveColor = Color.gray;

    [SerializeField, LabelText("활성 색상")]
    private Color activeColor = Color.cyan;

    [SerializeField, LabelText("색상 전환 시간")]
    private float colorTweenDuration = 0.15f;

    [Title("런타임 상태")]
    [ShowInInspector, ReadOnly, LabelText("현재 활성 여부")]
    private bool isActive = false;

    [ShowInInspector, ReadOnly, LabelText("마지막 바람 감지 물리 시각")]
    private float _lastBlownFixedTime = -999f;
    // Time.fixedTime 기준으로 마지막으로 OnBlown()이 호출된 시각을 기록한다.
    // -999f로 초기화해 게임 시작 시 즉시 비활성 상태로 시작한다.

    private Material _ventMat;
    private Tween _colorTween;

    private void Awake()
    {
        // 개별 머티리얼 인스턴스를 생성해 환풍기마다 색상을 독립적으로 관리한다.
        if (ventRenderer != null)
            _ventMat = ventRenderer.material;
    }

    private void Start()
    {
        SetColorImmediate(inactiveColor);
    }

    private void OnDestroy()
    {
        _colorTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    // FanTool과 동일한 물리 주기(FixedUpdate)에서 비활성화를 체크한다.
    // Update()를 쓰면 렌더 프레임 주기와 물리 주기가 달라 타이밍이 불안정해진다.
    private void FixedUpdate()
    {
        if (!isActive) return;

        // 마지막으로 바람을 받은 물리 시각 기준으로 windTimeout이 지나면 비활성화한다.
        if (Time.fixedTime - _lastBlownFixedTime > windTimeout)
            Deactivate();
    }

    // FanTool의 BoxCast/OverlapBox에 감지되면 호출된다.
    // 마지막 감지 물리 시각을 갱신하고, 처음 감지되면 연결된 VentOut을 활성화한다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        // 바람이 닿은 물리 시각을 기록한다. FixedUpdate에서 판정하므로 Time.fixedTime이 정확한 기준이 된다.
        _lastBlownFixedTime = Time.fixedTime;

        if (!isActive)
            Activate();
    }

    // 바람이 감지되기 시작할 때 호출된다. 연결된 모든 VentOut을 활성화한다.
    private void Activate()
    {
        isActive = true;
        TweenColor(activeColor);

        // Unity 오브젝트에는 ?. 대신 명시적 null 체크를 쓴다(빈 참조에서 ?.가 실제로 메서드
        // 호출을 건너뛰지 않고 예외를 던지는 경우가 있었다 — VentOut.cs 참고).
        foreach (VentOut vent in connectedVentOuts)
        {
            if (vent != null)
                vent.Activate();
        }
    }

    // 바람이 끊겼을 때 호출된다. 연결된 모든 VentOut에 비활성화 신호를 보낸다.
    // VentOut이 Latch 모드라면 VentOut 내부에서 비활성화를 무시한다.
    private void Deactivate()
    {
        isActive = false;
        TweenColor(inactiveColor);

        foreach (VentOut vent in connectedVentOuts)
        {
            if (vent != null)
                vent.Deactivate();
        }
    }

    private void SetColorImmediate(Color color)
    {
        if (_ventMat != null)
            _ventMat.SetColor("_BaseColor", color);
    }

    private void TweenColor(Color targetColor)
    {
        if (_ventMat == null) return;

        _colorTween?.Kill();
        // URP 셰이더의 _BaseColor 프로퍼티를 지정해 색상을 부드럽게 전환한다.
        _colorTween = DOTween.To(
            () => _ventMat.GetColor("_BaseColor"),
            x => _ventMat.SetColor("_BaseColor", x),
            targetColor,
            colorTweenDuration
        );
    }

    [Button("테스트: 활성화 (Play Mode)")]
    private void TestActivate()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[VentIn] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        _lastBlownFixedTime = Time.fixedTime;
        Activate();
    }

    [Button("테스트: 비활성화 (Play Mode)")]
    private void TestDeactivate()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[VentIn] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        Deactivate();
    }

    private void OnDrawGizmosSelected()
    {
        // 씬 뷰에서 활성 상태를 색으로 확인한다.
        Gizmos.color = isActive ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
