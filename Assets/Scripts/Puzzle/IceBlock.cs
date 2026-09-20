using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 횃불 열기로 녹일 수 있는 얼음 블록이다. IMeltable을 구현해 TorchTool의 열기를 받는다.
// 녹는 것은 계절과 무관하게 언제든 가능하지만, 계절이 겨울로 바뀌면 녹아있던 얼음이 다시 언다.
[RequireComponent(typeof(Collider))]
public class IceBlock : MonoBehaviour, IMeltable
{
    [Title("녹이기 설정")]
    [SerializeField, LabelText("최대 열량")]
    private float maxHeat = 30f;
    // 이 값만큼 열을 누적해서 받으면 완전히 녹는다. 값이 클수록 녹는 데 오래 걸린다.

    [Title("녹는 연출 설정")]
    [SerializeField, LabelText("녹는 연출 시간")]
    private float meltDuration = 0.4f;

    [SerializeField, LabelText("녹는 Ease")]
    private Ease meltEase = Ease.InQuad;
    // 붕괴되듯 무너지는 느낌을 원하면 Ease.InBack으로 바꾸면 좋다. 살짝 팽팽해졌다가 순간적으로 꺼지듯 줄어든다.

    [Title("붕괴 연출 설정")]
    [SerializeField, LabelText("붕괴 전 흔들림 시간")]
    private float collapseShakeDuration = 0.15f;
    // 완전히 녹기 직전, 스케일이 줄어들기 전에 위치/회전이 흔들리는 시간이다.
    // 구조가 버티다가 무너지는 느낌을 주기 위한 값이다.

    [SerializeField, LabelText("흔들림 위치 세기")]
    private float collapseShakeStrength = 0.1f;

    [SerializeField, LabelText("흔들림 회전 세기(도)")]
    private float collapseShakeRotationStrength = 15f;

    [SerializeField, LabelText("파편 파티클(옵션)")]
    private ParticleSystem iceShardParticle;
    // 완전히 녹는 순간 한 번 재생되는 얼음 조각 파티클이다. 비워두면 흔들림+스케일 연출만 재생된다.

    [Title("재생성 연출 설정")]
    [SerializeField, LabelText("재생성 연출 시간")]
    private float regrowDuration = 0.4f;

    [SerializeField, LabelText("재생성 Ease")]
    private Ease regrowEase = Ease.OutBack;

    [Title("중간 단계 연출 설정")]
    [SerializeField, LabelText("크랙 시점 비율"), Range(0f, 1f)]
    private float crackThresholdRatio = 0.5f;
    // 전체 열량(maxHeat) 중 이 비율만큼 쌓이면 "녹고 있다"는 중간 신호를 한 번 재생한다.
    // 기본 0.5면 maxHeat 30 기준으로 열량 15, 즉 초당 10 열량으로 계속 녹일 때 1.5초 지점이다.

    [SerializeField, LabelText("크랙 사운드")]
    private AudioClip crackSound;
    // 중간 단계에 재생할 "쩌적" 크랙 사운드다.

    [SerializeField, LabelText("멜트 사운드")]
    private AudioClip meltSound;
    // 완전히 녹는 순간 재생할 "주르륵" 사운드다.

    [SerializeField, LabelText("물방울 파티클")]
    private ParticleSystem waterDropParticle;
    // 중간 단계에 표면에 물방울이 맺히는 느낌을 주는 파티클이다. 비워두면 사운드만 재생된다.

    [SerializeField, LabelText("연결 AudioSource")]
    private AudioSource audioSource;
    // Inspector에서 이 얼음 오브젝트에 붙인 AudioSource 컴포넌트를 연결한다. 비워두면 사운드가 재생되지 않는다.

    [Title("크랙 연출 설정")]
    [SerializeField, LabelText("크랙 번쩍임 색상")]
    private Color crackFlashColor = Color.white;
    // 크랙 시점에 잠깐 밝아지는 색이다. 빛이 갈라진 틈에 반사되는 느낌을 준다.

    [SerializeField, LabelText("번쩍임 시간")]
    private float crackFlashDuration = 0.15f;

    [SerializeField, LabelText("흔들림 세기")]
    private float crackShakeStrength = 0.05f;

    [SerializeField, LabelText("흔들림 시간")]
    private float crackShakeDuration = 0.25f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 열량")]
    private float _currentHeat;

    [ReadOnly, ShowInInspector, LabelText("녹은 상태")]
    private bool _isMelted;

    [ReadOnly, ShowInInspector, LabelText("중간 단계 재생 여부")]
    private bool _hasCracked;

    private Collider _collider;
    private MeshRenderer _meshRenderer;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Tween _scaleTween;
    private Tween _crackFlashTween;
    private Tween _crackShakeTween;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _originalScale = transform.localScale;

        if (_meshRenderer != null)
            _originalColor = _meshRenderer.material.GetColor("_BaseColor");
        // material 접근 시 인스턴스 머티리얼이 생성되어 크랙 번쩍임 연출이 이 오브젝트에만 적용된다.
    }

    private void OnEnable()
    {
        // 계절이 겨울로 바뀌는 순간을 구독해 녹아있던 얼음을 다시 얼리기 위해 사용한다.
        SeasonManager.OnSeasonChanged += HandleSeasonChanged;
    }

    private void OnDisable()
    {
        SeasonManager.OnSeasonChanged -= HandleSeasonChanged;
    }

    private void OnDestroy()
    {
        _scaleTween?.Kill();
        _crackFlashTween?.Kill();
        _crackShakeTween?.Kill();
    }

    // TorchTool이 녹이기 반경 안에서 매 프레임 호출한다. 계절과 무관하게 언제든 녹을 수 있다.
    public void OnMelted(float heatAmount)
    {
        if (_isMelted) return;

        _currentHeat += heatAmount;

        // 완전히 녹기 전에 절반 정도 녹았다는 걸 먼저 알려주기 위한 중간 신호다.
        // _hasCracked로 한 번만 재생되게 막아 매 프레임 소리가 겹쳐 울리지 않게 한다.
        if (!_hasCracked && _currentHeat >= maxHeat * crackThresholdRatio)
            PlayCrackEffect();

        if (_currentHeat >= maxHeat)
            Melt();
    }

    // 절반 정도 녹았을 때 한 번만 재생되는 중간 단계 연출이다.
    private void PlayCrackEffect()
    {
        _hasCracked = true;

        if (audioSource != null && crackSound != null)
            audioSource.PlayOneShot(crackSound);

        if (waterDropParticle != null)
            waterDropParticle.Play();

        PlayCrackVisual();
    }

    // 표면이 잠깐 밝아졌다가 원래 색으로 돌아오는 번쩍임과 살짝 떨리는 스케일 흔들림으로
    // "금이 갔다"는 순간을 시각적으로 알려준다.
    private void PlayCrackVisual()
    {
        if (_meshRenderer != null)
        {
            _crackFlashTween?.Kill();
            _meshRenderer.material.SetColor("_BaseColor", crackFlashColor);
            _crackFlashTween = _meshRenderer.material
                .DOColor(_originalColor, "_BaseColor", crackFlashDuration)
                .SetEase(Ease.OutQuad);
        }

        _crackShakeTween?.Kill();
        _crackShakeTween = transform.DOShakeScale(crackShakeDuration, crackShakeStrength);
    }

    private void Melt()
    {
        _isMelted = true;

        if (audioSource != null && meltSound != null)
            audioSource.PlayOneShot(meltSound);

        if (iceShardParticle != null)
            iceShardParticle.Play();

        // 매끈하게 줄어들며 사라지는 대신, 짧게 흔들린 뒤 무너지듯 줄어들어 산산히 부서지는 느낌을 준다.
        _scaleTween?.Kill();
        Sequence collapseSequence = DOTween.Sequence();
        collapseSequence.Append(transform.DOShakePosition(collapseShakeDuration, collapseShakeStrength));
        collapseSequence.Join(transform.DOShakeRotation(collapseShakeDuration, collapseShakeRotationStrength));
        collapseSequence.Append(transform.DOScale(Vector3.zero, meltDuration).SetEase(meltEase));
        collapseSequence.OnComplete(() =>
        {
            // Collider와 Renderer만 꺼서 오브젝트 자체는 계속 Active 상태로 유지한다.
            // SetActive(false)를 쓰면 OnDisable이 호출되어 계절 이벤트 구독이 끊기고,
            // 겨울이 다시 와도 재생성 신호를 받지 못하는 문제가 생긴다.
            _collider.enabled = false;
            if (_meshRenderer != null) _meshRenderer.enabled = false;
        });

        _scaleTween = collapseSequence;
    }

    // 계절이 바뀔 때마다 SeasonManager로부터 호출된다.
    // 겨울로 전환됐고 이미 녹아있는 상태일 때만 다시 얼린다.
    private void HandleSeasonChanged(SeasonType newSeason)
    {
        if (newSeason != SeasonType.Winter) return;
        if (!_isMelted) return;

        Regrow();
    }

    private void Regrow()
    {
        _isMelted = false;
        _currentHeat = 0f;
        _hasCracked = false;

        _collider.enabled = true;
        if (_meshRenderer != null) _meshRenderer.enabled = true;

        _scaleTween?.Kill();
        transform.localScale = Vector3.zero;
        _scaleTween = transform.DOScale(_originalScale, regrowDuration).SetEase(regrowEase);
    }

    [Button("중간 단계(크랙) 강제 테스트")]
    private void TestCrack() => PlayCrackEffect();

    [Button("즉시 녹이기 테스트")]
    private void TestMelt() => Melt();

    [Button("즉시 재생성 테스트")]
    private void TestRegrow() => Regrow();
}
