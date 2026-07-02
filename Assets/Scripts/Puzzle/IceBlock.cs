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

    [Title("재생성 연출 설정")]
    [SerializeField, LabelText("재생성 연출 시간")]
    private float regrowDuration = 0.4f;

    [SerializeField, LabelText("재생성 Ease")]
    private Ease regrowEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 열량")]
    private float _currentHeat;

    [ReadOnly, ShowInInspector, LabelText("녹은 상태")]
    private bool _isMelted;

    private Collider _collider;
    private MeshRenderer _meshRenderer;
    private Vector3 _originalScale;
    private Tween _scaleTween;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _originalScale = transform.localScale;
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
    }

    // TorchTool이 녹이기 반경 안에서 매 프레임 호출한다. 계절과 무관하게 언제든 녹을 수 있다.
    public void OnMelted(float heatAmount)
    {
        if (_isMelted) return;

        _currentHeat += heatAmount;
        if (_currentHeat >= maxHeat)
            Melt();
    }

    private void Melt()
    {
        _isMelted = true;

        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(Vector3.zero, meltDuration)
            .SetEase(meltEase)
            .OnComplete(() =>
            {
                // Collider와 Renderer만 꺼서 오브젝트 자체는 계속 Active 상태로 유지한다.
                // SetActive(false)를 쓰면 OnDisable이 호출되어 계절 이벤트 구독이 끊기고,
                // 겨울이 다시 와도 재생성 신호를 받지 못하는 문제가 생긴다.
                _collider.enabled = false;
                if (_meshRenderer != null) _meshRenderer.enabled = false;
            });
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

        _collider.enabled = true;
        if (_meshRenderer != null) _meshRenderer.enabled = true;

        _scaleTween?.Kill();
        transform.localScale = Vector3.zero;
        _scaleTween = transform.DOScale(_originalScale, regrowDuration).SetEase(regrowEase);
    }

    [Button("즉시 녹이기 테스트")]
    private void TestMelt() => Melt();

    [Button("즉시 재생성 테스트")]
    private void TestRegrow() => Regrow();
}
