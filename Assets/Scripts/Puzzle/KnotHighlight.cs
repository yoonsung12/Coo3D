using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 불씨 야구 퍼즐의 매듭이 노란 은행잎 사이에 묻히지 않도록, 매듭을 주황빛으로 숨쉬듯 깜빡이게 한다.
// 불이 붙으면(FlammableObject.OnStartedBurning) 깜빡임을 멈추고 빛을 꺼서, 이후의 타는 연출·재 색 전환은 기존 그대로 보이게 한다.
// 매듭마다 따로 멈춰야 하므로 공유 재질을 바꾸지 않고, 렌더러마다 MaterialPropertyBlock으로 발광 색만 덮어쓴다.
// 재질(BridgeKnot)에는 Emission이 켜져 있어야 이 값이 화면에 반영된다.
[RequireComponent(typeof(FlammableObject))]
public class KnotHighlight : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("빛낼 렌더러")]
    private Renderer[] targetRenderers;
    // 매듭과 밧줄 메쉬의 Renderer를 연결한다(Visual/Knot, Visual/Rope).

    [Title("깜빡임 연출 (DOTween)")]
    [SerializeField, LabelText("발광 색")]
    private Color glowColor = new Color(1f, 0.55f, 0.1f);
    // 노란 잎과 구분되도록 주황색을 쓴다.

    [SerializeField, LabelText("최소 세기")]
    private float minIntensity = 0.3f;
    // 가장 어두울 때의 발광 세기다. 0이면 완전히 꺼졌다 켜지는 느낌이 된다.

    [SerializeField, LabelText("최대 세기")]
    private float maxIntensity = 2.5f;
    // 가장 밝을 때의 발광 세기다. 값이 클수록 멀리서도 눈에 띈다.

    [SerializeField, LabelText("한 번 밝아지는 시간")]
    private float pulseDuration = 0.8f;
    // 어두움 → 밝음까지 걸리는 시간이다. 되돌아오는 시간도 같아서 한 주기는 이 값의 2배다.

    [SerializeField, LabelText("Ease")]
    private Ease pulseEase = Ease.InOutSine;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 발광 세기")]
    private float _intensity;

    private FlammableObject _flammable;
    private MaterialPropertyBlock _block;
    private Tween _pulseTween;
    // 무한 반복 Tween이라 불이 붙거나 비활성화될 때 반드시 Kill한다.

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _flammable = GetComponent<FlammableObject>();
        _block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _flammable.OnStartedBurning += StopGlow;

        // 이미 불이 붙었거나 재가 된 매듭은 다시 빛내지 않는다.
        if (_flammable.CurrentState == FlammableObject.FireState.Unlit)
            StartGlow();
        else
            ApplyIntensity(0f);
    }

    private void OnDisable()
    {
        _flammable.OnStartedBurning -= StopGlow;
        _pulseTween?.Kill();
    }

    private void OnDestroy()
    {
        _pulseTween?.Kill();
    }

    // 최소 세기 ↔ 최대 세기를 Yoyo로 무한 반복한다.
    private void StartGlow()
    {
        _pulseTween?.Kill();
        ApplyIntensity(minIntensity);
        _pulseTween = DOVirtual.Float(minIntensity, maxIntensity, pulseDuration, ApplyIntensity)
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    // 불이 붙는 순간 깜빡임을 멈추고 빛을 끈다. 재가 됐을 때 주황빛이 남아 있지 않게 하기 위해서다.
    private void StopGlow()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
        ApplyIntensity(0f);
    }

    // 렌더러마다 발광 색 × 세기를 덮어쓴다. 다른 값(_BaseColor 등)은 건드리지 않아 재 색 전환과 충돌하지 않는다.
    private void ApplyIntensity(float intensity)
    {
        _intensity = intensity;
        if (targetRenderers == null) return;

        foreach (Renderer r in targetRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, glowColor * intensity);
            r.SetPropertyBlock(_block);
        }
    }

    [Button("깜빡임 다시 시작 (테스트)")]
    private void TestStartGlow()
    {
        if (!Application.isPlaying) return;
        StartGlow();
    }

    [Button("깜빡임 멈춤 (테스트)")]
    private void TestStopGlow()
    {
        if (!Application.isPlaying) return;
        StopGlow();
    }
}
