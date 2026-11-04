using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 "불씨 야구" 퍼즐의 불씨다. 위에서 떨어지다 화로 높이를 지나면 불이 붙고,
// 배게검(SwordHitbox)에 맞으면 맞은 높이에 해당하는 목표를 향해 일직선으로 날아간다.
// 기존 LeafMound는 선풍기 퍼즐과 다른 씬에서 쓰이므로 건드리지 않고 별도 컴포넌트로 만든다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class HittableEmber : MonoBehaviour, IHittable
{
    [Title("낙하 설정")]
    [SerializeField, LabelText("낙하 속도")]
    private float fallSpeed = 2.5f;
    // 초당 내려가는 거리다. 값이 클수록 타격 타이밍이 빡빡해진다.
    // 스윙은 누른 뒤 약 0.3초 뒤에 판정이 나므로 그만큼 미리 누를 수 있는 속도로 맞춘다.

    [Title("타격 설정")]
    [SerializeField, LabelText("비행 속도")]
    private float flySpeed = 22f;
    // 맞은 뒤 날아가는 속도다. 물리 한 스텝에 매듭 크기보다 멀리 가면 통과할 수 있으니 너무 키우지 않는다.

    [SerializeField, LabelText("비행 제한 시간(초)")]
    private float flyLifetime = 1.2f;
    // 매듭을 빗나간 불씨가 화면 밖으로 끝없이 날아가지 않도록 이 시간이 지나면 꺼진다.

    [Title("연출")]
    [SerializeField, LabelText("불 파티클")]
    private ParticleSystem burningParticle;

    [SerializeField, LabelText("꺼짐 파티클")]
    private ParticleSystem fizzleParticle;

    [SerializeField, LabelText("멈칫 시간(초)")]
    private float hitStopDuration = 0.07f;
    // 맞는 순간 불씨만 잠깐 멈춰 타격감을 준다. Time.timeScale은 일시정지 UI가 쓰므로 건드리지 않는다.

    [SerializeField, LabelText("타격 펀치 세기")]
    private float hitPunch = 0.8f;
    // 맞는 순간 현재 크기 대비 얼마나 부풀었다 돌아올지다.

    [SerializeField, LabelText("소멸 시간")]
    private float vanishDuration = 0.25f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("불붙음")]
    private bool _isLit;

    [ReadOnly, ShowInInspector, LabelText("맞음")]
    private bool _isHit;

    [ReadOnly, ShowInInspector, LabelText("끝남")]
    private bool _isResolved;

    private EmberBattingPuzzle _puzzle;
    private Rigidbody _rb;
    private float _flyEndTime;
    private bool _isFlying;
    private Sequence _hitSequence;
    private Tween _vanishTween;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        // 떨어지는 동안은 MovePosition으로 직접 움직이고, 맞은 뒤에만 속도로 날리기 위해 Kinematic으로 시작한다.

        GetComponent<Collider>().isTrigger = true;
        // 플레이어를 밀지 않고, 매듭과의 접촉은 트리거로 판정한다.
    }

    // 생성 직후 EmberBattingPuzzle이 호출한다. 프리팹은 씬 오브젝트를 직접 참조할 수 없어 이렇게 넘겨받는다.
    public void Init(EmberBattingPuzzle puzzle)
    {
        _puzzle = puzzle;
    }

    private void OnDestroy()
    {
        _hitSequence?.Kill();
        _vanishTween?.Kill();
    }

    private void FixedUpdate()
    {
        if (_isResolved || _puzzle == null) return;

        if (_isFlying)
        {
            // 땅볼은 바닥에 닿으면, 그 외에는 제한 시간이 지나면 꺼진다.
            if (_rb.position.y <= _puzzle.GroundY || Time.time >= _flyEndTime)
                Resolve(fizzle: true);
            return;
        }

        if (_isHit) return;
        // 멈칫 연출 중에는 제자리에 멈춰 있는다.

        // Kinematic Rigidbody는 물리 주기에 맞춰 옮겨야 하므로 fixedDeltaTime으로 초당 이동량을 맞춘다.
        _rb.MovePosition(_rb.position + Vector3.down * (fallSpeed * Time.fixedDeltaTime));

        if (!_isLit && _rb.position.y <= _puzzle.IgniteY)
            Ignite();

        if (_rb.position.y <= _puzzle.GroundY)
            Resolve(fizzle: true);
    }

    private void Ignite()
    {
        _isLit = true;
        if (burningParticle != null) burningParticle.Play();
    }

    // SwordHitbox가 호출한다. 불붙은 불씨만 반응하며, 맞은 높이로 날아갈 목표가 정해진다.
    public void OnHit()
    {
        if (!_isLit || _isHit || _isResolved || _puzzle == null) return;

        _isHit = true;
        Vector3 target = _puzzle.GetTargetForHeight(transform.position);
        _puzzle.PlayHitShake();

        _hitSequence = DOTween.Sequence()
            .Join(transform.DOPunchScale(transform.localScale * hitPunch, hitStopDuration * 2f, 4, 0.5f))
            .InsertCallback(hitStopDuration, () => Launch(target))
            .SetLink(gameObject);
    }

    // 멈칫이 끝나면 목표를 향해 일직선으로 날린다. 포물선보다 어느 높이든 정확히 닿아 조정이 쉽다.
    private void Launch(Vector3 target)
    {
        if (_isResolved) return;

        _isFlying = true;
        _flyEndTime = Time.time + flyLifetime;
        _rb.isKinematic = false;
        _rb.linearVelocity = (target - _rb.position).normalized * flySpeed;
    }

    // 날아가는 중 매듭에 닿으면 불을 옮긴다. 아치와 덩굴도 FlammableObject지만 매듭이 아니면 그냥 통과한다.
    // (아치에 바로 붙으면 매듭 3개를 맞히기도 전에 다리가 쓰러지기 때문이다.)
    private void OnTriggerEnter(Collider other)
    {
        if (!_isFlying || _isResolved) return;

        if (!other.TryGetComponent<FlammableObject>(out var flammable)) return;
        if (!_puzzle.TryIgniteKnot(flammable)) return;

        Resolve(fizzle: false);
    }

    // 성공/실패 공통 마무리다. 파티클을 정리하고 줄어들며 사라진다.
    private void Resolve(bool fizzle)
    {
        _isResolved = true;
        _isFlying = false;

        if (!_rb.isKinematic) _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic = true;

        if (burningParticle != null) burningParticle.Stop();
        if (fizzle && _isLit && fizzleParticle != null) fizzleParticle.Play();

        _hitSequence?.Kill();
        _vanishTween = transform.DOScale(Vector3.zero, vanishDuration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject)
            .OnComplete(() => Destroy(gameObject));
    }

    [Button("타격 테스트 (Play Mode)")]
    private void TestHit()
    {
        if (Application.isPlaying) OnHit();
    }
}
