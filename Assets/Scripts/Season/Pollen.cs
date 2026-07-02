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

    [Title("바람에 날림 연출")]
    [SerializeField, LabelText("날아간 뒤 소멸까지 걸리는 시간")]
    private float blownFadeTime = 1.5f;
    // 선풍기에 날린 뒤 소멸까지 걸리는 총 시간이다. 클수록 멀리 날아간다.

    [SerializeField, LabelText("속도 감속률")]
    private float blownShrinkSpeed = 2f;
    // 날아가는 동안 속도가 줄어드는 세기(초당)다. 낮을수록 오래 빠르게 날아간다.

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

    private void OnDestroy()
    {
        _blowTween?.Kill();
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

        // PlayClipAtPoint를 사용해 오브젝트가 Destroy된 뒤에도 소리가 끝까지 재생된다.
        if (blownSound != null)
            AudioSource.PlayClipAtPoint(blownSound, transform.position, soundVolume);

        StartCoroutine(BlownAwayRoutine());
    }

    // 플레이어와 접촉 시 봄 게이지를 올리기 위해 트리거 콜백을 사용한다.
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
            Destroy(gameObject);
        }
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
