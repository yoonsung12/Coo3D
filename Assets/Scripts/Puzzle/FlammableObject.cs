using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 횃불이 닿으면 불이 붙고, 일정 시간 타다가 스스로 꺼지며, 타는 동안 주변 인화성
// 오브젝트로 불이 번지는 오브젝트다. IIgnitable을 구현해 TorchTool의 점화 판정을 받는다.
// 다 타면 Ash(재) 상태로 영구 고정되며 다시 불이 붙지 않는다.
[RequireComponent(typeof(Collider))]
public class FlammableObject : MonoBehaviour, IIgnitable
{
    public enum FireState { Unlit, Burning, Ash }

    [Title("점화/연출 설정")]
    [SerializeField, LabelText("타는 시간")]
    private float burnDuration = 4f;
    // 불이 붙은 뒤 재가 되기까지 걸리는 시간이다. 값이 클수록 오래 탄다.

    [SerializeField, LabelText("불 파티클")]
    private ParticleSystem fireParticle;
    // Inspector에서 불꽃 ParticleSystem을 연결한다. 비워두면 파티클 없이 상태만 전환된다.

    [SerializeField, LabelText("연기 파티클")]
    private ParticleSystem smokeParticle;
    // Inspector에서 연기 ParticleSystem을 연결한다.

    [SerializeField, LabelText("재 색상")]
    private Color ashColor = new Color(0.25f, 0.25f, 0.25f);
    // 다 타서 재가 됐을 때 Material이 바뀌는 색상이다.

    [SerializeField, LabelText("색상 전환 시간")]
    private float colorChangeDuration = 0.6f;

    [Title("확산 설정")]
    [SerializeField, LabelText("확산 반경")]
    private float spreadRadius = 2.5f;
    // 이 반경 안에 있는 다른 인화성 오브젝트로 불이 번진다.

    [SerializeField, LabelText("확산 지연 시간")]
    private float spreadDelay = 1f;
    // 불이 붙은 뒤 이 시간이 지나면 주변으로 불이 번지기 시작한다.

    [SerializeField, LabelText("확산 감지 레이어")]
    private LayerMask spreadLayer;
    // Inspector에서 다른 인화성 오브젝트들의 레이어를 지정한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    public FireState CurrentState { get; private set; } = FireState.Unlit;

    private Collider _collider;
    private MeshRenderer _meshRenderer;
    private Tween _colorTween;
    private Coroutine _burnCoroutine;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 마른 덤불/낙엽 더미 같은 소품이라 플레이어 이동을 막지 않고 통과할 수 있어야 하므로
        // 트리거로 강제한다. 통로를 막는 장애물로 쓰려면 이 값을 false로 바꿔야 한다.
        _collider.isTrigger = true;

        _meshRenderer = GetComponent<MeshRenderer>();
        // material 접근 시 인스턴스 머티리얼이 생성되어 이 오브젝트만의 색상을 독립적으로 바꿀 수 있다.
    }

    private void OnDestroy()
    {
        _colorTween?.Kill();
        if (_burnCoroutine != null)
            StopCoroutine(_burnCoroutine);
    }

    // TorchTool의 Ignite()에서 SphereCast로 감지되면 호출된다.
    public void OnIgnited()
    {
        // 이미 타고 있거나 다 타서 재가 된 상태면 무시한다.
        if (CurrentState != FireState.Unlit) return;

        CurrentState = FireState.Burning;
        SetParticles(true);
        _burnCoroutine = StartCoroutine(BurnRoutine());
    }

    private IEnumerator BurnRoutine()
    {
        // 확산 지연 시간이 타는 시간보다 길면 확산 없이 바로 재가 되어버리는 문제를 막기 위해 제한한다.
        float delay = Mathf.Min(spreadDelay, burnDuration);
        yield return new WaitForSeconds(delay);

        SpreadFire();

        yield return new WaitForSeconds(burnDuration - delay);

        BecomeAsh();
    }

    // 확산 반경 안의 다른 인화성 오브젝트에 불을 옮긴다.
    private void SpreadFire()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, spreadRadius, spreadLayer);

        foreach (Collider col in cols)
        {
            if (col.gameObject == gameObject) continue;

            IIgnitable ignitable = col.GetComponent<IIgnitable>();
            ignitable?.OnIgnited();
        }
    }

    private void BecomeAsh()
    {
        CurrentState = FireState.Ash;
        SetParticles(false);

        if (_meshRenderer == null) return;

        _colorTween?.Kill();
        _colorTween = _meshRenderer.material.DOColor(ashColor, "_BaseColor", colorChangeDuration);
    }

    private void SetParticles(bool playing)
    {
        if (fireParticle != null)
        {
            if (playing) fireParticle.Play();
            else fireParticle.Stop();
        }

        if (smokeParticle != null)
        {
            if (playing) smokeParticle.Play();
            else smokeParticle.Stop();
        }
    }

    [Button("즉시 점화 테스트")]
    private void TestIgnite() => OnIgnited();

    [Button("즉시 재로 만들기 테스트")]
    private void TestBecomeAsh()
    {
        if (_burnCoroutine != null)
            StopCoroutine(_burnCoroutine);
        BecomeAsh();
    }
}
