using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 씬 전환 포털(ScenePortal)의 겉모습 연출만 담당한다.
// 소용돌이 면을 계속 회전시키고 크기와 빛 밝기를 살짝 맥동시켜 "살아 있는 포털"처럼 보이게 한다.
// 씬 전환 로직은 ScenePortal이 그대로 처리하므로 이 컴포넌트는 판정이나 이동에 관여하지 않는다.
public class PortalVisual : MonoBehaviour
{
    [Title("연결 참조")]
    [SerializeField, LabelText("회전할 소용돌이")]
    private Transform swirlSpin;
    // 소용돌이 텍스처가 붙은 Quad를 연결한다. 이 오브젝트가 자기 Z축으로 계속 돈다.
    // 텍스처가 원형이라 부모(타원 크기)는 그대로 두고 이 Quad만 돌려도 모양이 흔들리지 않는다.

    [SerializeField, LabelText("맥동할 소용돌이 묶음")]
    private Transform swirlRoot;
    // 소용돌이의 전체 크기를 담당하는 부모를 연결한다. 이 오브젝트의 크기가 살짝 커졌다 작아진다.

    [SerializeField, LabelText("빛 (선택)")]
    private Light glowLight;
    // 포털 앞 바닥을 비추는 Point Light다. 비워두면 빛 맥동은 생략한다.

    [Title("회전")]
    [SerializeField, LabelText("한 바퀴 시간 (초)"), MinValue(0.1f)]
    private float spinDuration = 4f;
    // 값이 작을수록 소용돌이가 빨리 돈다.

    [Title("맥동")]
    [SerializeField, LabelText("맥동 크기 비율")]
    private float pulseScale = 1.06f;
    // 원래 크기에 곱할 최대 비율이다. 1.06이면 6%까지 커졌다가 돌아온다.

    [SerializeField, LabelText("맥동 시간 (초)"), MinValue(0.1f)]
    private float pulseDuration = 1.2f;
    // 커졌다가 원래대로 돌아오는 데 걸리는 시간의 절반이다(Yoyo라 왕복은 두 배).

    [SerializeField, LabelText("맥동 Ease")]
    private Ease pulseEase = Ease.InOutSine;

    [SerializeField, LabelText("빛 최소 밝기")]
    private float lightMinIntensity = 1.5f;

    [SerializeField, LabelText("빛 최대 밝기")]
    private float lightMaxIntensity = 3f;

    private Tween _spinTween;
    private Tween _pulseTween;
    private Tween _lightTween;
    // 비활성화·파괴 때 정리할 수 있도록 각각의 Tween을 저장해 둔다.

    private Vector3 _swirlBaseScale;
    // 맥동 기준이 되는 원래 크기다. 다시 켜질 때 크기가 누적되어 커지지 않게 기억한다.

    private void Awake()
    {
        if (swirlRoot != null)
            _swirlBaseScale = swirlRoot.localScale;
    }

    // 포털이 켜질 때마다 연출을 처음부터 다시 시작한다.
    private void OnEnable()
    {
        PlayIdle();
    }

    // 꺼지거나 파괴될 때 Tween을 정리해야 빈 Transform을 건드리는 오류가 생기지 않는다.
    private void OnDisable()
    {
        KillTweens();
    }

    private void PlayIdle()
    {
        KillTweens();

        if (swirlSpin != null)
        {
            // 로컬 Z축으로 360도를 계속 더해 가며 일정한 속도로 무한 회전한다.
            _spinTween = swirlSpin
                .DOLocalRotate(new Vector3(0f, 0f, -360f), spinDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);
        }

        if (swirlRoot != null)
        {
            swirlRoot.localScale = _swirlBaseScale;
            _pulseTween = swirlRoot
                .DOScale(_swirlBaseScale * pulseScale, pulseDuration)
                .SetEase(pulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (glowLight != null)
        {
            glowLight.intensity = lightMinIntensity;
            _lightTween = glowLight
                .DOIntensity(lightMaxIntensity, pulseDuration)
                .SetEase(pulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    private void KillTweens()
    {
        _spinTween?.Kill();
        _pulseTween?.Kill();
        _lightTween?.Kill();
    }

    [Button("연출 다시 시작 (Play Mode)")]
    private void TestRestart()
    {
        if (Application.isPlaying) PlayIdle();
    }
}
