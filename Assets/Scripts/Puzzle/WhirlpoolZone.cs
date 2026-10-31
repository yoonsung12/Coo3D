using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 물 위의 소용돌이다. 반경 안의 FloatTube를 중심으로 빨아들이며 시계 방향으로 돌린다.
// 중심(전복 반경) 안까지 끌려가면 튜브가 뒤집혀 최신 체크포인트로 돌아간다.
// 가장자리에서 선풍기 반동으로 벗어나면 안전하다.
[RequireComponent(typeof(SphereCollider))]
public class WhirlpoolZone : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("회전 모델")]
    private Transform visual;
    // 자식 디스크다. 제자리에서 계속 도는 연출만 담당한다.

    [Title("끌어당김 설정")]
    [SerializeField, LabelText("끌어당김 세기 (m/s²)")]
    private float pullStrength = 6f;
    // 중심에서의 최대 가속도다. 거리에 비례해 가장자리에서는 0으로 줄어든다.

    [SerializeField, LabelText("소용돌이 세기 (m/s²)")]
    private float swirlStrength = 4f;
    // 중심 둘레를 시계 방향(위에서 볼 때)으로 돌리는 가속도다.

    [SerializeField, LabelText("전복 반경")]
    private float capsizeRadius = 1.5f;
    // 튜브 중심이 이 거리 안으로 들어오면 전복한다.
    // 튜브가 중심을 빙빙 도는 궤도(원심력 때문에 중심에서 약 1.3m)를 만들어서, 이보다 작으면 영원히 돌기만 하고 전복하지 않는다.

    [SerializeField, LabelText("전복 재발동 대기 시간")]
    private float capsizeCooldown = 2f;
    // 전복 직후 튜브가 재배치되는 동안 중복 발동하지 않게 하는 시간이다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("1회전 시간")]
    private float spinDuration = 2.5f;

    private SphereCollider _zone;
    private Tween _spinTween;
    private float _nextCapsizeTime;

    private void Awake()
    {
        _zone = GetComponent<SphereCollider>();
        _zone.isTrigger = true;
    }

    private void OnEnable()
    {
        if (visual == null) return;
        _spinTween?.Kill();
        // 위에서 볼 때 시계 방향은 Y축 기준으로 양(+)의 회전이다.
        _spinTween = visual
            .DOLocalRotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    private void OnDisable()
    {
        _spinTween?.Kill();
    }

    private void OnDestroy()
    {
        _spinTween?.Kill();
    }

    private float WorldRadius => _zone.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

    private void OnTriggerStay(Collider other)
    {
        FloatTube tube = other.GetComponent<FloatTube>();
        if (tube == null) return;
        Rigidbody tubeBody = tube.GetComponent<Rigidbody>();
        if (tubeBody == null) return;

        Vector3 toCenter = transform.position - tubeBody.position;
        toCenter.y = 0f;
        float distance = toCenter.magnitude;
        float radius = WorldRadius;
        if (distance >= radius) return;

        // 중심에 가까울수록 1, 가장자리는 0이 되는 비율이다. 힘이 부드럽게 커지게 한다.
        float closeness = 1f - distance / radius;

        if (distance > 0.001f)
        {
            Vector3 inward = toCenter / distance;
            // 중심에서 튜브로 향하는 벡터(-inward)를 90도 돌리면 시계 방향 접선이 된다. (위에서 볼 때)
            Vector3 radial = -inward;
            Vector3 tangent = new Vector3(radial.z, 0f, -radial.x);

            // OnTriggerStay는 물리 스텝마다 호출되고 ForceMode.Acceleration이 fixedDeltaTime을 내부에서 곱하므로 deltaTime 불필요.
            tubeBody.AddForce(inward * (pullStrength * closeness) + tangent * (swirlStrength * closeness), ForceMode.Acceleration);
        }

        if (distance < capsizeRadius && tube.IsMounted && Time.time >= _nextCapsizeTime)
        {
            _nextCapsizeTime = Time.time + capsizeCooldown;
            tube.CapsizeToCheckpoint();
        }
    }

    private void OnDrawGizmos()
    {
        SphereCollider zone = GetComponent<SphereCollider>();
        if (zone == null) return;
        Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, zone.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, capsizeRadius);
    }

    [Button("전복 테스트")]
    private void TestCapsize()
    {
        if (!Application.isPlaying) return;
        FloatTube tube = FindFirstObjectByType<FloatTube>();
        if (tube != null && tube.IsMounted)
            tube.CapsizeToCheckpoint();
    }
}
