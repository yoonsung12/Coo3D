using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 겨울 패턴 파훼용 온기존(화로)이다. 성냥(TorchTool)으로 점화하면 일정 시간 동안
// 불이 켜져 있고, 그동안 BossWinterPattern이 "유인 대상"으로 감지할 수 있게 된다.
// 지속시간이 다 되면 자동으로 꺼지며(파훼 실패), 다시 점화해서 재도전할 수 있다.
[RequireComponent(typeof(Collider))]
public class Brazier : MonoBehaviour, IIgnitable
{
    [Title("지속시간 설정")]
    [SerializeField, LabelText("불이 유지되는 시간(초)")]
    private float burnDuration = 12f;
    // 이 시간 안에 보스를 유인해서 고드름으로 맞혀야 한다. 값이 클수록 여유롭게 재도전할 시간이 늘어난다.

    [Title("연출 연결")]
    [SerializeField, LabelText("불빛(Point Light)")]
    private Light fireLight;
    // Inspector에서 화로 오브젝트 자식의 Point Light를 연결한다.

    [SerializeField, LabelText("불꽃 파티클")]
    private ParticleSystem fireParticle;
    // 비워두면 파티클 없이 불빛만으로 점화/소화 연출을 표현한다.

    [Title("불빛 연출 설정")]
    [SerializeField, LabelText("최대 밝기")]
    private float lightMaxIntensity = 1.5f;

    [SerializeField, LabelText("밝아지는 시간")]
    private float fadeInDuration = 0.2f;

    [SerializeField, LabelText("어두워지는 시간")]
    private float fadeOutDuration = 0.4f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("불이 켜짐")]
    public bool IsLit { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("남은 시간")]
    private float _remainingTime;

    private Tween _lightTween;

    private void Update()
    {
        if (!IsLit) return;

        // 손에 들고 계속 태우는 횃불(TorchTool)과 달리, 화로는 한 번 점화되면 플레이어가
        // 옆에 없어도(다른 곳으로 이동해도) 계속 시간이 줄어든다 — 보스를 유인하러 이동해야 하기 때문이다.
        _remainingTime -= Time.deltaTime;
        if (_remainingTime <= 0f)
            Extinguish();
    }

    private void OnDestroy()
    {
        _lightTween?.Kill();
    }

    // TorchTool.Ignite()의 전방 SphereCast에 감지되면 호출된다.
    public void OnIgnited()
    {
        if (IsLit) return;

        IsLit = true;
        _remainingTime = burnDuration;
        SetLight(true);
        if (fireParticle != null) fireParticle.Play();
    }

    // 지속시간 만료(자동) 또는 BossWinterPattern이 파훼 성공 시 직접 호출해 정리한다.
    public void Extinguish()
    {
        if (!IsLit) return;

        IsLit = false;
        SetLight(false);
        if (fireParticle != null) fireParticle.Stop();
    }

    private void SetLight(bool on)
    {
        if (fireLight == null) return;

        _lightTween?.Kill();

        if (on)
        {
            fireLight.enabled = true;
            fireLight.intensity = 0f;
            _lightTween = fireLight.DOIntensity(lightMaxIntensity, fadeInDuration).SetEase(Ease.OutQuad);
        }
        else
        {
            _lightTween = fireLight.DOIntensity(0f, fadeOutDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() => fireLight.enabled = false);
        }
    }

    [Button("점화 테스트")]
    private void TestIgnite() => OnIgnited();

    [Button("소화 테스트")]
    private void TestExtinguish() => Extinguish();
}
