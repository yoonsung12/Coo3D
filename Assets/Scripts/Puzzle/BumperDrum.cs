using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 물 위에 떠 있는 범퍼 드럼이다. 튜브가 반경 안에 닿으면 바깥쪽으로 튕겨낸다.
// 드럼 자체에는 충돌체가 없고(막지 않음), 루트의 트리거 SphereCollider가 "튕김 범위"가 된다.
// 이 범위를 피해 틈으로 지나가는 것이 퍼즐 포인트다.
[RequireComponent(typeof(SphereCollider))]
public class BumperDrum : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("드럼 모델")]
    private Transform visual;
    // 자식 메시다. 튕김 펀치와 둥둥 떠 있는 연출은 이 오브젝트에만 적용한다.

    [SerializeField, LabelText("AudioSource")]
    private AudioSource audioSource;
    // 같은 오브젝트에 붙인 AudioSource(Play On Awake 끔, Spatial Blend 0=2D)다.

    [SerializeField, LabelText("튕김 소리")]
    private AudioClip bounceClip;
    // 튜브를 튕겨낼 때 재생할 소리다. (BoingSound)

    [Title("튕김 설정")]
    [SerializeField, LabelText("튕김 속도 (m/s)")]
    private float bounceSpeed = 6f;
    // 튜브의 수평 속도를 이 값으로 덮어쓴다. 클수록 멀리 튕겨난다.

    [SerializeField, LabelText("재튕김 대기 시간")]
    private float cooldown = 0.35f;
    // 한 번 튕긴 뒤 이 시간 동안은 다시 튕기지 않는다. 범위 안에서 매 프레임 속도를 덮어쓰는 것을 막는다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("펀치 세기")]
    private float punchStrength = 0.35f;

    [SerializeField, LabelText("펀치 시간")]
    private float punchDuration = 0.35f;

    [SerializeField, LabelText("둥실 높이")]
    private float bobHeight = 0.06f;
    // 가만히 있을 때 위아래로 흔들리는 폭(m)이다.

    [SerializeField, LabelText("둥실 주기")]
    private float bobDuration = 1.2f;

    [SerializeField, LabelText("Ease")]
    private Ease punchEase = Ease.OutQuad;

    private SphereCollider _zone;
    private Tween _punchTween;
    private Tween _bobTween;
    private float _nextBounceTime;
    private Vector3 _visualStartScale;
    private float _visualStartLocalY;

    private void Awake()
    {
        _zone = GetComponent<SphereCollider>();
        _zone.isTrigger = true; // 막지 않고 범위 감지만 하도록 강제한다.
        if (visual != null)
        {
            _visualStartScale = visual.localScale;
            _visualStartLocalY = visual.localPosition.y;
        }
    }

    private void OnEnable()
    {
        StartBob();
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
        _punchTween?.Kill();
        _bobTween?.Kill();
    }

    private void StartBob()
    {
        if (visual == null) return;
        _bobTween?.Kill();
        visual.localPosition = new Vector3(visual.localPosition.x, _visualStartLocalY, visual.localPosition.z);
        _bobTween = visual
            .DOLocalMoveY(_visualStartLocalY + bobHeight, bobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryBounce(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryBounce(other);
    }

    private void TryBounce(Collider other)
    {
        if (Time.time < _nextBounceTime) return;

        FloatTube tube = other.GetComponent<FloatTube>();
        if (tube == null) return;
        Rigidbody tubeBody = tube.GetComponent<Rigidbody>();
        if (tubeBody == null) return;

        Bounce(tubeBody);
    }

    private void Bounce(Rigidbody tubeBody)
    {
        _nextBounceTime = Time.time + cooldown;

        // 드럼 중심에서 튜브로 향하는 수평 방향이 "바깥쪽"이다. 정확히 겹치면 임의 방향(+X)으로 보낸다.
        Vector3 outward = tubeBody.position - transform.position;
        outward.y = 0f;
        outward = outward.sqrMagnitude < 0.0001f ? Vector3.right : outward.normalized;

        // 현재 속도를 무시하고 바깥 방향 속도로 덮어써야 어느 각도로 들어와도 확실히 튕겨난다. Y는 수면 고정이라 0.
        tubeBody.linearVelocity = outward * bounceSpeed;

        PlayPunch();
        if (audioSource != null && bounceClip != null)
            audioSource.PlayOneShot(bounceClip);
    }

    private void PlayPunch()
    {
        if (visual == null) return;
        _punchTween?.Kill();
        visual.localScale = _visualStartScale;
        _punchTween = visual
            .DOPunchScale(Vector3.one * punchStrength, punchDuration)
            .SetEase(punchEase);
    }

    private void OnDrawGizmos()
    {
        SphereCollider zone = GetComponent<SphereCollider>();
        if (zone == null) return;
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + zone.center, zone.radius * transform.lossyScale.x);
    }

    [Button("튕김 테스트")]
    private void TestBounce()
    {
        if (!Application.isPlaying) return;
        PlayPunch();
        if (audioSource != null && bounceClip != null)
            audioSource.PlayOneShot(bounceClip);
    }
}
