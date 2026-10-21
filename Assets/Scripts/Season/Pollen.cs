using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 봄 계절 낙하물인 꽃가루다. 좌우로 흔들리며 천천히 떨어지고,
// 선풍기 바람에 날려 보낼 수 있으며(IBlowable), Player에 닿으면 봄 게이지를 올린다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Pollen : BaseHazard, IBlowable
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

    [Title("크기 설정")]
    [SerializeField, LabelText("최소 크기")]
    private float minScale = 0.3f;

    [SerializeField, LabelText("최대 크기")]
    private float maxScale = 0.5f;

    [Title("수명 설정")]
    [SerializeField, LabelText("수명(초)"), Min(0f)]
    private float lifetime = 10f;
    // 생성된 뒤 이 시간이 지나면 작아지며 사라진다. 0이면 수명 없이 파괴 Y 좌표 아래로 떨어질 때까지 남는다.
    // 꽃가루는 트리거라 바닥을 뚫고 계속 내려가므로, 땅속에서 개수 제한을 차지하지 않도록 수명을 둔다.
    // 낙하 속도 × 수명만큼 떨어진 뒤 사라지므로, 스포너 높이에서 바닥 근처까지 내려오는 값으로 맞춘다.

    [SerializeField, LabelText("수명 끝 사라지는 시간"), Min(0.01f)]
    private float lifetimeFadeTime = 0.5f;
    // 수명이 끝났을 때 작아지며 투명해지는 데 걸리는 시간이다.

    [Title("바람에 날림 연출")]
    [SerializeField, LabelText("날아간 뒤 소멸까지 걸리는 시간")]
    private float blownFadeTime = 1.5f;
    // 선풍기에 날린 뒤 소멸까지 걸리는 총 시간이다. 클수록 멀리 날아간다.

    [SerializeField, LabelText("속도 감속률")]
    private float blownShrinkSpeed = 2f;
    // 날아가는 동안 속도가 줄어드는 세기(초당)다. 낮을수록 오래 빠르게 날아간다.

    [Title("바닥 그림자")]
    [InfoBox("사이드뷰에서도 앞뒤(Z)로 움직이므로, 꽃가루가 어디에 떨어질지 바닥 그림자로 보여준다.")]
    [SerializeField, LabelText("그림자 프리팹")]
    private GameObject shadowPrefab;
    // Inspector에서 BlobShadow 프리팹(콜라이더 없는 원형 Quad)을 연결한다. 비워 두면 그림자를 쓰지 않는다.

    [SerializeField, LabelText("바닥 레이어")]
    private LayerMask shadowGroundLayer = ~0;
    // 그림자를 깔 바닥을 찾는 레이캐스트 대상 레이어다. 플레이어 머리 위에도 그림자가 비쳐 "맞는다"는 걸 알 수 있다.

    [SerializeField, LabelText("그림자 표시 최대 높이")]
    private float shadowMaxHeight = 12f;
    // 바닥에서 이 높이보다 높이 있으면 그림자가 거의 보이지 않는다. 스포너 높이 ~ 바닥 거리 정도로 맞춘다.

    [SerializeField, LabelText("그림자 최소 / 최대 크기")]
    private Vector2 shadowSizeRange = new Vector2(0.35f, 0.9f);
    // x: 바닥에 거의 닿았을 때 크기, y: 높이 있을 때 크기. 내려올수록 작고 또렷해져 착지 지점이 좁혀진다.

    [SerializeField, LabelText("그림자 최소 / 최대 진하기")]
    private Vector2 shadowAlphaRange = new Vector2(0.3f, 0.75f);
    // x: 높이 있을 때 불투명도, y: 바닥에 거의 닿았을 때 불투명도(0~1).
    // 최소값을 0보다 크게 둬야 막 생긴 높은 꽃가루의 착지 지점도 미리 보인다.

    [SerializeField, LabelText("그림자 페이드 시간"), Min(0.01f)]
    private float shadowFadeDuration = 0.3f;
    // 생성될 때 그림자가 나타나고, 사라질 때 지워지는 데 걸리는 시간이다.

    [Title("사운드")]
    [SerializeField, LabelText("날아갈 때 효과음")]
    private AudioClip blownSound;

    [SerializeField, LabelText("효과음 볼륨"), Range(0f, 1f)]
    private float soundVolume = 0.8f;

    private MeshRenderer _meshRenderer;
    private Color _originalColor;
    private Rigidbody _rb;
    private float _seed;
    private bool _isBlown;
    private Tween _blowTween;

    private Transform _shadow;
    private Renderer _shadowRenderer;
    private MaterialPropertyBlock _shadowProps;
    // 그림자마다 머티리얼을 복제하지 않고 진하기만 바꾸기 위해 MaterialPropertyBlock을 쓴다.
    private Color _shadowBaseColor;
    private float _shadowFade;
    // 0~1. 등장/소멸 페이드 값이며, 높이에 따른 진하기에 곱해진다.
    private Tween _shadowTween;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer != null)
            // material 접근 시 인스턴스 머티리얼이 생성되어 이 꽃가루만의 색상을 독립적으로 바꿀 수 있다.
            _originalColor = _meshRenderer.material.GetColor("_BaseColor");

        float scale = Random.Range(minScale, maxScale);
        transform.localScale = Vector3.one * scale;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        // 떠다니는 낙하물이라 자체 낙하 속도(fallSpeed)로만 내려가고, 중력의 영향은 받지 않는다.

        GetComponent<Collider>().isTrigger = true;

        _seed = Random.Range(0f, 100f);
        // 여러 꽃가루가 같은 주기로 흔들리지 않도록 개체마다 다른 위상을 준다.
    }

    private void Start()
    {
        if (lifetime > 0f)
            StartCoroutine(LifetimeRoutine());

        CreateShadow();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _blowTween?.Kill();
        _shadowTween?.Kill();

        if (_shadow != null)
            Destroy(_shadow.gameObject);
        // 그림자는 회전하는 꽃가루를 따라 돌지 않도록 자식이 아닌 별도 오브젝트라서 직접 지운다.
    }

    // LateUpdate에서 갱신해 이번 프레임 꽃가루 이동이 끝난 위치 기준으로 그림자를 놓는다.
    private void LateUpdate()
    {
        UpdateShadow();
    }

    // 그림자 프리팹을 씬 루트에 생성하고 서서히 나타나게 한다.
    private void CreateShadow()
    {
        if (shadowPrefab == null) return;

        _shadow = Instantiate(shadowPrefab).transform;
        _shadowRenderer = _shadow.GetComponentInChildren<Renderer>();
        _shadowProps = new MaterialPropertyBlock();
        _shadowBaseColor = _shadowRenderer != null ? _shadowRenderer.sharedMaterial.GetColor(BaseColorId) : Color.black;

        _shadowFade = 0f;
        _shadowTween = DOTween.To(() => _shadowFade, v => _shadowFade = v, 1f, shadowFadeDuration);
        // 생성 순간 그림자가 툭 튀어나오지 않도록 0 → 1로 페이드인한다.

        UpdateShadow();
    }

    // 꽃가루 바로 아래로 레이캐스트해 맞은 바닥 위에 그림자를 깔고, 높이에 따라 크기와 진하기를 바꾼다.
    private void UpdateShadow()
    {
        if (_shadow == null) return;

        bool hasGround = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
            shadowMaxHeight, shadowGroundLayer, QueryTriggerInteraction.Ignore);
        // 꽃가루 자신과 다른 낙하물은 트리거라 Ignore로 건너뛰고, 실제 바닥(또는 플레이어)만 맞춘다.

        _shadow.gameObject.SetActive(hasGround);
        if (!hasGround) return;
        // 낭떠러지 위처럼 아래에 바닥이 없으면 그림자를 숨긴다.

        float closeness = 1f - hit.distance / shadowMaxHeight;
        // 0 = 표시 최대 높이, 1 = 바닥에 닿음. 이 값으로 크기와 진하기를 보간한다.

        _shadow.position = hit.point + hit.normal * 0.02f;
        // 바닥과 같은 높이면 깜빡이므로(Z-fighting) 법선 방향으로 살짝 띄운다.
        _shadow.rotation = Quaternion.FromToRotation(Vector3.back, hit.normal);
        // Quad의 앞면(-Z)이 바닥 법선을 보게 눕혀, 경사면에서도 바닥에 붙어 보이게 한다.
        _shadow.localScale = Vector3.one * Mathf.Lerp(shadowSizeRange.y, shadowSizeRange.x, closeness);

        if (_shadowRenderer != null)
        {
            Color color = _shadowBaseColor;
            color.a = Mathf.Lerp(shadowAlphaRange.x, shadowAlphaRange.y, closeness) * _shadowFade;
            _shadowProps.SetColor(BaseColorId, color);
            _shadowRenderer.SetPropertyBlock(_shadowProps);
        }
    }

    // 꽃가루가 사라지기 시작할 때 그림자도 함께 지운다.
    private void FadeOutShadow(float duration)
    {
        if (_shadow == null) return;

        _shadowTween?.Kill();
        _shadowTween = DOTween.To(() => _shadowFade, v => _shadowFade = v, 0f, duration);
    }

    // 수명이 끝나면 작아지며 사라진다. 선풍기에 날린 꽃가루는 날림 연출이 따로 소멸을 처리하므로 건너뛴다.
    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (_isBlown) yield break;

        // 사라지는 동안 콜라이더를 꺼서, 거의 보이지 않는 꽃가루에 닿아 게이지가 오르거나
        // 선풍기·우산에 다시 반응하는 일이 없게 한다.
        GetComponent<Collider>().enabled = false;
        StartFadeOut(lifetimeFadeTime);
        FadeOutShadow(lifetimeFadeTime);

        yield return new WaitForSeconds(lifetimeFadeTime);
        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (_isBlown) return;
        Float();
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

    // FanTool의 바람 판정에 감지되면 호출된다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (_isBlown) return;

        _isBlown = true;
        _rb.linearVelocity = direction.normalized * force;

        FadeOutShadow(shadowFadeDuration);
        // 바람에 날려 간 꽃가루는 더 이상 떨어지지 않으므로 착지 지점 그림자를 바로 지운다.

        // PlayClipAtPoint를 사용해 오브젝트가 Destroy된 뒤에도 소리가 끝까지 재생된다.
        if (blownSound != null)
            AudioSource.PlayClipAtPoint(blownSound, transform.position, soundVolume);

        StartCoroutine(BlownAwayRoutine());
    }

    // 플레이어와 접촉 시 봄 게이지를 올리기 위해 트리거 콜백을 사용한다.
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (TryBlockByUmbrella(player))
        {
            FadeOutShadow(shadowFadeDuration);
            return; // 우산에 막힘: 게이지 증가 없이 사라짐
        }

        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        Destroy(gameObject);
    }

    private IEnumerator BlownAwayRoutine()
    {
        float timer = 0f;
        bool fadeStarted = false;
        const float fadeStartRatio = 0.5f;
        // 전반부(0~50%)는 원래 크기로 빠르게 날아가고, 후반부부터 서서히 작아지고 투명해진다.
        // 이렇게 하면 더 멀리 날아간 뒤 사라지는 느낌을 준다.

        while (timer < blownFadeTime)
        {
            timer += Time.deltaTime;
            float totalT = timer / blownFadeTime;

            if (!fadeStarted && totalT >= fadeStartRatio)
            {
                fadeStarted = true;
                StartFadeOut(blownFadeTime * (1f - fadeStartRatio));
            }

            // 프레임레이트에 독립적인 속도 감속. MoveTowards로 선형 감속해 자연스럽게 속도가 줄어든다.
            _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, Vector3.zero, blownShrinkSpeed * Time.deltaTime);

            yield return null;
        }

        Destroy(gameObject);
    }

    // 크기를 0으로 줄이고 색상을 투명하게 만드는 DOTween 연출을 시작한다.
    private void StartFadeOut(float duration)
    {
        Sequence seq = DOTween.Sequence();
        seq.Join(transform.DOScale(Vector3.zero, duration));

        if (_meshRenderer != null)
        {
            Color fadeColor = _originalColor;
            fadeColor.a = 0f;
            seq.Join(_meshRenderer.material.DOColor(fadeColor, "_BaseColor", duration));
        }

        _blowTween = seq;
    }

    [Button("바람에 날리기 테스트")]
    private void TestBlow() => OnBlown(Vector3.right, 5f);
}
