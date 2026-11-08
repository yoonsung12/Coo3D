using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹" 여름 패턴(피뢰침 발판)에서 번개를 맞아 충전되는 피뢰침이다.
// 평소에는 땅속(시각 오브젝트를 아래로 내림)에 숨어 있다가 패턴이 Rise()를 부르면 하늘에서 떨어져 땅에 꽂히고,
// 번개가 근처에 떨어지면 Charge()로 영구 충전된다(ResetRod 전까지 유지).
// 루트는 움직이지 않고 자식 Visual만 DOTween으로 오르내린다. 그래야 루트 위치(= 발판 위 기준점)가 흔들리지 않는다.
public class LightningRod : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("시각 오브젝트 (기둥+끝 구)")]
    private Transform visualRoot;
    // 솟고 가라앉을 때 위아래로 움직이는 자식이다. 루트가 아니라 자식을 움직여 기준 위치를 고정한다.

    [SerializeField, LabelText("꼭대기 지점")]
    private Transform topPoint;
    // 번개 선(전류)이 시작되는 위치다. 솟은 상태의 피뢰침 끝에 맞춰 둔다. 움직이지 않는 자식이어야 한다.

    [Title("솟아오름 설정")]
    [SerializeField, LabelText("숨었을 때 내려가는 깊이")]
    private float hiddenOffsetY = 3.5f;
    // 피뢰침 높이보다 크게 잡아야 숨었을 때 지면 아래로 완전히 가려진다.

    [Title("떨어짐 설정")]
    [SerializeField, LabelText("떨어지기 시작하는 높이")]
    private float dropHeight = 14f;
    // 하늘 어디쯤에서 나타나 떨어질지다. 카메라 화면 위쪽 밖에서 시작하도록 충분히 높게 둔다.

    [SerializeField, LabelText("떨어지는 시간")]
    private float dropDuration = 0.45f;
    // 짧을수록 더 세게 내리꽂히는 느낌이 난다.

    [SerializeField, LabelText("떨어지는 Ease")]
    private Ease dropEase = Ease.InQuad;
    // InQuad는 점점 빨라지며 떨어져 땅에 박히는 순간이 강조된다.

    [SerializeField, LabelText("꽂힐 때 찌그러짐 세기")]
    private float landPunch = 0.25f;
    // 땅에 박히는 순간 세로로 살짝 눌렸다 돌아오는 정도다. 0이면 연출 없음.

    [SerializeField, LabelText("꽂히는 소리 (캉!)")]
    private AudioClip landClip;

    [SerializeField, LabelText("꽂히는 소리 볼륨"), Range(0f, 1f)]
    private float landVolume = 1f;

    [SerializeField, LabelText("꽂히는 소리 들리는 최대 거리")]
    private float landSoundMaxDistance = 40f;
    // 3D 소리라 멀리 있는 피뢰침은 작게 들린다. 맵 끝에서도 들리도록 섬 크기보다 크게 둔다.

    [SerializeField, LabelText("가라앉는 시간")]
    private float sinkDuration = 0.5f;

    [Title("충전 연출")]
    [SerializeField, LabelText("충전 색")]
    private Color chargedColor = new Color(1f, 0.9f, 0.2f, 1f);

    [SerializeField, LabelText("충전 발광 세기")]
    private float chargedEmission = 3f;
    // 값이 클수록 충전된 피뢰침이 더 밝게 빛난다.

    [SerializeField, LabelText("충전 펄스 시간")]
    private float pulseDuration = 0.6f;

    [SerializeField, LabelText("충전 순간 크기 튕김")]
    private float chargePunchScale = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("솟아올라 있음")]
    public bool IsRisen { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("충전됨")]
    public bool IsCharged { get; private set; }

    // 번개 선이 시작될 월드 위치다. topPoint가 없으면 루트 위 2.5m로 대신한다.
    public Vector3 TopPosition => topPoint != null ? topPoint.position : transform.position + Vector3.up * 2.5f;

    // 바닥 기준 XZ 위치다. 번개가 떨어진 자리와 거리를 잴 때 쓴다.
    public Vector3 FootprintCenter => new Vector3(transform.position.x, 0f, transform.position.z);

    private Collider[] _colliders;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;
    private Vector3 _shownLocalPosition;
    private Vector3 _shownLocalScale = Vector3.one;
    private Tween _moveTween;
    private Tween _pulseTween;
    private Tween _punchTween;
    private float _pulse01;
    private AudioSource _audio;
    // 꽂히는 소리용 3D AudioSource다. 소리가 지정돼 있을 때만 Awake에서 만든다.

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _block = new MaterialPropertyBlock();
        _shownLocalPosition = visualRoot.localPosition;
        _shownLocalScale = visualRoot.localScale;
        // 충전 튕김 연출 뒤 크기를 되돌릴 기준이다. 시각 오브젝트 크기를 1이 아닌 값으로 바꿔도 깨지지 않게 저장해 둔다.

        if (landClip != null)
        {
            // 피뢰침이 꽂히는 자리에서 소리가 나도록 루트에 3D 소리를 둔다.
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 15f; // 이 거리 안에서는 최대 볼륨으로 들린다. 섬 대부분이 들어오게 넓혀 멀리서도 세게 들리게 한다.
            _audio.maxDistance = landSoundMaxDistance;
            _audio.dopplerLevel = 0f;
        }

        HideImmediate();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    // 연출 없이 즉시 땅속으로 숨긴다. 숨은 동안 콜라이더는 꺼서 플레이어가 걸리지 않게 한다.
    [Button("즉시 숨기기 (테스트)")]
    public void HideImmediate()
    {
        _moveTween?.Kill();
        visualRoot.localPosition = _shownLocalPosition + Vector3.down * hiddenOffsetY;
        IsRisen = false;
        SetCollidersEnabled(false);
    }

    // 하늘에서 떨어져 땅에 꽂힌다. 이름은 패턴 코드가 그대로 부를 수 있게 Rise로 유지한다.
    // 콜라이더는 땅에 꽂힌 뒤에 켠다. 떨어지는 도중에 켜면 공중의 막대가 플레이어를 밀어낼 수 있기 때문이다.
    [Button("떨어뜨리기 (테스트)")]
    public void Rise()
    {
        if (IsRisen) return;
        IsRisen = true;

        _moveTween?.Kill();
        _punchTween?.Kill();
        visualRoot.localScale = _shownLocalScale;
        visualRoot.localPosition = _shownLocalPosition + Vector3.up * dropHeight;

        _moveTween = visualRoot.DOLocalMove(_shownLocalPosition, dropDuration)
            .SetEase(dropEase)
            .OnComplete(OnLanded)
            .SetLink(gameObject);
    }

    // 땅에 박히는 순간: 콜라이더를 켜고, 세로로 눌렸다 돌아오는 연출과 '캉!' 소리를 낸다.
    private void OnLanded()
    {
        SetCollidersEnabled(true);

        if (landPunch > 0f)
        {
            _punchTween = visualRoot.DOPunchScale(new Vector3(landPunch, -landPunch, landPunch), 0.25f, 6, 0.6f)
                .SetLink(gameObject);
        }

        if (_audio != null && landClip != null) _audio.PlayOneShot(landClip, landVolume);
    }

    [Button("가라앉기 (테스트)")]
    public void Sink()
    {
        if (!IsRisen) return;
        IsRisen = false;

        _moveTween?.Kill();
        _moveTween = visualRoot.DOLocalMove(_shownLocalPosition + Vector3.down * hiddenOffsetY, sinkDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => SetCollidersEnabled(false))
            .SetLink(gameObject);
    }

    // 번개를 맞아 충전된다. ResetRod 전까지 풀리지 않는다.
    [Button("충전 (테스트)")]
    public void Charge()
    {
        if (!IsRisen || IsCharged) return;
        IsCharged = true;

        _pulse01 = 0f;
        ApplyChargedLook(0f);

        // 밝기가 계속 오르내려 "충전 완료" 상태가 한눈에 보이게 한다.
        _pulseTween?.Kill();
        _pulseTween = DOTween.To(() => _pulse01, v => { _pulse01 = v; ApplyChargedLook(v); }, 1f, pulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject);

        _punchTween?.Kill();
        visualRoot.localScale = _shownLocalScale;
        _punchTween = visualRoot.DOPunchScale(_shownLocalScale * chargePunchScale, 0.35f, 6, 0.6f)
            .SetLink(gameObject);
    }

    // 충전 상태와 색을 원래대로 되돌린다. 패턴이 중단되거나 끝났을 때 부른다.
    [Button("충전 해제 (테스트)")]
    public void ResetRod()
    {
        IsCharged = false;
        _pulseTween?.Kill();
        _punchTween?.Kill();
        visualRoot.localScale = _shownLocalScale;
        if (_renderers == null) return;
        foreach (Renderer r in _renderers)
        {
            if (r != null) r.SetPropertyBlock(null);
        }
    }

    // t(0~1)에 따라 기본 색에서 충전 색으로 섞고 발광을 준다.
    private void ApplyChargedLook(float t)
    {
        float glow = Mathf.Lerp(chargedEmission * 0.5f, chargedEmission, t);
        foreach (Renderer r in _renderers)
        {
            if (r == null) continue;
            _block.SetColor(BaseColorId, chargedColor);
            _block.SetColor(EmissionColorId, chargedColor * glow);
            r.SetPropertyBlock(_block);
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider col in _colliders)
            col.enabled = enabled;
    }

    private void KillTweens()
    {
        _moveTween?.Kill();
        _pulseTween?.Kill();
        _punchTween?.Kill();
    }
}
