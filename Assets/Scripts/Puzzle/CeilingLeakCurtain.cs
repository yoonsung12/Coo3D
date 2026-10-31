using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 동굴 천장에서 주기적으로 쏟아지는 "물벼락"이다. 수로 폭 전체를 가로지르는 트리거 BoxCollider가 판정 범위가 된다.
// 대기 -> 경고(물방울 증가 + 수면 파문) -> 쏟아짐(위험) -> 물보라(페이드) 순서를 DOTween Sequence로 무한 반복한다.
// 쏟아지는 동안에만 탑승 튜브가 트리거 안에 있으면 체크포인트로 전복한다. 우산은 막아 주지 않는다. (순수 타이밍 장애물)
// 두 곳 이상 배치할 때는 시작 지연(startDelay)을 달리해 서로 엇박자가 나게 한다.
[RequireComponent(typeof(BoxCollider))]
public class CeilingLeakCurtain : MonoBehaviour
{
    private enum Phase { Idle, Warning, Gush, Splash }

    [Title("연결")]
    [SerializeField, LabelText("물줄기 기둥")]
    private Transform stream;
    // 쏟아질 때 천장에서 수면까지 내려오는 두꺼운 반투명 물 기둥(Cube)이다. 대기 중에는 렌더러를 꺼 둔다.

    [SerializeField, LabelText("수면 파문 (선택)")]
    private Transform ripple;
    // 경고 동안 수면에서 퍼져 나가는 납작한 링(원기둥)이다. 대기 중에는 숨긴다.

    [SerializeField, LabelText("물방울 파티클 (선택)")]
    private ParticleSystem drips;
    // 천장에서 떨어지는 물방울이다. 경고에 가까워질수록 방출량이 늘어난다.

    [SerializeField, LabelText("물보라 파티클 (선택)")]
    private ParticleSystem splashParticles;
    // 쏟아지는 순간 수면에서 한 번 터뜨리는 물방울이다.

    [SerializeField, LabelText("카메라 (선택)")]
    private SideViewCamera sideViewCamera;
    // 쏟아지는 순간 가볍게 흔들 카메라다. 비워 두면 흔들림 없이 진행한다.

    [Title("주기 설정 (초)")]
    [SerializeField, LabelText("시작 지연")]
    private float startDelay = 0f;
    // 처음 한 번만 적용되는 지연이다. 장애물끼리 엇박자를 만들 때 쓴다. 값이 클수록 첫 물벼락이 늦게 온다.

    [SerializeField, LabelText("대기 시간")]
    private float idleDuration = 2f;
    // 안전하게 지나갈 수 있는 시간이다. 짧을수록 어렵다.

    [SerializeField, LabelText("경고 시간")]
    private float warningDuration = 0.8f;
    // 물방울이 늘고 수면에 파문이 퍼지는 시간이다. 플레이어가 곧 쏟아짐을 알아챌 단서다.

    [SerializeField, LabelText("쏟아짐 시간 (위험)")]
    private float gushDuration = 0.6f;
    // 이 시간 동안만 트리거 안의 탑승 튜브가 전복한다.

    [SerializeField, LabelText("물보라 시간")]
    private float splashDuration = 0.3f;
    // 쏟아짐이 끝난 뒤 기둥이 사라지는 시간이다. 위험 판정은 없다.

    [Title("판정 설정")]
    [SerializeField, LabelText("재발동 대기 시간")]
    private float cooldown = 2f;
    // 전복 직후 튜브가 재배치되는 동안 중복 발동하지 않게 하는 시간이다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("천장 월드 높이")]
    private float ceilingWorldY = -16.95f;
    // 물 기둥의 윗면이 항상 붙어 있는 높이다.

    [SerializeField, LabelText("수면 월드 높이")]
    private float waterWorldY = -28.95f;
    // 물 기둥의 아랫면이 닿는 높이다.

    [SerializeField, LabelText("기둥 가로 폭")]
    private float streamWidth = 7f;
    // 수로 폭 전체(X)를 덮는 값이다.

    [SerializeField, LabelText("기둥 두께")]
    private float streamDepth = 1.2f;

    [SerializeField, LabelText("기둥 낙하 시간")]
    private float streamDropDuration = 0.2f;
    // 쏟아짐이 시작되고 기둥이 천장에서 수면까지 내려오는 데 걸리는 시간이다. 쏟아짐 시간보다 짧아야 한다.

    [SerializeField, LabelText("낙하 Ease")]
    private Ease streamDropEase = Ease.InQuad;

    [SerializeField, LabelText("파문 최대 크기 (X, Z)")]
    private Vector2 rippleMaxSize = new Vector2(7f, 2.5f);

    [SerializeField, LabelText("파문 시작 크기 비율")]
    private float rippleStartScale = 0.15f;

    [SerializeField, LabelText("평상시 물방울 방출량")]
    private float idleDripRate = 2f;

    [SerializeField, LabelText("경고 때 물방울 방출량")]
    private float warningDripRate = 40f;

    [SerializeField, LabelText("카메라 흔들림 시간")]
    private float shakeDuration = 0.25f;

    [SerializeField, LabelText("카메라 흔들림 세기")]
    private float shakeStrength = 0.15f;

    [Title("소리")]
    [SerializeField, LabelText("AudioSource")]
    private AudioSource audioSource;
    // 2D 소리용이다. Play On Awake는 끈다.

    [SerializeField, LabelText("경고 소리")]
    private AudioClip warningClip;

    [SerializeField, LabelText("경고 볼륨"), Range(0f, 1f)]
    private float warningVolume = 0.6f;

    [SerializeField, LabelText("쏟아짐 소리")]
    private AudioClip gushClip;

    [SerializeField, LabelText("쏟아짐 볼륨"), Range(0f, 1f)]
    private float gushVolume = 0.9f;

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("현재 단계")]
    private Phase _phase = Phase.Idle;

    private Sequence _cycleSequence;
    private float _nextTriggerTime;

    // 연출 상태값(0~1)이다. Tween이 이 값을 바꾸면 Apply 메서드가 실제 오브젝트에 반영한다.
    private float _streamHeight01;
    private float _streamAlpha01;
    private float _rippleSize01;
    private float _rippleAlpha01;

    private Renderer _streamRenderer;
    private Renderer _rippleRenderer;
    private Material _streamMaterial;
    private Material _rippleMaterial;
    private Color _streamBaseColor = Color.white;
    private Color _rippleBaseColor = Color.white;
    private float _streamTopLocalY;
    private float _streamFullHeight;
    private float _rippleBaseScaleY = 1f;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true; // 막지 않고 감지만 한다.

        // 월드 높이를 로컬 높이로 바꿔 둔다. 이후 기둥은 윗면(천장)을 고정한 채 아래로만 늘어난다.
        _streamTopLocalY = transform.InverseTransformPoint(new Vector3(transform.position.x, ceilingWorldY, transform.position.z)).y;
        _streamFullHeight = ceilingWorldY - waterWorldY;

        if (stream != null)
        {
            _streamRenderer = stream.GetComponent<Renderer>();
            if (_streamRenderer != null)
            {
                _streamMaterial = _streamRenderer.material; // 인스턴스를 만들어 알파를 개별 조절한다.
                _streamBaseColor = _streamMaterial.GetColor("_BaseColor");
            }
        }

        if (ripple != null)
        {
            _rippleRenderer = ripple.GetComponent<Renderer>();
            _rippleBaseScaleY = ripple.localScale.y;
            if (_rippleRenderer != null)
            {
                _rippleMaterial = _rippleRenderer.material;
                _rippleBaseColor = _rippleMaterial.GetColor("_BaseColor");
            }
        }
    }

    private void OnEnable()
    {
        BuildCycle(startDelay);
    }

    private void OnDisable()
    {
        _cycleSequence?.Kill();
    }

    private void OnDestroy()
    {
        _cycleSequence?.Kill();
        // Awake에서 만든 머티리얼 인스턴스는 직접 지워야 누수가 나지 않는다.
        if (_streamMaterial != null) Destroy(_streamMaterial);
        if (_rippleMaterial != null) Destroy(_rippleMaterial);
    }

    // 대기 -> 경고 -> 쏟아짐 -> 물보라를 무한 반복하는 Sequence를 만든다. delay는 첫 바퀴에만 적용된다.
    private void BuildCycle(float delay)
    {
        _cycleSequence?.Kill();
        ResetToIdle();

        _cycleSequence = DOTween.Sequence();
        _cycleSequence.AppendInterval(idleDuration);
        AppendWarningGushSplash(_cycleSequence);
        _cycleSequence.AppendCallback(ResetToIdle);
        _cycleSequence.SetDelay(delay);
        _cycleSequence.SetLoops(-1, LoopType.Restart);
    }

    // 경고 -> 쏟아짐 -> 물보라 구간을 주어진 Sequence 뒤에 이어 붙인다. 정상 주기와 "강제로 쏟기"가 같이 쓴다.
    private void AppendWarningGushSplash(Sequence seq)
    {
        // 경고: 물방울 증가 + 수면 파문이 커지며 짙어진다.
        seq.AppendCallback(StartWarning);
        seq.Append(DOTween.To(() => _rippleSize01, v => { _rippleSize01 = v; ApplyRipple(); }, 1f, warningDuration).SetEase(Ease.OutQuad));
        seq.Join(DOTween.To(() => _rippleAlpha01, v => { _rippleAlpha01 = v; ApplyRipple(); }, 1f, warningDuration));
        seq.Join(DOTween.To(() => GetDripRate(), SetDripRate, warningDripRate, warningDuration).SetEase(Ease.InQuad));

        // 쏟아짐: 기둥이 천장에서 수면까지 빠르게 떨어지고, 남은 시간 동안 유지된다.
        seq.AppendCallback(StartGush);
        float drop = Mathf.Min(streamDropDuration, gushDuration);
        seq.Append(DOTween.To(() => _streamHeight01, v => { _streamHeight01 = v; ApplyStream(); }, 1f, drop).SetEase(streamDropEase));
        seq.AppendInterval(Mathf.Max(0f, gushDuration - drop));

        // 물보라: 기둥과 파문이 옅어지며 사라진다. 위험 판정은 끝난다.
        seq.AppendCallback(StartSplash);
        seq.Append(DOTween.To(() => _streamAlpha01, v => { _streamAlpha01 = v; ApplyStream(); }, 0f, splashDuration));
        seq.Join(DOTween.To(() => _rippleAlpha01, v => { _rippleAlpha01 = v; ApplyRipple(); }, 0f, splashDuration));
        seq.Join(DOTween.To(() => GetDripRate(), SetDripRate, idleDripRate, splashDuration));
    }

    // 모든 연출을 대기 상태(숨김)로 되돌린다.
    private void ResetToIdle()
    {
        _phase = Phase.Idle;
        _streamHeight01 = 0f;
        _streamAlpha01 = 0f;
        _rippleSize01 = 0f;
        _rippleAlpha01 = 0f;
        ApplyStream();
        ApplyRipple();
        SetDripRate(idleDripRate);
    }

    private void StartWarning()
    {
        _phase = Phase.Warning;
        _rippleSize01 = 0f;
        _rippleAlpha01 = 0f;
        ApplyRipple();
        PlayClip(warningClip, warningVolume);
    }

    private void StartGush()
    {
        _phase = Phase.Gush;
        _streamHeight01 = 0f;
        _streamAlpha01 = 1f;
        ApplyStream();
        SetDripRate(0f); // 기둥이 떨어지는 동안은 물방울이 보이지 않게 한다.
        if (splashParticles != null) splashParticles.Play();
        PlayClip(gushClip, gushVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(shakeDuration, shakeStrength);
    }

    private void StartSplash()
    {
        _phase = Phase.Splash;
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    // 기둥의 높이/위치/알파를 상태값에 맞춘다. 윗면은 천장에 고정하고 아래로만 늘어난다.
    private void ApplyStream()
    {
        if (stream == null) return;
        float height = _streamFullHeight * _streamHeight01;
        bool visible = height > 0.001f && _streamAlpha01 > 0.001f;
        if (_streamRenderer != null) _streamRenderer.enabled = visible;
        if (!visible) return;

        stream.localScale = new Vector3(streamWidth, height, streamDepth);
        stream.localPosition = new Vector3(0f, _streamTopLocalY - height * 0.5f, 0f);
        if (_streamMaterial != null)
        {
            Color c = _streamBaseColor;
            c.a *= _streamAlpha01;
            _streamMaterial.SetColor("_BaseColor", c);
        }
    }

    // 파문의 크기/알파를 상태값에 맞춘다. 크기는 작은 비율에서 수로 폭까지 커진다.
    private void ApplyRipple()
    {
        if (ripple == null) return;
        bool visible = _rippleAlpha01 > 0.001f;
        if (_rippleRenderer != null) _rippleRenderer.enabled = visible;
        if (!visible) return;

        float s = Mathf.Lerp(rippleStartScale, 1f, _rippleSize01);
        ripple.localScale = new Vector3(rippleMaxSize.x * s, _rippleBaseScaleY, rippleMaxSize.y * s);
        if (_rippleMaterial != null)
        {
            Color c = _rippleBaseColor;
            c.a *= _rippleAlpha01;
            _rippleMaterial.SetColor("_BaseColor", c);
        }
    }

    private float GetDripRate()
    {
        return drips != null ? drips.emission.rateOverTime.constant : 0f;
    }

    private void SetDripRate(float rate)
    {
        if (drips == null) return;
        ParticleSystem.EmissionModule emission = drips.emission;
        emission.rateOverTime = rate;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTube(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleTube(other);
    }

    // 쏟아지는 순간에만 탑승 튜브를 전복시킨다. 그 외 단계에는 안전하다.
    private void HandleTube(Collider other)
    {
        if (_phase != Phase.Gush) return;
        FloatTube tube = other.GetComponent<FloatTube>();
        if (tube == null || !tube.IsMounted) return; // 빈 튜브는 그냥 떠내려가게 둔다.
        if (Time.time < _nextTriggerTime) return;
        _nextTriggerTime = Time.time + cooldown;
        tube.CapsizeToCheckpoint();
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    [Button("강제로 쏟기")]
    private void TestGush()
    {
        if (!Application.isPlaying) return;
        // 정상 주기를 멈추고 경고+쏟아짐+물보라를 한 번 바로 실행한 뒤 정상 주기로 돌아간다.
        _cycleSequence?.Kill();
        ResetToIdle();
        Sequence once = DOTween.Sequence();
        AppendWarningGushSplash(once);
        once.AppendCallback(ResetToIdle);
        once.AppendCallback(() => BuildCycle(0f));
        _cycleSequence = once;
    }
}
