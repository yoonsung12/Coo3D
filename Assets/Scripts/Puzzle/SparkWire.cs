using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 물에 빠진 합선 전선이다. 꺼짐 -> 경고(깜빡임) -> 켜짐 순서로 반복하며,
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

    [SerializeField, LabelText("지직 반복 소리 (선택)")]
    private AudioClip buzzLoopClip;
    // 판정이 켜진 동안에만 반복 재생되는 지지직 소리다. 꺼짐/경고 때는 나지 않는다.

    [SerializeField, LabelText("지직 소리 볼륨"), Range(0f, 1f)]
    private float buzzVolume = 0.8f;

    [SerializeField, LabelText("지직 소리 끄는 시간(초)")]
    private float buzzFadeOutDuration = 0.1f;
    // 꺼질 때 소리가 뚝 끊기지 않게 이 시간 동안 볼륨을 줄인 뒤 멈춘다.

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

    [Title("물 위 전기 효과")]
    [SerializeField, LabelText("번개 띠 렌더러 (선택)")]
    private Renderer waterArcRenderer;
    // 전선을 따라 수면에 깔린 번개 띠 메시다. 가산(Additive) 파티클 머티리얼을 쓴다.
    // 텍스처는 세로로 번개 무늬 여러 줄(arcRows)을 쌓은 한 장이며, 켜진 동안 줄과 가로 위치를 바꿔 지직거린다.

    [SerializeField, LabelText("번개 무늬 줄 수")]
    private int arcRows = 4;

    [SerializeField, LabelText("켜진 밝기(알파)"), Range(0f, 1f)]
    private float arcOnAlpha = 1f;

    [SerializeField, LabelText("경고 밝기(알파)"), Range(0f, 1f)]
    private float arcWarningAlpha = 0.35f;
    // 경고 깜빡임 때 번개 띠가 희미하게 보이는 정도다. 0이면 경고 때 안 보인다.

    [SerializeField, LabelText("지직임 간격(초)")]
    private float arcFlickerInterval = 0.07f;
    // 켜진 동안 번개 무늬를 바꾸는 간격이다. 작을수록 빠르게 지직거린다.

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("현재 단계")]
    private Phase _phase = Phase.Off;

    public bool IsOn => _phase == Phase.On;

    private Sequence _cycleSequence;
    private Tween _forceTween;
    private Tween _buzzFadeTween;
    private bool _shockedThisActivation;
    private Material _cableMaterial;
    private Material _arcMaterial;
    private Sequence _arcFlickerSequence;
    private Color _arcBaseColor;
    private Vector2 _arcScale;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (cableRenderer != null) _cableMaterial = cableRenderer.material;
        if (waterArcRenderer != null)
        {
            _arcMaterial = waterArcRenderer.material;
            _arcBaseColor = _arcMaterial.GetColor("_BaseColor");
            _arcScale = _arcMaterial.GetTextureScale("_BaseMap");
        }
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
        _arcFlickerSequence?.Kill();
        _buzzFadeTween?.Kill();
        if (audioSource != null && audioSource.clip == buzzLoopClip) audioSource.Stop();
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
            _cycleSequence.AppendCallback(() => { SetLightIntensity(onIntensity * 0.5f); SetArcAlpha(arcWarningAlpha); RandomizeArcFrame(); });
            _cycleSequence.AppendInterval(blinkStep);
            _cycleSequence.AppendCallback(() => { SetLightIntensity(offIntensity); SetArcAlpha(0f); });
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
                StartArcFlicker();
                StartBuzz();
                if (audioSource != null && zapClip != null) audioSource.PlayOneShot(zapClip);
                break;
        }
    }

    private void ApplyOffVisual()
    {
        SetLightIntensity(offIntensity);
        if (_cableMaterial != null) _cableMaterial.SetColor("_BaseColor", offColor);
        if (sparkParticles != null) sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _arcFlickerSequence?.Kill();
        SetArcAlpha(0f);
        StopBuzz();
    }

    // 판정이 켜지는 순간 지직 소리를 반복 재생한다. AudioSource의 clip/loop를 사용하므로 PlayOneShot(감전 소리)과 겹쳐도 된다.
    private void StartBuzz()
    {
        if (audioSource == null || buzzLoopClip == null) return;
        _buzzFadeTween?.Kill();
        audioSource.clip = buzzLoopClip;
        audioSource.loop = true;
        audioSource.volume = buzzVolume;
        audioSource.Play();
    }

    // 판정이 꺼지면 볼륨을 짧게 줄인 뒤 멈춘다.
    private void StopBuzz()
    {
        if (audioSource == null || buzzLoopClip == null || !audioSource.isPlaying || audioSource.clip != buzzLoopClip) return;
        _buzzFadeTween?.Kill();
        _buzzFadeTween = audioSource.DOFade(0f, buzzFadeOutDuration)
            .OnComplete(() => audioSource.Stop());
    }

    // 켜진 동안 일정 간격으로 번개 무늬를 바꾸는 반복 Sequence다. 꺼짐/비활성화 때 Kill한다.
    private void StartArcFlicker()
    {
        if (_arcMaterial == null) return;
        _arcFlickerSequence?.Kill();
        SetArcAlpha(arcOnAlpha);
        RandomizeArcFrame();
        _arcFlickerSequence = DOTween.Sequence()
            .AppendInterval(arcFlickerInterval)
            .AppendCallback(RandomizeArcFrame)
            .SetLoops(-1, LoopType.Restart);
    }

    // 텍스처의 번개 줄 하나를 무작위로 고르고 가로 위치도 섞어서, 매번 다른 번개처럼 보이게 한다.
    private void RandomizeArcFrame()
    {
        if (_arcMaterial == null) return;
        int rows = Mathf.Max(1, arcRows);
        int row = Random.Range(0, rows);
        _arcMaterial.SetTextureScale("_BaseMap", new Vector2(_arcScale.x, 1f / rows));
        _arcMaterial.SetTextureOffset("_BaseMap", new Vector2(Random.value, (float)row / rows));
    }

    // 가산 머티리얼은 알파가 밝기에 곱해지므로 알파 0이면 완전히 안 보인다.
    private void SetArcAlpha(float alpha)
    {
        if (_arcMaterial == null) return;
        Color c = _arcBaseColor;
        c.a = alpha;
        _arcMaterial.SetColor("_BaseColor", c);
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
