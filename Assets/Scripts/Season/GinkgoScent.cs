using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 은행열매(GinkgoFruit)가 터질 때 남기는 냄새다. 생성되면 잠깐 퍼지는 연출을 보여준 뒤 그 자리에 머문다.
// Player가 범위 안에 계속 머무르면 일정 간격(틱)마다 가을 게이지가 추가로 오르고,
// 선풍기 바람에 맞으면(IBlowable) 게이지 없이 즉시 흩어져 사라진다.
// 겉모습은 자식(냄새 줄기·가스 뭉게 파티클, 바닥 자국·범위 링)이 담당하고, 이 오브젝트의 공 렌더러는 꺼 둔다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class GinkgoScent : BaseHazard, IBlowable
{
    [Title("냄새 지속 설정")]
    [SerializeField, LabelText("게이지 상승 간격(초)")]
    private float tickInterval = 3f;
    // Player가 냄새 범위 안에 이 시간만큼 머무를 때마다 가을 게이지가 1칸씩 오른다.

    [SerializeField, LabelText("지속 시간(초)")]
    private float lifeTime = 10f;
    // 이 시간이 지나면 Player가 없어도 저절로 흩어져 사라진다.

    [Title("퍼지는 연출 설정")]
    [SerializeField, LabelText("퍼지는 데 걸리는 시간")]
    private float spreadDuration = 0.3f;

    [SerializeField, LabelText("다 퍼졌을 때 크기 배율")]
    private float spreadScale = 2.5f;

    [Title("바람에 흩어지는 연출")]
    [SerializeField, LabelText("흩어지는 데 걸리는 시간")]
    private float disperseFadeTime = 0.4f;

    [Title("냄새 외형 (선택)")]
    [SerializeField, LabelText("냄새 파티클들")]
    private ParticleSystem[] scentParticles;
    // 위로 피어오르는 냄새 줄기·가스 뭉게 파티클을 넣는다. 사라질 때 새로 뿜는 것을 멈추고, 바람을 맞으면 남은 입자를 바람 쪽으로 쓸어 보낸다.

    [SerializeField, LabelText("바닥 표시들")]
    private Renderer[] groundDecals;
    // 바닥에 붙는 으깬 은행 자국·범위 링 Quad를 넣는다. 시작할 때 실제 바닥 높이에 맞춰 붙이고, 사라질 때 투명해진다.

    [SerializeField, LabelText("자연 소멸 페이드 시간")]
    private float naturalFadeTime = 1.2f;
    // 지속 시간이 끝나 저절로 사라질 때 걸리는 시간이다. 바람에 흩어질 때보다 천천히 옅어진다.

    [SerializeField, LabelText("바람에 쓸리는 속도")]
    private float windPushSpeed = 6f;
    // 선풍기에 맞았을 때 남아 있는 냄새 입자에 더해 주는 속도(m/s)다. 클수록 빨리 날아간다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("Player 머문 시간")]
    private float _stayTimer;

    private MeshRenderer _meshRenderer;
    private Color _originalColor;
    private Rigidbody _rb;
    private bool _isDispersed;
    private float _lifeTimer;
    private Tween _scaleTween;

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer != null)
            // material 접근 시 인스턴스 머티리얼이 생성되어 이 냄새만의 색상/투명도를 독립적으로 바꿀 수 있다.
            _originalColor = _meshRenderer.material.GetColor("_BaseColor");

        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;
        // 냄새는 제자리에 머무는 오브젝트라 물리 이동이 필요 없다. Kinematic Rigidbody는
        // CharacterController 등과의 트리거(OnTriggerStay) 감지를 안정적으로 받기 위해 붙여둔다.

        GetComponent<Collider>().isTrigger = true;

        transform.localScale = Vector3.zero;
        _scaleTween = transform.DOScale(spreadScale, spreadDuration).SetEase(Ease.OutQuad);
        // 생성되자마자 0에서 spreadScale까지 커지며 "냄새가 퍼지는" 느낌을 준다.
    }

    private void Start()
    {
        SnapDecalsToGround();
    }

    // 냄새는 터진 은행의 중심(바닥보다 조금 위)에 생기므로, 바닥 표시는 아래로 레이를 쏴 실제 바닥 표면에 붙인다.
    // 바닥에 딱 붙이면 깜빡이므로(Z-fighting) 살짝 띄운다.
    private void SnapDecalsToGround()
    {
        if (groundDecals == null || groundDecals.Length == 0) return;

        // QueryTriggerInteraction.Ignore: 자기 자신(트리거)이나 다른 구역 트리거가 아니라 실제 바닥을 찾는다.
        if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            return;

        // 이 시점에는 퍼지는 연출 때문에 크기가 0에 가까우므로 월드 위치를 바로 넣으면 계산이 틀어진다.
        // 다 퍼졌을 때 크기(spreadScale)를 기준으로 로컬 높이를 계산해 넣는다(바닥 표시는 이 오브젝트의 바로 아래 자식이다).
        for (int i = 0; i < groundDecals.Length; i++)
        {
            if (groundDecals[i] == null) continue;
            Transform decal = groundDecals[i].transform;
            float targetY = hit.point.y + 0.02f + i * 0.005f; // 여러 장이 서로 겹쳐 깜빡이지 않게 조금씩 높이를 다르게 한다.
            Vector3 local = decal.localPosition;
            local.y = (targetY - transform.position.y) / spreadScale;
            decal.localPosition = local;
        }
    }

    private void OnDestroy()
    {
        _scaleTween?.Kill();
    }

    protected override void Update()
    {
        base.Update(); // BaseHazard의 destroyBelowY 자동 파괴 로직을 재사용한다.

        if (_isDispersed) return;

        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= lifeTime)
            Disperse(Vector3.zero, naturalFadeTime);
    }

    // Player가 냄새 범위 안에 머무는 동안 매 프레임 호출된다.
    // tickInterval초가 지날 때마다 가을 게이지를 1칸씩 올린다.
    private void OnTriggerStay(Collider other)
    {
        if (_isDispersed) return;
        if (other.GetComponent<PlayerController>() == null) return;

        _stayTimer += Time.deltaTime;
        if (_stayTimer >= tickInterval)
        {
            _stayTimer -= tickInterval;
            AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        }
    }

    // Player가 범위를 벗어나면 머문 시간을 초기화한다. 다시 들어오면 처음부터 다시 tickInterval을 채워야 한다.
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            _stayTimer = 0f;
    }

    // FanTool의 바람 판정에 감지되면 호출된다. 게이지 피해 없이 그 자리에서 즉시 흩어져 사라진다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (_isDispersed) return;
        Disperse(direction, disperseFadeTime);
    }

    // 더 이상 게이지를 올리지 않도록 멈추고, 옅어지는 연출 후 사라진다.
    // windDirection이 0이 아니면(선풍기) 남은 냄새 입자를 그 방향으로 쓸어 보낸다.
    private void Disperse(Vector3 windDirection, float fadeTime)
    {
        _isDispersed = true;

        _scaleTween?.Kill();
        Sequence seq = DOTween.Sequence();

        // 기존 공 모양 외형을 쓰는 경우에만 커지면서 옅어진다. 새 외형은 공 렌더러를 꺼 두므로 크기를 건드리지 않는다.
        if (_meshRenderer != null && _meshRenderer.enabled)
        {
            seq.Join(transform.DOScale(spreadScale * 1.5f, fadeTime));
            Color fadeColor = _originalColor;
            fadeColor.a = 0f;
            seq.Join(_meshRenderer.material.DOColor(fadeColor, "_BaseColor", fadeTime));
        }

        // 바닥 표시(으깬 자국·범위 링)는 같은 시간 동안 투명해진다.
        if (groundDecals != null)
        {
            foreach (Renderer decal in groundDecals)
            {
                if (decal == null) continue;
                // material 접근 시 인스턴스 머티리얼이 생겨 다른 냄새의 바닥 표시에는 영향을 주지 않는다.
                seq.Join(decal.material.DOFade(0f, "_BaseColor", fadeTime));
            }
        }

        StopScentParticles(windDirection, fadeTime);

        seq.OnComplete(() => Destroy(gameObject));
        seq.SetLink(gameObject);
        _scaleTween = seq;
    }

    // 냄새 파티클이 새로 뿜어지지 않게 멈춘다. 바람이 불었다면 남은 입자를 바람 방향으로 밀고 수명을 페이드 시간에 맞춰 줄인다.
    private void StopScentParticles(Vector3 windDirection, float fadeTime)
    {
        if (scentParticles == null) return;

        Vector3 push = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized * windPushSpeed : Vector3.zero;

        foreach (ParticleSystem ps in scentParticles)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // 남은 입자의 수명을 페이드 시간 안으로 줄여, 오브젝트가 파괴되기 전에 자연스럽게 사라지게 한다.
            var particles = new ParticleSystem.Particle[ps.particleCount];
            int count = ps.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                particles[i].velocity += push;
                particles[i].remainingLifetime = Mathf.Min(particles[i].remainingLifetime, fadeTime);
            }
            ps.SetParticles(particles, count);
        }
    }
}
