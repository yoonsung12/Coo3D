using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 여름 패턴(섬 아레나)의 견제용 "번개 기둥" 하나다.
// 이 기둥은 눈에 보이는 위험 표시일 뿐 물리 오브젝트가 아니므로 Rigidbody/Collider를 두지 않는다.
//  - 플레이어가 기둥을 "통과"하며 피해만 받는 방식이다(CharacterController가 막히지 않는다).
//  - 피해는 낙뢰와 같이 수평 거리로 판정한다. 이동도 transform 직접 이동으로 충분하다.
public class LightningPillar : MonoBehaviour
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("기둥 Pivot")]
    private Transform pivot;
    // 등장 시 스케일 Y를 0에서 키운다. 바닥에 닿은 지점(y=0)이 고정되도록 자식 Beam의 부모를 따로 둔 것이다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("기둥 렌더러")]
    private Renderer beamRenderer;
    // 깜빡임/페이드 때 MaterialPropertyBlock으로 알파만 바꾼다(머티리얼 인스턴스를 만들지 않기 위해).

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("빔 Transform (빌보드)")]
    private Transform beamTransform;
    // 빛줄기 쿼드다. 월드 Y축으로만 돌려 항상 카메라 쪽을 보게 한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바닥 빛 렌더러")]
    private Renderer groundGlowRenderer;
    // 바닥에 깔린 빛무리다. 기둥과 같은 알파 흐름(등장/깜빡임/사라짐)을 따라간다.

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("등장 시간")]
    private float appearDuration = 0.15f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("등장 Ease")]
    private Ease appearEase = Ease.OutQuad;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("깜빡임 최소 알파")]
    private float flickerMinAlpha = 0.45f;
    // 작을수록 깜빡임이 크게 보인다.

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("깜빡임 최대 알파")]
    private float flickerMaxAlpha = 0.85f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("깜빡임 주기(편도)")]
    private float flickerDuration = 0.08f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("사라짐 시간")]
    private float vanishDuration = 0.2f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("흐르는 속도")]
    private float scrollSpeed = 1.2f;
    // 빛줄기 텍스처가 아래로 흐르는 속도(초당 UV)다. 클수록 빠르게 쏟아져 내린다.

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("위험 상태")]
    public bool IsActiveHazard { get; private set; }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    private PlayerController _player;
    private CharacterController _playerCc;
    private float _damage;
    private float _hitRadius;
    private bool _hasHit;
    private bool _vanishing;
    private Color _baseColor = Color.white;
    private MaterialPropertyBlock _block;
    private Tween _appearTween;
    private Tween _flickerTween;
    private Tween _vanishTween;
    private Tween _scrollTween;
    private MaterialPropertyBlock _glowBlock;
    private float _scrollOffset;
    private Camera _camera;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _glowBlock = new MaterialPropertyBlock();
        // 기둥이 수십 개 생기므로 매번 찾지 않고 한 번만 캐시한다. 아레나에는 MainCamera 하나뿐이라 Camera.main으로 충분하다.
        _camera = Camera.main;
        if (beamRenderer != null && beamRenderer.sharedMaterial != null && beamRenderer.sharedMaterial.HasProperty(BaseColorId))
            _baseColor = beamRenderer.sharedMaterial.GetColor(BaseColorId);
        if (pivot != null) pivot.localScale = new Vector3(1f, 0f, 1f);
        SetAlpha(0f);
    }

    private void OnDisable() => KillTweens();
    private void OnDestroy() => KillTweens();

    // 탑다운 카메라가 기울어져 있어 평면 쿼드가 납작해 보이므로, 월드 Y축으로만 돌려 항상 카메라를 향하게 한다.
    private void LateUpdate()
    {
        if (beamTransform == null || _camera == null) return;
        Vector3 toCamera = _camera.transform.position - beamTransform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f) return;
        // 쿼드의 앞면은 -Z쪽이므로 카메라 반대 방향을 바라보게 하면 앞면이 카메라를 향한다.
        beamTransform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
    }

    // 패턴이 기둥을 만든 직후 피해량과 판정 반경을 넘겨준다.
    public void Init(PlayerController player, float damage, float hitRadius)
    {
        _player = player;
        _playerCc = player != null ? player.GetComponent<CharacterController>() : null;
        _damage = damage;
        _hitRadius = hitRadius;
    }

    // 바닥(y=0) 기준 수평 위치만 정한다. 패턴이 매 프레임 호출해 기둥을 움직인다.
    public void SetPosition(Vector3 flatPos)
    {
        transform.position = new Vector3(flatPos.x, 0f, flatPos.z);
    }

    // 위에서 꽂히듯 솟아오른 뒤 계속 깜빡이며 위험 상태가 된다.
    public void Appear()
    {
        if (_vanishing) return;
        KillTweens();
        IsActiveHazard = true;
        _hasHit = false;

        SetAlpha(flickerMaxAlpha);
        if (pivot != null)
        {
            pivot.localScale = new Vector3(1f, 0f, 1f);
            _appearTween = pivot.DOScaleY(1f, appearDuration).SetEase(appearEase).SetLink(gameObject);
        }

        // 텍스처를 아래로 계속 흘려 보내 빛이 쏟아지는 느낌을 낸다. 오프셋을 줄여야 그림이 아래로 이동한다.
        _scrollTween = DOTween.To(() => _scrollOffset, v => { _scrollOffset = v; ApplyScroll(v); }, -1f, 1f / Mathf.Max(0.01f, scrollSpeed))
            .SetRelative(false)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetLink(gameObject);

        float alpha = flickerMaxAlpha;
        _flickerTween = DOTween.To(() => alpha, a => { alpha = a; SetAlpha(a); }, flickerMinAlpha, flickerDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    // 알파를 0으로 줄인 뒤 파괴한다. 사라지는 동안에는 더 이상 피해를 주지 않는다.
    public void Vanish()
    {
        if (_vanishing) return;
        _vanishing = true;
        IsActiveHazard = false;
        KillTweens();

        float alpha = _currentAlpha;
        _vanishTween = DOTween.To(() => alpha, a => { alpha = a; SetAlpha(a); }, 0f, vanishDuration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() => { if (this != null) Destroy(gameObject); });
    }

    private void Update()
    {
        if (!IsActiveHazard || _hasHit || _player == null) return;

        // 낙뢰와 같은 기준: 수평 거리로만 판정한다.
        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > _hitRadius) return;

        // 물에 빠진 상태(발이 낮음)에서는 피해를 주지 않는다. 복귀 지점에서 다시 맞는 일을 막는다.
        float feetY = _playerCc != null ? _playerCc.bounds.min.y : _player.transform.position.y;
        if (feetY < -0.5f) return;

        _hasHit = true; // 같은 기둥에는 한 번만 맞는다.
        PlayerHealth.Instance?.TakeDamage(_damage);
    }

    private float _currentAlpha;

    private void SetAlpha(float alpha)
    {
        _currentAlpha = alpha;
        Color c = _baseColor;
        c.a = alpha;
        if (beamRenderer != null)
        {
            // 먼저 읽어 와야 스크롤용 _BaseMap_ST 값이 알파 변경에 지워지지 않는다.
            beamRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            beamRenderer.SetPropertyBlock(_block);
        }
        if (groundGlowRenderer != null)
        {
            groundGlowRenderer.GetPropertyBlock(_glowBlock);
            _glowBlock.SetColor(BaseColorId, c);
            groundGlowRenderer.SetPropertyBlock(_glowBlock);
        }
    }

    // 타일링은 (1,1) 고정, 세로 오프셋만 바꾼다. 텍스처 Wrap이 V Repeat이라 이음매 없이 반복된다.
    private void ApplyScroll(float offsetY)
    {
        if (beamRenderer == null) return;
        beamRenderer.GetPropertyBlock(_block);
        _block.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, offsetY));
        beamRenderer.SetPropertyBlock(_block);
    }

    private void KillTweens()
    {
        _appearTween?.Kill();
        _flickerTween?.Kill();
        _vanishTween?.Kill();
        _scrollTween?.Kill();
        _scrollTween = null;
        _appearTween = null;
        _flickerTween = null;
        _vanishTween = null;
    }
}
