using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 위에 서면 미끄러지는 얼음 발판이다.
// 실제 미끄러짐 계산은 PlayerController가 하고, 이 컴포넌트는 "얼음인지"와 미끄러짐 수치만 제공한다.
// IMeltable을 구현해 횃불(TorchTool)로 녹일 수 있고, 녹으면 발판은 남지만 미끄럽지 않게 되며
// 일정 시간이 지나면 추위에 다시 얼어 미끄러워진다.
[RequireComponent(typeof(Collider))]
[InfoBox("Is Trigger를 끈 콜라이더에 붙인다. 횃불로 녹이려면 이 오브젝트의 Layer가 TorchTool의 Melt Layer에 포함되어야 한다.")]
public class IcePlatform : MonoBehaviour, IMeltable
{
    [Title("미끄러짐 설정")]
    [SerializeField, LabelText("가속도")]
    private float acceleration = 6f;
    // 얼음 위에서 입력 방향으로 속도가 붙는 정도(초당 속도 증가량)다.
    // 작을수록 출발이 굼뜨고, 이동 속도(5)보다 충분히 크면 일반 바닥과 비슷해진다.

    [SerializeField, LabelText("감속도(마찰)")]
    private float deceleration = 3f;
    // 입력을 떼거나 반대로 누를 때 속도가 줄어드는 정도(초당 속도 감소량)다.
    // 작을수록 더 멀리 미끄러진다.

    [Title("녹이기 설정")]
    [SerializeField, LabelText("최대 열량")]
    private float maxHeat = 20f;
    // 이 값만큼 열을 누적해서 받으면 녹는다. 값이 클수록 오래 비춰야 녹는다.

    [SerializeField, LabelText("다시 어는 시간(초)")]
    private float refreezeDelay = 5f;
    // 녹은 뒤 이 시간이 지나면 다시 언다. 횃불을 계속 비추는 동안에는 시간이 처음부터 다시 센다.

    [Title("상태 표시 (옵션)")]
    [SerializeField, LabelText("녹은 상태 색상")]
    private Color meltedColor = new Color(0.45f, 0.6f, 0.8f);
    // 녹았을 때 발판 색을 이 색으로 바꿔 미끄럽지 않은 상태임을 알려준다. MeshRenderer가 없으면 무시된다.

    [SerializeField, LabelText("색 변화 시간")]
    private float colorChangeDuration = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 열량")]
    private float _currentHeat;

    [ReadOnly, ShowInInspector, LabelText("녹은 상태")]
    private bool _isMelted;

    public float Acceleration => acceleration;
    public float Deceleration => deceleration;

    // 녹지 않았을 때만 미끄럽다. PlayerController가 발밑 판정 시 확인한다.
    public bool IsSlippery => !_isMelted;

    private MeshRenderer _meshRenderer;
    private Color _originalColor;
    private Tween _colorTween;
    private Tween _refreezeTween;

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshRenderer != null)
            _originalColor = _meshRenderer.material.GetColor("_BaseColor");
        // material 접근 시 인스턴스 머티리얼이 생성되어 색 변화가 이 발판에만 적용된다.
    }

    private void OnDestroy()
    {
        _colorTween?.Kill();
        _refreezeTween?.Kill();
    }

    // TorchTool이 녹이기 반경 안에서 매 프레임 호출한다.
    public void OnMelted(float heatAmount)
    {
        if (_isMelted)
        {
            // 이미 녹은 상태에서 계속 비추면 다시 어는 타이머를 처음부터 센다.
            StartRefreezeTimer();
            return;
        }

        _currentHeat += heatAmount;

        if (_currentHeat >= maxHeat)
            Melt();
    }

    private void Melt()
    {
        _isMelted = true;
        TweenColor(meltedColor);
        StartRefreezeTimer();
    }

    private void Refreeze()
    {
        _isMelted = false;
        _currentHeat = 0f;
        TweenColor(_originalColor);
    }

    // DOVirtual.DelayedCall을 Tween 변수에 저장해, 다시 비추면 Kill 후 재시작할 수 있게 한다.
    private void StartRefreezeTimer()
    {
        _refreezeTween?.Kill();
        _refreezeTween = DOVirtual.DelayedCall(refreezeDelay, Refreeze);
    }

    private void TweenColor(Color targetColor)
    {
        if (_meshRenderer == null) return;

        _colorTween?.Kill();
        _colorTween = _meshRenderer.material.DOColor(targetColor, "_BaseColor", colorChangeDuration);
    }

    [Button("즉시 녹이기 테스트")]
    private void TestMelt() => Melt();

    [Button("즉시 얼리기 테스트")]
    private void TestRefreeze()
    {
        _refreezeTween?.Kill();
        Refreeze();
    }
}
