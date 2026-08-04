using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 구역 "공중 낙엽 점화" 퍼즐에 쓰이는 낙엽더미다. 성냥(TorchTool)으로 점화한 뒤,
// 다 타기 전에 선풍기(FanTool)로 날려 마른 덩굴벽(FlammableObject)에 닿으면 벽에 불을 옮기고
// 자신은 사라진다(성공). 정해진 시간 안에 벽에 닿지 못하면 스스로 꺼지며 사라진다(실패).
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class LeafMound : MonoBehaviour, IIgnitable, IBlowable
{
    [Title("연소 설정")]
    [SerializeField, LabelText("연소 시간(초)")]
    private float burnDuration = 3f;
    // 점화 후 다 타기까지 걸리는 시간이다. 덩굴벽까지의 거리에 맞춰 씬마다 다르게 조정한다.
    // 이 값이 곧 난이도다 — 점화 후 바로 선풍기로 안 날리면 벽까지 못 갈 정도로 타이트하게 잡는다.

    [SerializeField, LabelText("불 파티클")]
    private ParticleSystem burningParticle;

    [SerializeField, LabelText("꺼짐 파티클")]
    private ParticleSystem fizzleParticle;
    // 시간 초과로 실패했을 때만 재생된다.

    [Title("접촉 판정")]
    [SerializeField, LabelText("접촉 데미지")]
    private float touchDamage = 5f;
    // 타는 동안 플레이어가 맨몸으로 닿았을 때 깎이는 체력이다. 플레이어 체력이 5칸 기준이라 10은
    // 과해서 5로 낮춰뒀다 — 낙엽더미는 도구(성냥/선풍기)로만 안전하게 다뤄야 한다는 의도를 살린다.

    [Title("소멸 연출 설정")]
    [SerializeField, LabelText("소멸 연출 시간")]
    private float fadeDuration = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("타는 중")]
    private bool _isBurning;

    private bool _isResolved;
    private Rigidbody _rb;
    private Coroutine _burnCoroutine;
    private Tween _fadeTween;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        // 점화되어 선풍기에 맞기 전까지는 제자리에 가만히 있어야 하므로 Kinematic으로 고정한다.

        GetComponent<Collider>().isTrigger = true;
        // 플레이어 이동을 막지 않고, 덩굴벽과의 접촉은 트리거로 판정하기 위함이다.
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        if (_burnCoroutine != null)
            StopCoroutine(_burnCoroutine);
    }

    // TorchTool의 SphereCast에 감지되면 호출된다.
    public void OnIgnited()
    {
        if (_isBurning || _isResolved) return;

        _isBurning = true;
        if (burningParticle != null) burningParticle.Play();
        _burnCoroutine = StartCoroutine(BurnRoutine());
    }

    // FanTool의 바람 판정에 감지되면 호출된다. 불이 붙은 상태에서만 날아간다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (!_isBurning || _isResolved) return;

        _rb.isKinematic = false;
        _rb.linearVelocity = direction.normalized * force;
    }

    // 타는 동안 덩굴벽(FlammableObject)에 닿으면 불을 옮기고 성공 처리한다.
    // 덩굴벽이 아니라 플레이어가 맨몸으로 닿은 경우엔 데미지만 주고, 낙엽더미 자체는 계속 탄다
    // (선풍기로 날려야 하는 퍼즐이 데미지 한 번으로 끝나버리지 않도록 소멸시키지 않는다).
    private void OnTriggerEnter(Collider other)
    {
        if (!_isBurning || _isResolved) return;

        FlammableObject wall = other.GetComponent<FlammableObject>();
        if (wall != null)
        {
            wall.OnIgnited();
            Resolve(fizzle: false);
            return;
        }

        if (other.GetComponent<PlayerController>() != null)
            PlayerHealth.Instance?.TakeDamage(touchDamage);
    }

    private IEnumerator BurnRoutine()
    {
        yield return new WaitForSeconds(burnDuration);

        // 시간 안에 벽에 닿지 못했으면 실패 처리한다(이미 성공 처리됐다면 무시).
        if (!_isResolved)
            Resolve(fizzle: true);
    }

    // 성공(벽 점화)/실패(시간 초과) 공통 마무리. 파티클을 정리하고 축소 연출 후 소멸한다.
    private void Resolve(bool fizzle)
    {
        _isResolved = true;
        _isBurning = false;

        if (burningParticle != null) burningParticle.Stop();
        if (fizzle && fizzleParticle != null) fizzleParticle.Play();

        _fadeTween?.Kill();
        _fadeTween = transform.DOScale(Vector3.zero, fadeDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }

    [Button("강제 점화 테스트")]
    private void TestIgnite() => OnIgnited();

    [Button("강제 날리기 테스트")]
    private void TestBlow() => OnBlown(Vector3.right, 5f);
}
