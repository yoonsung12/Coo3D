using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 계절 낙하물인 은행열매다. 진짜 열매처럼 가속하며 똑 떨어지다가,
// 아래에 바닥이 감지되면 그 표면 위에 멈춰 가만히 있는다.
// 착지 후 Player가 접촉하면 가을 게이지가 즉시 오르고, 그 자리에 냄새(GinkgoScent)를 남긴 채 사라진다.
// 착지 전에는 우산으로만 막을 수 있다 — 선풍기 반응(IBlowable)은 의도한 동작이 아니라서 제거함.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class GinkgoFruit : BaseHazard
{
    [Title("낙하 설정")]
    [SerializeField, LabelText("중력 가속도")]
    private float gravity = 20f;
    // 값이 클수록 빨리 가속해서 묵직하게 "똑" 떨어진다.

    [SerializeField, LabelText("최대 낙하 속도")]
    private float maxFallSpeed = 15f;
    // 가속이 이 속도에서 멈춘다. 너무 크면 한 프레임에 바닥을 뚫을 수 있어 아래 바닥 검사 거리에 포함시킨다.

    [Title("착지 설정")]
    [SerializeField, LabelText("바닥 레이어")]
    private LayerMask groundLayer = ~0;
    // 은행이 앉을 수 있는 바닥 레이어. 기본은 전체(Everything).
    // 트리거 콜라이더와 플레이어는 바닥으로 취급하지 않는다.

    [Title("크기 설정")]
    [SerializeField, LabelText("최소 크기")]
    private float minScale = 0.3f;

    [SerializeField, LabelText("최대 크기")]
    private float maxScale = 0.5f;

    [Title("터짐 연출 (착지 후 접촉 시)")]
    [SerializeField, LabelText("냄새 프리팹")]
    private GameObject scentPrefab;
    // Inspector에서 GinkgoScent 컴포넌트가 붙은 프리팹을 연결한다.

    [SerializeField, LabelText("터진 뒤 소멸까지 걸리는 시간")]
    private float burstFadeTime = 0.25f;

    private MeshRenderer _meshRenderer;
    private Color _originalColor;
    private Rigidbody _rb;
    private float _fallSpeed;
    // 현재 낙하 속도. 0에서 시작해 gravity만큼 매 물리 프레임 커진다.
    private float _bottomOffset;
    // 은행 중심에서 바닥면까지의 거리(높이의 절반). 바닥 표면 위에 앉히기 위해 쓴다.
    private float _landedLifetime;
    private bool _isLanded;
    private bool _hasBurst;
    private Tween _fadeTween;
    private Tween _lifetimeTween;

    // 바닥 검사 결과를 담는 버퍼. 매 프레임 배열을 새로 만들지 않도록 모든 은행이 공유한다.
    private static readonly RaycastHit[] _groundHits = new RaycastHit[8];

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer != null)
            // material 접근 시 인스턴스 머티리얼이 생성되어 이 은행열매만의 색상을 독립적으로 바꿀 수 있다.
            _originalColor = _meshRenderer.material.GetColor("_BaseColor");

        float scale = Random.Range(minScale, maxScale);
        transform.localScale = Vector3.one * scale;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        // 중력을 Unity 물리에 맡기면 트리거 콜라이더라 바닥을 통과하므로, 직접 속도를 계산해 내려보낸다.

        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        // 랜덤 크기가 Awake에서 적용된 뒤라 실제 월드 높이의 절반을 정확히 구할 수 있다.
        _bottomOffset = GetComponent<Collider>().bounds.extents.y;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _fadeTween?.Kill();
        _lifetimeTween?.Kill();
    }

    private void FixedUpdate()
    {
        if (_isLanded) return;

        _fallSpeed = Mathf.Min(_fallSpeed + gravity * Time.fixedDeltaTime, maxFallSpeed);
        // 이번 물리 프레임에 내려갈 거리. 프레임 시간을 곱해야 프레임레이트와 무관한 속도가 된다.
        float step = _fallSpeed * Time.fixedDeltaTime;

        if (TryFindGround(step, out float groundY))
        {
            Land(groundY + _bottomOffset);
            return;
        }

        // 사이드뷰라 X와 Z는 그대로 두고 Y만 곧게 내려간다.
        _rb.MovePosition(_rb.position + Vector3.down * step);
    }

    // 이번 프레임 이동 거리(+은행 반높이) 안에 바닥이 있는지 아래로 레이를 쏴서 확인한다.
    // 트리거 콜라이더는 무시하고, 자기 자신/플레이어는 바닥으로 보지 않는다
    // (플레이어 접촉은 OnTriggerEnter가 따로 처리한다).
    private bool TryFindGround(float step, out float groundY)
    {
        groundY = 0f;

        int count = Physics.RaycastNonAlloc(
            _rb.position, Vector3.down, _groundHits,
            step + _bottomOffset, groundLayer, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _groundHits[i];
            if (hit.collider is CharacterController) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                groundY = hit.point.y;
            }
        }

        return nearest < float.MaxValue;
    }

    // 스포너가 생성 직후 호출한다. 0이면 기존처럼 착지 후 영구히 남는다.
    public void SetLandedLifetime(float seconds)
    {
        _landedLifetime = seconds;
    }

    // 바닥을 감지했을 때 호출된다. 위치를 바닥 표면 위에 맞춰 고정하고
    // Rigidbody를 Kinematic으로 바꿔 더 이상 움직이지 않게 한다.
    private void Land(float landY)
    {
        _isLanded = true;

        Vector3 pos = _rb.position;
        pos.y = landY;
        _rb.position = pos;
        _rb.isKinematic = true;

        // 착지 후 소멸 시간이 있으면 그 시간 뒤 사라져 스포너가 계속 새 은행을 떨어뜨릴 수 있게 한다.
        // _hasBurst를 켜서 사라지는 도중에 밟아도 게이지/냄새가 생기지 않게 한다.
        if (_landedLifetime > 0f)
        {
            _lifetimeTween = DOVirtual.DelayedCall(_landedLifetime, () =>
            {
                if (_hasBurst) return;
                _hasBurst = true;
                StartFadeOut(burstFadeTime, () => Destroy(gameObject));
            }, false);
        }
    }

    // 플레이어와 접촉하면 즉시 가을 게이지를 올리고, 그 자리에 냄새를 남긴 채 터진다.
    // 단, 착지 전(공중에서 떨어지는 중)에 우산을 펼친 플레이어와 부딪히면 우산에 막혀 사라진다.
    // 이미 바닥에 착지한 은행열매는 우산으로 막을 수 없다 — 밟으면 기존처럼 그대로 터진다.
    private void OnTriggerEnter(Collider other)
    {
        if (_hasBurst) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (!_isLanded && TryBlockByUmbrella(player)) return; // 공중에서만 우산 차단 적용

        _hasBurst = true;
        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다. 접촉 즉시 1회 오른다.
        Burst();
    }

    // 냄새 프리팹을 남기고 은행열매 자신은 축소+페이드 연출 후 사라진다.
    private void Burst()
    {
        if (scentPrefab != null)
            Instantiate(scentPrefab, transform.position, Quaternion.identity);

        StartFadeOut(burstFadeTime, () => Destroy(gameObject));
    }

    // 게이지/냄새 없이 즉시 사라져야 할 때 호출한다(예: 도화선 에스코트 퍼즐에서 FuseLine을
    // 소화시킨 경우). 소화시킨 자리에 은행열매가 계속 남아있으면 복구된 도화선이 같은 위치에서
    // 다시 부딪혀 무한히 꺼지는 문제가 생기므로, 흔적 없이 바로 사라지게 한다.
    public void ExtinguishAndVanish()
    {
        if (_hasBurst) return;
        _hasBurst = true;
        StartFadeOut(burstFadeTime, () => Destroy(gameObject));
    }

    // 크기를 0으로 줄이고 색상을 투명하게 만드는 DOTween 연출을 시작한다.
    // onComplete가 주어지면 연출이 끝난 뒤 호출한다 (예: 터짐 연출 후 오브젝트 제거).
    private void StartFadeOut(float duration, TweenCallback onComplete = null)
    {
        Sequence seq = DOTween.Sequence();
        seq.Join(transform.DOScale(Vector3.zero, duration));

        if (_meshRenderer != null)
        {
            Color fadeColor = _originalColor;
            fadeColor.a = 0f;
            seq.Join(_meshRenderer.material.DOColor(fadeColor, "_BaseColor", duration));
        }

        if (onComplete != null)
            seq.OnComplete(onComplete);

        _fadeTween = seq;
    }
}
