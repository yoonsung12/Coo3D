using System;
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

    [SerializeField, LabelText("재 먼지 파티클(1회성)")]
    private ParticleSystem ashPuffParticle;
    // 다 타서 재가 되는 순간 한 번만 터지는 파티클이다. 색상 전환만으로는 "타서 재가 됐다"는
    // 느낌이 약해서, 그 순간을 눈에 띄게 강조하기 위해 추가한다. 비워두면 색상 전환만 일어난다.

    [SerializeField, LabelText("재 색상")]
    private Color ashColor = new Color(0.25f, 0.25f, 0.25f);
    // 다 타서 재가 됐을 때 Material이 바뀌는 색상이다.

    [SerializeField, LabelText("색상 전환 시간")]
    private float colorChangeDuration = 0.6f;

    [SerializeField, LabelText("색을 바꿀 렌더러")]
    private Renderer colorTargetRenderer;
    // 재 색으로 바뀔 렌더러다. 덩굴 덮인 돌 블록처럼 잎과 돌이 자식으로 나뉜 오브젝트는
    // 잎 렌더러만 연결해 잎만 그을리게 한다. 비워두면 기존처럼 자기 자신의 MeshRenderer를 쓴다.

    [Title("다 탄 뒤 사라짐 연출 (선택)")]
    [SerializeField, LabelText("다 타면 사라질 오브젝트")]
    private Transform hideOnBurnedOut;
    // 재가 된 뒤 작아지며 사라질 오브젝트(예: 돌 아치를 덮은 잎/덩굴)다. 비워두면 사라지는 연출 없이 색만 바뀐다.

    [SerializeField, LabelText("사라지는 시간"), ShowIf("@hideOnBurnedOut != null")]
    private float hideDuration = 0.8f;
    // 잎이 크기 0까지 줄어드는 데 걸리는 시간이다. 값이 클수록 천천히 사그라든다.

    [SerializeField, LabelText("사라지는 Ease"), ShowIf("@hideOnBurnedOut != null")]
    private Ease hideEase = Ease.InBack;
    // InBack은 살짝 부풀었다가 빨려 들어가듯 줄어들어 "타서 바스러지는" 느낌을 준다.

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

    [Title("장애물 설정")]
    [SerializeField, LabelText("연소 전까지 통행 차단")]
    private bool blocksPathUntilBurned = false;
    // 기본값은 false로, 기존과 동일하게 항상 트리거(통과 가능)로 동작한다.
    // true로 켜면 마른 덩굴벽처럼 다 타기 전까지는 실제로 길을 막는 장애물이 되고,
    // 다 타서 재가 되면 지나갈 수 있게 자동으로 열린다.

    [SerializeField, LabelText("다 타면 열 문")]
    private DoorController doorToOpen;
    // Inspector에서 이 덩굴이 다 탔을 때 열릴 문(DoorHinge)을 연결한다.
    // 낙엽더미(LeafMound)로 덩굴에 불을 붙여 문을 여는 퍼즐용이다. 비워두면 문과 연결되지 않는다.
    // 도화선(FuseLine)처럼 외부에서 따로 문을 연결하는 덩굴은 비워둔다(OpenDoor는 중복 호출돼도 안전하다).

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    public FireState CurrentState { get; private set; } = FireState.Unlit;

    // 다 타서 재가 됐을 때 호출된다. 문 열림 연출처럼 외부 시스템이 완전 연소 시점을
    // 구독해서 반응할 수 있게 하기 위한 이벤트다.
    public event Action OnBurnedOut;

    private Collider _collider;
    private Renderer _meshRenderer;
    private Tween _colorTween;
    private Sequence _burnOutSequence;
    // 재 색 전환 → 잎 축소 → 비활성화로 이어지는 연출이다. 파괴 시 정리하기 위해 저장한다.
    private Coroutine _burnCoroutine;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 마른 덤불/낙엽 더미 같은 소품은 플레이어 이동을 막지 않고 통과할 수 있어야 하므로
        // 트리거로 두지만, blocksPathUntilBurned가 켜져 있으면 덩굴벽처럼 실제 장애물이 되어야
        // 하므로 콜라이더를 막힌 상태(isTrigger = false)로 시작한다.
        _collider.isTrigger = !blocksPathUntilBurned;

        _meshRenderer = colorTargetRenderer != null ? colorTargetRenderer : GetComponent<MeshRenderer>();
        // material 접근 시 인스턴스 머티리얼이 생성되어 이 오브젝트만의 색상을 독립적으로 바꿀 수 있다.

        if (doorToOpen != null)
            OnBurnedOut += doorToOpen.OpenDoor;
        // 완전 연소(재가 되는 순간) 이벤트에 문 열기를 연결해, 덩굴이 다 타면 문이 열리게 한다.
    }

    private void OnDestroy()
    {
        _colorTween?.Kill();
        _burnOutSequence?.Kill();
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

        // 타서 재가 되는 순간을 강조하기 위한 1회성 파티클이다. Play()만 하면 Loop 설정에 따라
        // 계속 재생될 수 있으므로 Emit()으로 정해진 개수만 한 번에 즉시 방출한다.
        if (ashPuffParticle != null)
            ashPuffParticle.Emit(20);

        // 장애물로 막고 있던 콜라이더라면 다 탄 뒤 지나갈 수 있도록 연다.
        if (blocksPathUntilBurned)
            _collider.isTrigger = true;

        OnBurnedOut?.Invoke();

        if (_meshRenderer != null)
        {
            _colorTween?.Kill();
            _colorTween = _meshRenderer.material.DOColor(ashColor, "_BaseColor", colorChangeDuration);
        }

        PlayHideSequence();
    }

    // 잎처럼 다 타면 없어져야 하는 부분을 재 색 전환이 끝난 뒤 줄여서 숨긴다.
    private void PlayHideSequence()
    {
        if (hideOnBurnedOut == null) return;

        _burnOutSequence?.Kill();
        _burnOutSequence = DOTween.Sequence()
            .AppendInterval(colorChangeDuration)
            // 잎이 먼저 까맣게 그을린 모습을 보여준 뒤 사라지도록 색 전환 시간만큼 기다린다.
            .Append(hideOnBurnedOut.DOScale(Vector3.zero, hideDuration).SetEase(hideEase))
            .OnComplete(() => hideOnBurnedOut.gameObject.SetActive(false));
            // 크기 0인 채로 남겨두지 않고 꺼서 렌더링 비용도 없앤다.
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
