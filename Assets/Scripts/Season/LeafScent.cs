using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 은행잎(LeafDrop)이 터질 때 남기는 냄새다. 생성되면 잠깐 퍼지는 연출을 보여준 뒤 그 자리에 머문다.
// Player가 범위 안에 계속 머무르면 일정 간격(틱)마다 가을 게이지가 추가로 오르고,
// 선풍기 바람에 맞으면(IBlowable) 게이지 없이 즉시 흩어져 사라진다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class LeafScent : BaseHazard, IBlowable
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
            Disperse();
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
        Disperse();
    }

    // 더 이상 게이지를 올리지 않도록 멈추고, 커지면서 옅어지는 연출 후 사라진다.
    private void Disperse()
    {
        _isDispersed = true;

        _scaleTween?.Kill();
        Sequence seq = DOTween.Sequence();
        seq.Join(transform.DOScale(spreadScale * 1.5f, disperseFadeTime));

        if (_meshRenderer != null)
        {
            Color fadeColor = _originalColor;
            fadeColor.a = 0f;
            seq.Join(_meshRenderer.material.DOColor(fadeColor, "_BaseColor", disperseFadeTime));
        }

        seq.OnComplete(() => Destroy(gameObject));
        _scaleTween = seq;
    }
}
