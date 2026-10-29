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

    [FoldoutGroup("위에서부터 녹는 연출")]
    [InfoBox("켜면 열량이 쌓이는 만큼 얼음이 위에서부터 낮아지고, 다 녹으면 남은 덩어리가 주저앉으며 사라진다.\n" +
             "얼음은 똑바로 세워져 있어야 하고(기울이지 않기), 콜라이더는 다 녹을 때까지 원래 크기를 유지한다.")]
    [SerializeField, LabelText("위에서부터 녹기")]
    private bool meltFromTop;
    // 끄면 기존처럼 다 녹는 순간 한 번에 줄어들며 사라진다. 다른 씬의 횃불 얼음은 꺼 둔 상태로 쓴다.

    [FoldoutGroup("위에서부터 녹는 연출")]
    [SerializeField, LabelText("최소 남는 높이 비율"), Range(0.05f, 0.5f), ShowIf(nameof(meltFromTop))]
    private float minHeightRatio = 0.15f;
    // 열량이 다 찼을 때 남는 덩어리 높이(원래 높이 대비)다. 이 덩어리가 마지막에 주저앉으며 사라진다.

    [FoldoutGroup("위에서부터 녹는 연출")]
    [SerializeField, LabelText("주저앉는 시간"), ShowIf(nameof(meltFromTop))]
    private float collapseDuration = 0.6f;
    // 남은 덩어리가 바닥으로 꺼지듯 사라지는 데 걸리는 시간이다.

    [FoldoutGroup("위에서부터 녹는 연출")]
    [SerializeField, LabelText("주저앉는 Ease"), ShowIf(nameof(meltFromTop))]
    private Ease collapseEase = Ease.InQuad;

    [FoldoutGroup("위에서부터 녹는 연출")]
    [SerializeField, LabelText("물웅덩이(옵션)"), ShowIf(nameof(meltFromTop))]
    private Transform puddle;
    // 녹는 동안 바닥에 퍼지는 물웅덩이다. 얼음의 자식으로 두면 얼음 크기에 같이 줄어드므로 얼음 옆(형제)에 둔다. 비워두면 웅덩이 없이 녹는다.

    [FoldoutGroup("위에서부터 녹는 연출")]
    [SerializeField, LabelText("물웅덩이 최대 크기"), ShowIf(nameof(meltFromTop))]
    private Vector3 puddleMaxScale = Vector3.one;
    // 완전히 녹았을 때 웅덩이의 localScale이다. 녹는 정도에 비례해 0에서 이 크기까지 커진다.

    [FoldoutGroup("위에서부터 녹는 연출")]
    [SerializeField, LabelText("웅덩이 퍼지는 시간"), ShowIf(nameof(meltFromTop))]
    private float puddleSpreadDuration = 0.8f;
    // 다 녹은 뒤 웅덩이가 최대 크기까지 마저 퍼지는 시간이다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 열량")]
    private float _currentHeat;

    [ReadOnly, ShowInInspector, LabelText("녹은 상태")]
    private bool _isMelted;

    [ReadOnly, ShowInInspector, LabelText("중간 단계 재생 여부")]
    private bool _hasCracked;

    public bool IsMelted => _isMelted;
    // 다른 오브젝트(예: 얼음에 갇힌 고드름)가 이 얼음이 녹았는지 확인하는 용도다. 읽기 전용이다.

    private Collider _collider;
    private MeshRenderer _meshRenderer;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Tween _scaleTween;
    private Tween _crackFlashTween;
    private Tween _crackShakeTween;

    // ---- 위에서부터 녹기용 원래 상태 ----
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private BoxCollider _boxCollider;
    private Vector3 _originalColliderCenter;
    private Vector3 _originalColliderSize;
    private float _meshBottomLocalY;
    // 메쉬 바닥의 로컬 Y다. 높이를 줄여도 바닥이 제자리에 붙어 있도록 위치를 보정할 때 쓴다.

    private float _heightRatio = 1f;
    // 지금 얼음 높이 비율(1=원래 높이)이다.

    private Tween _puddleTween;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _originalScale = transform.localScale;
        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;

        _boxCollider = _collider as BoxCollider;
        if (_boxCollider != null)
        {
            _originalColliderCenter = _boxCollider.center;
            _originalColliderSize = _boxCollider.size;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        _meshBottomLocalY = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.bounds.min.y : -0.5f;

        if (meltFromTop && puddle != null)
        {
            puddle.localScale = Vector3.zero;
            puddle.gameObject.SetActive(false);
            // 처음에는 웅덩이가 보이지 않다가, 녹기 시작하면 켜져서 퍼진다.
        }

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
        _puddleTween?.Kill();
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

        // 위에서부터 녹기: 쌓인 열량 비율만큼 바로 높이를 낮추고 웅덩이를 넓힌다.
        // 열이 매 프레임 조금씩 들어오므로 Tween을 매번 만들지 않고 값을 직접 반영한다.
        if (meltFromTop)
            ApplyMeltProgress(Mathf.Clamp01(_currentHeat / maxHeat));

        if (_currentHeat >= maxHeat)
            Melt();
    }

    // 녹은 정도(0=안 녹음, 1=열량이 다 참)를 얼음 높이와 웅덩이 크기에 반영한다.
    private void ApplyMeltProgress(float progress)
    {
        SetHeightRatio(Mathf.Lerp(1f, minHeightRatio, progress));

        if (puddle != null)
        {
            if (!puddle.gameObject.activeSelf) puddle.gameObject.SetActive(true);
            puddle.localScale = puddleMaxScale * (progress * 0.7f);
            // 녹는 동안은 최대 크기의 70%까지만 퍼지고, 다 녹은 뒤 나머지가 마저 퍼진다.
        }
    }

    // 얼음 높이를 원래 높이의 ratio배로 바꾼다. 바닥은 제자리에 붙어 있고 윗면만 내려온다.
    // 콜라이더는 다 녹을 때까지 원래 월드 크기를 유지해, 반쯤 녹았을 때 넘어가 버리지 않게 한다.
    private void SetHeightRatio(float ratio)
    {
        _heightRatio = Mathf.Max(ratio, 0.001f);
        // 0으로 나누지 않도록 아주 작은 값까지만 내린다.

        Vector3 scale = _originalScale;
        scale.y = _originalScale.y * _heightRatio;
        transform.localScale = scale;

        // 높이를 줄이면 피벗(보통 가운데)을 기준으로 위아래가 같이 줄어드므로,
        // 메쉬 바닥이 원래 바닥 높이에 그대로 오도록 위치를 내려 준다.
        float bottomY = _originalLocalPosition.y + _originalScale.y * _meshBottomLocalY;
        Vector3 position = _originalLocalPosition;
        position.y = bottomY - scale.y * _meshBottomLocalY;
        transform.localPosition = position;

        if (_boxCollider != null)
        {
            // 콜라이더는 로컬 크기가 오브젝트 스케일에 곱해지므로, 줄어든 비율만큼 거꾸로 키워 월드 크기를 유지한다.
            float originalCenterY = _originalLocalPosition.y + _originalScale.y * _originalColliderCenter.y;
            Vector3 center = _originalColliderCenter;
            center.y = (originalCenterY - position.y) / scale.y;
            _boxCollider.center = center;

            Vector3 size = _originalColliderSize;
            size.y = _originalColliderSize.y / _heightRatio;
            _boxCollider.size = size;
        }
    }

    // 높이와 콜라이더를 원래 상태로 정확히 되돌린다. 다시 얼 때 사용한다.
    private void RestoreOriginalShape()
    {
        _heightRatio = 1f;
        transform.localScale = _originalScale;
        transform.localPosition = _originalLocalPosition;
        transform.localRotation = _originalLocalRotation;

        if (_boxCollider != null)
        {
            _boxCollider.center = _originalColliderCenter;
            _boxCollider.size = _originalColliderSize;
        }
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

        // 위에서부터 녹는 중에는 매 프레임 크기를 직접 정하므로, 크기를 흔드는 Tween과 서로 덮어쓰지 않게 건너뛴다.
        if (meltFromTop) return;

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

        if (meltFromTop)
        {
            PlayCollapseFromTop();
            return;
        }

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

    // 위에서부터 녹기 모드의 마지막 단계다. 남은 덩어리가 흔들림 없이 그대로 바닥으로 꺼지듯 사라지고,
    // 그동안 물웅덩이는 최대 크기까지 마저 퍼진다.
    // (흔들림을 넣으면 천천히 녹던 흐름이 끝에서 한 번 움찔거려 어색하므로 높이만 이어서 줄인다.)
    private void PlayCollapseFromTop()
    {
        _scaleTween?.Kill();
        Sequence collapseSequence = DOTween.Sequence();
        collapseSequence.Append(DOTween.To(() => _heightRatio, SetHeightRatio, 0f, collapseDuration).SetEase(collapseEase));
        collapseSequence.OnComplete(() =>
        {
            // 기존 방식과 같은 이유로 SetActive(false) 대신 Collider/Renderer만 끈다(계절 이벤트 구독 유지).
            _collider.enabled = false;
            if (_meshRenderer != null) _meshRenderer.enabled = false;
        });
        _scaleTween = collapseSequence;

        if (puddle != null)
        {
            puddle.gameObject.SetActive(true);
            _puddleTween?.Kill();
            _puddleTween = puddle.DOScale(puddleMaxScale, puddleSpreadDuration).SetEase(Ease.OutQuad);
        }
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

        if (meltFromTop)
        {
            // 위에서부터 녹은 얼음은 위치·콜라이더까지 바뀌어 있으므로, 원래 모양으로 먼저 되돌린 뒤 기존처럼 자라나게 한다.
            RestoreOriginalShape();

            if (puddle != null)
            {
                _puddleTween?.Kill();
                _puddleTween = puddle.DOScale(Vector3.zero, regrowDuration)
                    .OnComplete(() => puddle.gameObject.SetActive(false));
                // 얼음이 다시 생기면 웅덩이는 줄어들며 사라진다.
            }
        }

        transform.localScale = Vector3.zero;
        _scaleTween = transform.DOScale(_originalScale, regrowDuration).SetEase(regrowEase);
    }

    [Button("중간 단계(크랙) 강제 테스트")]
    private void TestCrack() => PlayCrackEffect();

    [Button("즉시 녹이기 테스트")]
    private void TestMelt() => Melt();

    [Button("즉시 재생성 테스트")]
    private void TestRegrow() => Regrow();

    [FoldoutGroup("위에서부터 녹는 연출")]
    [Button("녹는 정도 미리보기 (Play Mode)"), ShowIf(nameof(meltFromTop))]
    private void TestMeltProgress(float progress = 0.5f)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[IceBlock] Play Mode에서만 테스트할 수 있습니다."); return; }
        if (_isMelted) return;
        progress = Mathf.Clamp01(progress);
        // 열량을 직접 정해 그 정도만큼 녹은 모습을 바로 확인한다. 1을 넣으면 마지막 주저앉는 연출까지 재생된다.
        _currentHeat = maxHeat * progress;
        ApplyMeltProgress(progress);
        if (progress >= 1f) Melt();
    }
}
