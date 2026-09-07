using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 계절 낙하물인 은행열매다. 꽃가루(Pollen)와 동일하게 좌우로 흔들리며 천천히 떨어지다가,
// 착지 Y좌표에 도달하면 그 자리에 멈춰 가만히 있는다.
// 착지 후 Player가 접촉하면 가을 게이지가 즉시 오르고, 그 자리에 냄새(GinkgoScent)를 남긴 채 사라진다.
// 착지 전에는 우산으로만 막을 수 있다 — 선풍기 반응(IBlowable)은 의도한 동작이 아니라서 제거함.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class GinkgoFruit : BaseHazard
{
    [Title("떠다니는 움직임 설정")]
    [SerializeField, LabelText("바람에 밀리는 속도")]
    private float windSpeed = 0.6f;
    // 값이 클수록 옆으로 더 빨리 흘러간다.

    [SerializeField, LabelText("좌우 흔들림 속도")]
    private float swaySpeed = 2f;

    [SerializeField, LabelText("좌우 흔들림 폭")]
    private float swayAmount = 0.4f;

    [SerializeField, LabelText("상하 흔들림 폭")]
    private float verticalSwayAmount = 0.15f;

    [SerializeField, LabelText("회전 속도")]
    private float rotateSpeed = 80f;

    [SerializeField, LabelText("낙하 속도")]
    private float fallSpeed = 1.2f;

    [Title("착지 설정")]
    [SerializeField, LabelText("착지 Y좌표")]
    private float landingY = 0.5f;
    // 이 높이까지 떨어지면 은행열매가 멈추고 그 자리에 가만히 있는다. 바닥(Ground) 표면 높이에 맞춘다.

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
    private float _seed;
    private bool _isLanded;
    private bool _hasBurst;
    private Tween _fadeTween;

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
        // 떠다니는 낙하물이라 자체 낙하 속도(fallSpeed)로만 내려가고, 중력의 영향은 받지 않는다.

        GetComponent<Collider>().isTrigger = true;

        _seed = Random.Range(0f, 100f);
        // 여러 은행열매가 같은 주기로 흔들리지 않도록 개체마다 다른 위상을 준다.
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _fadeTween?.Kill();
    }

    private void FixedUpdate()
    {
        if (_isLanded) return;
        Float();

        if (_rb.position.y <= landingY)
            Land();
    }

    private void Float()
    {
        float swayX = Mathf.Sin(Time.fixedTime * swaySpeed + _seed) * swayAmount;
        float swayY = Mathf.Cos(Time.fixedTime * swaySpeed * 0.7f + _seed) * verticalSwayAmount;

        // 사이드뷰(X-Y 평면)라 Z는 건드리지 않고 X(좌우)/Y(상하)로만 떠다닌다.
        Vector3 move = new Vector3(windSpeed + swayX, -fallSpeed + swayY, 0f);

        _rb.MovePosition(_rb.position + move * Time.fixedDeltaTime);
        _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, 0f, rotateSpeed * Time.fixedDeltaTime));
    }

    // 착지 Y좌표에 도달했을 때 호출된다. 위치를 착지 높이에 맞춰 고정하고
    // Rigidbody를 Kinematic으로 바꿔 더 이상 중력/바람의 영향을 받지 않고 가만히 있게 한다.
    private void Land()
    {
        _isLanded = true;

        Vector3 pos = _rb.position;
        pos.y = landingY;
        _rb.position = pos;
        _rb.isKinematic = true;
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
