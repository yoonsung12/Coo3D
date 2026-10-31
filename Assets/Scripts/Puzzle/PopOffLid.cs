using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 환풍구 뚜껑처럼 구멍을 막고 있다가, 아래에서 플레이어가 몸으로 치면 위로 퉁 날아가는 뚜껑이다.
// 날아간 뒤에는 실제 물리(Rigidbody)로 떨어져 바닥에 텅텅 튀고, 잠시 뒤 작아지며 사라진다.
// 아래에서 닿았는지는 뚜껑 바로 아래에 둔 자식 트리거 콜라이더로 판정한다.
// (CharacterController는 Rigidbody를 밀지 못하므로 충돌 대신 트리거로 감지한다.)
[RequireComponent(typeof(Rigidbody))]
public class PopOffLid : MonoBehaviour
{
    [InfoBox("뚜껑 바로 아래에 Is Trigger를 켠 BoxCollider 자식 오브젝트를 두고 구멍 크기에 맞춘다. 플레이어가 이 트리거에 닿으면 날아간다.")]
    [Title("날아가기 설정")]
    [SerializeField, LabelText("위로 튀는 속도")]
    private float popUpSpeed = 7f;
    // 값이 클수록 더 높이 솟구친다. 7이면 약 2.5m 정도 올라갔다가 떨어진다.

    [SerializeField, LabelText("옆으로 밀리는 방향")]
    private Vector3 sideDirection = Vector3.back;
    // 날아간 뚜껑이 다시 구멍 위로 떨어져 막지 않도록 살짝 옆으로 보낼 방향이다. (월드 기준, Y는 무시)

    [SerializeField, LabelText("옆으로 밀리는 속도")]
    private float sideSpeed = 2.5f;
    // 0이면 제자리로 다시 떨어진다. 구멍 밖 바닥에 떨어질 만큼만 준다.

    [SerializeField, LabelText("회전 세기")]
    private float spinStrength = 6f;
    // 날아가며 빙글빙글 도는 정도(초당 라디안)다. 클수록 요란하게 돈다.

    [SerializeField, LabelText("튕김 정도"), Range(0f, 1f)]
    private float bounciness = 0.35f;
    // 바닥에 떨어졌을 때 다시 튀어 오르는 정도다. 0이면 튀지 않고, 1에 가까울수록 오래 텅텅 튄다.

    [Title("사라지기 (DOTween)")]
    [SerializeField, LabelText("사라지기까지 시간")]
    private float disappearDelay = 3f;
    // 날아간 뒤 이 시간이 지나면 작아지며 사라진다. 바닥에 남아 길을 막지 않게 하기 위해서다.

    [SerializeField, LabelText("작아지는 시간")]
    private float shrinkDuration = 0.4f;

    [Title("연출")]
    [SerializeField, LabelText("칠 때 소리")]
    private AudioClip popClip;
    // 몸으로 쳐서 뚜껑이 날아가는 순간 재생할 소리다.

    [SerializeField, LabelText("떨어질 때 소리")]
    private AudioClip impactClip;
    // 바닥 등에 부딪힐 때마다 재생할 소리다. 세게 부딪힐수록 크게 난다.

    [SerializeField, LabelText("AudioSource")]
    private AudioSource audioSource;
    // 같은 오브젝트에 AudioSource(Play On Awake 끔, Spatial Blend 0=2D)를 붙여 연결한다.

    [SerializeField, LabelText("부딪힘 소리 최소 속도")]
    private float minImpactSpeed = 1f;
    // 이 속도보다 약하게 부딪히면 소리를 내지 않는다. 바닥에서 미끄러질 때 소리가 계속 나는 것을 막는다.

    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // 칠 때 화면을 살짝 흔들 카메라다. 비워 두면 흔들지 않는다.

    [SerializeField, LabelText("카메라 흔들림 세기")]
    private float shakeStrength = 0.25f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("날아감")]
    private bool _hasPopped;

    private Rigidbody _rigidbody;
    private Collider _lidCollider;
    private Tween _disappearTween;
    private float _lastImpactTime;

    private const float ImpactSoundInterval = 0.1f;
    // 한 번 부딪힐 때 모서리 여러 곳이 연달아 닿아 소리가 겹치지 않도록 두는 최소 간격(초)이다.

    private const float PopLiftOffset = 0.25f;
    // 날리기 직전에 뚜껑을 띄우는 높이(m)다. 주변 바닥에 파묻힌 깊이보다 커야 한다.

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        // 트리거가 아닌 실제 뚜껑 콜라이더를 찾는다. 자식의 감지용 트리거는 제외한다.
        foreach (Collider col in GetComponents<Collider>())
        {
            if (!col.isTrigger)
            {
                _lidCollider = col;
                break;
            }
        }

        // 치기 전에는 구멍 위에 고정해 둔다. 물리로 두면 미세하게 흔들리거나 밀릴 수 있다.
        _rigidbody.isKinematic = true;

        // 바닥에 떨어졌을 때 튀도록 런타임 물리 재질을 만들어 붙인다.
        if (_lidCollider != null)
        {
            _lidCollider.material = new PhysicsMaterial("PopOffLid_Bounce")
            {
                bounciness = bounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = 0.6f,
                staticFriction = 0.6f
            };
        }
    }

    private void OnDestroy()
    {
        _disappearTween?.Kill();
    }

    // 자식 트리거에 들어온 것도 부모 Rigidbody가 있는 이 오브젝트로 전달된다.
    private void OnTriggerEnter(Collider other)
    {
        if (_hasPopped) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Pop(other);
    }

    // 뚜껑을 날린다. playerCollider가 있으면 날아가는 뚜껑이 플레이어에게 걸리지 않게 충돌을 끈다.
    private void Pop(Collider playerCollider)
    {
        _hasPopped = true;

        // 감지용 트리거는 더 이상 필요 없으므로 끈다.
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger)
                col.enabled = false;
        }

        if (playerCollider != null && _lidCollider != null)
            Physics.IgnoreCollision(_lidCollider, playerCollider, true);

        transform.position += Vector3.up * PopLiftOffset;
        // 뚜껑 가장자리가 주변 바닥에 살짝 파묻혀 놓여 있으면, 물리를 켜는 순간 겹친 것을 밀어내는 힘 때문에
        // 엉뚱한 옆 방향으로 튕겨 나간다. 날리기 직전에 살짝 띄워 겹침을 없앤다.

        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;

        Vector3 side = new Vector3(sideDirection.x, 0f, sideDirection.z).normalized;
        _rigidbody.linearVelocity = Vector3.up * popUpSpeed + side * sideSpeed;
        // 위로 크게, 옆으로는 구멍을 벗어날 만큼만 보낸다.

        _rigidbody.angularVelocity = Random.onUnitSphere * spinStrength;
        // 매번 조금씩 다르게 돌도록 회전축을 무작위로 정한다.

        PlaySound(popClip, 1f);

        if (sideViewCamera != null)
            sideViewCamera.Shake(0.2f, shakeStrength);

        // 바닥에 텅텅 튄 뒤 작아지며 사라지게 한다.
        _disappearTween?.Kill();
        _disappearTween = transform
            .DOScale(Vector3.zero, shrinkDuration)
            .SetDelay(disappearDelay)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }

    // 날아간 뒤 바닥/벽에 부딪힐 때마다 세기에 맞춰 소리를 낸다.
    private void OnCollisionEnter(Collision collision)
    {
        if (!_hasPopped) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;
        if (Time.time - _lastImpactTime < ImpactSoundInterval) return;

        _lastImpactTime = Time.time;
        float volume = Mathf.Clamp01(impactSpeed / popUpSpeed);
        // 처음 떨어질 때가 가장 크고, 튈수록 점점 작아진다.
        PlaySound(impactClip, volume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    [Button("날리기 테스트 (Play Mode)")]
    private void TestPop()
    {
        if (!Application.isPlaying || _hasPopped) return;
        Pop(null);
    }
}
