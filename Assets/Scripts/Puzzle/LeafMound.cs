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

    [Title("낙하 설정")]
    [SerializeField, LabelText("떨어지는 낙엽")]
    private bool isFalling = false;
    // 켜면 위에서 천천히 나풀나풀 떨어지는 낙엽이 된다(FallingLeafSpawner가 생성하는 프리팹용).
    // 끄면 기존처럼 제자리에 떠 있는 낙엽더미로 동작한다.

    [FoldoutGroup("낙하 세부값"), ShowIf("isFalling")]
    [SerializeField, LabelText("낙하 속도")]
    private float fallSpeed = 0.6f;
    // 초당 내려가는 거리다. 값이 작을수록 천천히 떨어져 성냥으로 붙일 시간이 늘어난다.

    [FoldoutGroup("낙하 세부값"), ShowIf("isFalling")]
    [SerializeField, LabelText("좌우 흔들림 폭")]
    private float swayAmount = 0.8f;
    // 좌우로 흔들리는 속도의 크기다. 값이 클수록 옆으로 크게 나풀거린다.

    [FoldoutGroup("낙하 세부값"), ShowIf("isFalling")]
    [SerializeField, LabelText("흔들림 속도")]
    private float swaySpeed = 1.5f;
    // 좌우로 한 번 왕복하는 빠르기다. 값이 클수록 빠르게 팔랑거린다.

    [FoldoutGroup("낙하 세부값"), ShowIf("isFalling")]
    [SerializeField, LabelText("기울기 각도")]
    private float tiltAngle = 25f;
    // 흔들릴 때 좌우로 기울어지는 최대 각도다. 흔들림 방향에 맞춰 기울어 "나풀나풀" 느낌을 낸다.

    [FoldoutGroup("낙하 세부값"), ShowIf("isFalling")]
    [SerializeField, LabelText("착지 높이(Y)")]
    private float landingY = 0.2f;
    // 이 높이까지 내려오면 땅에 닿은 것으로 보고 사라진다. 바닥 높이에 맞춰 조정한다.

    [Title("소멸 연출 설정")]
    [SerializeField, LabelText("소멸 연출 시간")]
    private float fadeDuration = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("타는 중")]
    private bool _isBurning;

    private bool _isResolved;
    private bool _isBlown;
    // 선풍기에 맞아 날아가기 시작했는지 여부다. 날아가는 중엔 낙하/흔들림을 멈춘다.
    private float _seed;
    private Rigidbody _rb;
    private Coroutine _burnCoroutine;
    private Tween _fadeTween;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        // 점화되어 선풍기에 맞기 전까지는 제자리에 가만히 있어야 하므로 Kinematic으로 고정한다.

        _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
        // 사이드뷰 퍼즐이므로 날아가는 동안 회전하거나 깊이(Z) 방향으로 빠지지 않게 고정한다.

        GetComponent<Collider>().isTrigger = true;
        // 플레이어 이동을 막지 않고, 덩굴벽과의 접촉은 트리거로 판정하기 위함이다.

        _seed = Random.Range(0f, 100f);
        // 여러 낙엽이 똑같은 박자로 흔들리지 않도록 개체마다 다른 위상을 준다.
    }

    private void FixedUpdate()
    {
        // Kinematic Rigidbody를 MovePosition으로 옮기므로 물리 주기에 맞춰 FixedUpdate에서 처리한다.
        if (!isFalling || _isBlown || _isResolved) return;

        FallAndSway();

        if (_rb.position.y <= landingY)
            Land();
    }

    // 사인 곡선으로 좌우 속도와 기울기를 함께 바꿔, 좌우로 흔들리며 천천히 내려오는 낙엽 움직임을 만든다.
    private void FallAndSway()
    {
        float wave = Mathf.Sin(Time.fixedTime * swaySpeed + _seed);

        // 사이드뷰(X-Y 평면)라 Z는 건드리지 않는다. fixedDeltaTime을 곱해 초당 이동량으로 맞춘다.
        Vector3 move = new Vector3(wave * swayAmount, -fallSpeed, 0f);
        _rb.MovePosition(_rb.position + move * Time.fixedDeltaTime);

        // 오른쪽으로 흔들릴 때 오른쪽이 내려가도록(Z축 음수 회전) 기울여 잎이 미끄러지듯 떨어지는 느낌을 낸다.
        _rb.MoveRotation(Quaternion.Euler(0f, 0f, -wave * tiltAngle));
    }

    // 땅에 닿았을 때 호출된다. 불붙은 채 떨어졌으면 연기를 내며 꺼지고, 아니면 조용히 사라진다.
    private void Land()
    {
        Resolve(fizzle: _isBurning);
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

    // LeafPile(바닥 낙엽더미)이 가득 찬 상태에서 점화될 때, 그 자리에 생성한 불덩이에 호출한다.
    // 프리팹은 "떨어지는 낙엽" 모드로 저장돼 있으므로 낙하를 끄고 제자리에 뜬 채 타게 만든다.
    public void IgniteAsLaunched()
    {
        isFalling = false;
        OnIgnited();
    }

    // FanTool의 바람 판정에 감지되면 호출된다. 불이 붙은 상태에서만 날아간다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (!_isBurning || _isResolved) return;

        // 선풍기 바람 방향은 마우스 조준 방향이라 위아래(Y) 성분이 섞여 있다. 중력 없는 트리거라
        // 그대로 쓰면 대각선으로 날아가 땅을 뚫거나 하늘로 사라지므로, 좌우(X) 방향만 남겨 수평으로 날린다.
        if (Mathf.Approximately(direction.x, 0f)) return;
        // 바로 위/아래로만 쏜 경우엔 좌우 방향을 정할 수 없으므로 무시한다.
        float xDir = Mathf.Sign(direction.x);

        _isBlown = true;
        _rb.isKinematic = false;
        _rb.linearVelocity = new Vector3(xDir * force, 0f, 0f);
    }

    // 타는 동안 덩굴벽(FlammableObject)에 닿으면 불을 옮기고 성공 처리한다.
    // 덩굴벽이 아니라 플레이어가 맨몸으로 닿은 경우엔 데미지만 주고, 낙엽더미 자체는 계속 탄다
    // (선풍기로 날려야 하는 퍼즐이 데미지 한 번으로 끝나버리지 않도록 소멸시키지 않는다).
    private void OnTriggerEnter(Collider other)
    {
        // 떨어지는 중인 낙엽이 바닥 낙엽더미(LeafPile)에 닿으면 더미에 흡수되어 한 단계 채운다.
        // 불붙은 채 떨어졌다면 그 사실을 넘겨, 더미가 가득 찰 때 불이 옮겨붙게 한다.
        if (isFalling && !_isBlown && !_isResolved)
        {
            LeafPile pile = other.GetComponent<LeafPile>();
            if (pile != null)
            {
                pile.AbsorbLeaf(_isBurning);
                Resolve(fizzle: false);
                return;
            }
        }

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
