using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 천장에서 늘어진 합선 전선이다. 꺼짐 -> 경고(깜빡임) -> 켜짐 순서로 반복하며,
// 켜진 동안 수로 폭을 가로지르는 트리거에 탑승 튜브가 들어오면 감전 데미지를 주고 온 방향으로 튕겨낸다.
// 꺼져 있을 때 타이밍을 맞춰 지나가는 것이 퍼즐 포인트다.
[RequireComponent(typeof(BoxCollider))]
public class SparkWire : MonoBehaviour
{
    private enum Phase { Off, Warning, On }

    [Title("연결")]
    [SerializeField, LabelText("전기 조명")]
    private Light sparkLight;
    // 전선 끝의 Point Light다. 경고 때 깜빡이고 켜짐 때 밝아진다.

    [SerializeField, LabelText("불꽃 파티클 (선택)")]
    private ParticleSystem sparkParticles;
    // 켜진 동안 재생되는 스파크다.

    [SerializeField, LabelText("전선 렌더러 (선택)")]
    private Renderer cableRenderer;
    // 켜질 때 색을 밝게 바꿀 전선 메시다. 머티리얼 인스턴스를 만들어 쓴다.

    [SerializeField, LabelText("AudioSource (선택)")]
    private AudioSource audioSource;

    [SerializeField, LabelText("감전 소리 (선택)")]
    private AudioClip zapClip;

    [Title("주기 설정")]
    [SerializeField, LabelText("꺼짐 시간(초)")]
    private float offDuration = 2.5f;
    // 안전하게 지나갈 수 있는 시간이다. 짧을수록 어렵다.

    [SerializeField, LabelText("경고 시간(초)")]
    private float warningDuration = 0.6f;

    [SerializeField, LabelText("켜짐 시간(초)")]
    private float onDuration = 1.5f;

    [Title("감전 설정")]
    [SerializeField, LabelText("데미지")]
    private float damage = 20f;

    [SerializeField, LabelText("튕김 속도 (m/s)")]
    private float pushBackSpeed = 4f;
    // 튜브가 들어온 반대 방향으로 이 속도만큼 VelocityChange를 준다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("경고 깜빡임 횟수")]
    private int warningBlinks = 4;

    [SerializeField, LabelText("꺼진 밝기")]
    private float offIntensity = 0f;

    [SerializeField, LabelText("켜진 밝기")]
    private float onIntensity = 6f;

    [SerializeField, LabelText("전선 켜진 색")]
    private Color onColor = new Color(0.6f, 1f, 1f, 1f);

    [SerializeField, LabelText("전선 꺼진 색")]
    private Color offColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("현재 단계")]
    private Phase _phase = Phase.Off;

    public bool IsOn => _phase == Phase.On;

    private Sequence _cycleSequence;
    private Tween _forceTween;
    private bool _shockedThisActivation;
    private Material _cableMaterial;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (cableRenderer != null) _cableMaterial = cableRenderer.material;
    }

    private void OnEnable()
    {
        BuildCycle();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void KillTweens()
    {
        _cycleSequence?.Kill();
        _forceTween?.Kill();
    }

    // 꺼짐 -> 경고 -> 켜짐을 무한 반복하는 Sequence를 만든다.
    private void BuildCycle()
    {
        _cycleSequence?.Kill();
        SetPhase(Phase.Off);

        float blinkStep = warningDuration / Mathf.Max(1, warningBlinks * 2);
        _cycleSequence = DOTween.Sequence();
        _cycleSequence.AppendInterval(offDuration);
        _cycleSequence.AppendCallback(() => SetPhase(Phase.Warning));
        for (int i = 0; i < warningBlinks; i++)
        {
            _cycleSequence.AppendCallback(() => SetLightIntensity(onIntensity * 0.5f));
            _cycleSequence.AppendInterval(blinkStep);
            _cycleSequence.AppendCallback(() => SetLightIntensity(offIntensity));
            _cycleSequence.AppendInterval(blinkStep);
        }
        _cycleSequence.AppendCallback(() => SetPhase(Phase.On));
        _cycleSequence.AppendInterval(onDuration);
        _cycleSequence.AppendCallback(() => SetPhase(Phase.Off));
        _cycleSequence.SetLoops(-1, LoopType.Restart);
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        switch (phase)
        {
            case Phase.Off:
                ApplyOffVisual();
                break;
            case Phase.Warning:
                _shockedThisActivation = false;
                break;
            case Phase.On:
                _shockedThisActivation = false;
                SetLightIntensity(onIntensity);
                if (_cableMaterial != null) _cableMaterial.SetColor("_BaseColor", onColor);
                if (sparkParticles != null) sparkParticles.Play();
                if (audioSource != null && zapClip != null) audioSource.PlayOneShot(zapClip);
                break;
        }
    }

    private void ApplyOffVisual()
    {
        SetLightIntensity(offIntensity);
        if (_cableMaterial != null) _cableMaterial.SetColor("_BaseColor", offColor);
        if (sparkParticles != null) sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void SetLightIntensity(float value)
    {
        if (sparkLight != null) sparkLight.intensity = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryShock(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryShock(other);
    }

    private void TryShock(Collider other)
    {
        if (_phase != Phase.On || _shockedThisActivation) return;

        FloatTube tube = other.GetComponent<FloatTube>();
        if (tube == null || !tube.IsMounted) return;

        // 켜짐 한 번당 한 번만 감전시킨다. (PlayerHealth의 무적과 별개로 중복 피해를 막는다.)
        _shockedThisActivation = true;
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.TakeDamage(damage);

        // 사망하면 PlayerHealth가 GameOver를 띄우므로 튕김은 생략해도 되지만, 살아 있을 때를 위해 항상 적용한다.
        // 들어온 방향(현재 속도)의 반대로 밀어낸다. 거의 멈춘 상태면 전선 중심 바깥쪽으로 민다.
        Rigidbody body = tube.GetComponent<Rigidbody>();
        if (body == null) return;
        Vector3 flat = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        Vector3 away = flat.sqrMagnitude > 0.01f ? -flat.normalized : (body.position - transform.position).normalized;
        away.y = 0f;
        // 이미 가진 속도를 지우고 반대 방향 속도를 줘야 확실히 튕겨난다.
        body.linearVelocity = Vector3.zero;
        body.AddForce(away * pushBackSpeed, ForceMode.VelocityChange);
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(1f, 0.95f, 0.2f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    [Button("감전 강제 켜기")]
    private void TestForceOn()
    {
        if (!Application.isPlaying) return;
        _cycleSequence?.Kill();
        _forceTween?.Kill();
        SetPhase(Phase.On);
        // 켜짐 시간이 지나면 다시 정상 주기로 돌아간다.
        _forceTween = DOVirtual.DelayedCall(onDuration, BuildCycle);
    }
}
