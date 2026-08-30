using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 보스 봄 패턴(꽃가루 돌진)이 남기는 분진 트레일이다. FlammableObject와 같은 구조로
// 성냥(TorchTool)에 의해 점화되면 BossSpringPattern이 지정한 지연 시간 뒤 폭발 판정을 한다.
// 그 순간 보스가 폭발 반경 안에 있으면 데미지를 주고 패턴을 파훼시키며, 반경 밖이면
// 트레일만 사라지고 보스는 계속 무적 상태로 다음 돌진 사이클을 이어간다.
// 폭발/파훼 관련 수치(데미지, 반경, 지연)는 BossSpringPattern이 Initialize()로 주입한다
// (Boss.TryFireProjectile()이 BossProjectile.Launch()에 수치를 넘기는 것과 동일한 구조) —
// 이 클래스 자신은 점화/확산 판정과 자기 수명 관리만 책임진다.
[RequireComponent(typeof(Collider))]
public class PollenTrail : MonoBehaviour, IIgnitable
{
    [Title("확산 설정")]
    [SerializeField, LabelText("확산 반경")]
    private float spreadRadius = 3f;
    // 폭발 시 이 반경 안의 다른 PollenTrail도 함께 점화된다(FlammableObject.SpreadFire와 동일한 구조).

    [SerializeField, LabelText("확산 감지 레이어")]
    private LayerMask spreadLayer;
    // Inspector에서 PollenTrail이 속한 레이어(Default)를 지정한다.

    [Title("연출")]
    [SerializeField, LabelText("폭발 섬광 색상")]
    private Color flashColor = new Color(1f, 0.95f, 0.8f);
    // 터지는 첫 순간(펀치) 번쩍이는 색상이다. 흰색에 가까울수록 강한 섬광 느낌이 난다.

    [SerializeField, LabelText("폭발 색상")]
    private Color explodeColor = new Color(1f, 0.6f, 0.1f);
    // 섬광 이후 줄어들며 사라지는 동안의 색상이다.

    [SerializeField, LabelText("펀치(순간 확대) 배율")]
    private float burstScaleMultiplier = 1.6f;
    // 터지는 순간 원래 크기의 몇 배까지 순간적으로 커질지 결정한다. 값이 클수록 "펑" 하는 느낌이 강해진다.

    [SerializeField, LabelText("펀치 지속시간")]
    private float burstDuration = 0.08f;
    // 순간적으로 커지는 데 걸리는 시간이다. 짧을수록 더 강하고 날카로운 폭발감이 난다.

    [SerializeField, LabelText("연출 시간")]
    private float visualDuration = 0.3f;
    // 펀치 이후 줄어들며 사라지기까지 걸리는 시간이다.

    [Title("폭발 파티클/카메라 흔들림")]
    [SerializeField, LabelText("폭발 파티클")]
    private ParticleSystem explodeBurstParticle;
    // Inspector에서 자식의 ExplodeBurst(ParticleSystem)를 연결한다. 비워두면 파티클 없이 연출만 재생된다.

    [SerializeField, LabelText("파티클 개수")]
    private int burstParticleCount = 20;

    [SerializeField, LabelText("카메라 흔들림 세기")]
    private float cameraShakeStrength = 0.3f;

    [SerializeField, LabelText("카메라 흔들림 시간")]
    private float cameraShakeDuration = 0.2f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("점화됨")]
    private bool _isIgnited;

    private Boss _boss;
    private float _explosionRadius;
    private float _explosionDamage;
    private float _explodeDelay;

    private MeshRenderer _meshRenderer;
    private Tween _visualTween;

    // BossSpringPattern이 생성 직후 호출해 이 트레일의 폭발 판정 수치를 주입한다.
    public void Initialize(Boss boss, float explosionRadius, float explosionDamage, float explodeDelay)
    {
        _boss = boss;
        _explosionRadius = explosionRadius;
        _explosionDamage = explosionDamage;
        _explodeDelay = explodeDelay;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnDestroy()
    {
        _visualTween?.Kill();
    }

    // TorchTool의 Ignite()에서 SphereCast로 감지되면 호출된다.
    public void OnIgnited()
    {
        if (_isIgnited) return;
        _isIgnited = true;
        StartCoroutine(ExplodeRoutine());
    }

    private IEnumerator ExplodeRoutine()
    {
        yield return new WaitForSeconds(_explodeDelay);

        SpreadFire();

        if (_boss != null && Vector3.Distance(transform.position, _boss.transform.position) <= _explosionRadius)
        {
            _boss.ApplyPatternDamage(_explosionDamage);
            _boss.NotifyPatternSolved();
        }

        PlayExplodeVisual();
    }

    // 확산 반경 안의 다른 PollenTrail에도 불을 옮긴다.
    private void SpreadFire()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, spreadRadius, spreadLayer);

        foreach (Collider col in cols)
        {
            if (col.gameObject == gameObject) continue;
            col.GetComponent<PollenTrail>()?.OnIgnited();
        }
    }

    // 터지는 순간 확 커지며 흰색으로 번쩍인 뒤(펀치), 주황색으로 가라앉으며 줄어들어 사라진다.
    // 여기에 파티클 버스트 + 카메라 흔들림을 더해 실제로 "터지는" 느낌을 낸다.
    private void PlayExplodeVisual()
    {
        _visualTween?.Kill();

        Vector3 baseScale = transform.localScale;

        Sequence seq = DOTween.Sequence();

        if (_meshRenderer != null)
            seq.Join(_meshRenderer.material.DOColor(flashColor, "_BaseColor", burstDuration * 0.5f));
        seq.Join(transform.DOScale(baseScale * burstScaleMultiplier, burstDuration).SetEase(Ease.OutQuad));

        if (_meshRenderer != null)
            seq.Append(_meshRenderer.material.DOColor(explodeColor, "_BaseColor", visualDuration * 0.3f));
        seq.Join(transform.DOScale(Vector3.zero, visualDuration).SetEase(Ease.InBack));

        seq.OnComplete(() => Destroy(gameObject));

        _visualTween = seq;

        explodeBurstParticle?.Emit(burstParticleCount);

        SideViewCamera cam = Camera.main != null ? Camera.main.GetComponent<SideViewCamera>() : null;
        cam?.Shake(cameraShakeDuration, cameraShakeStrength);
    }

    // BossSpringPattern이 트레일 나이가 다 됐을 때(돌진 2회 경과) 외부에서 호출한다.
    // 폭발 연출 없이 조용히 사라진다.
    public void FadeOutAndDestroy()
    {
        if (_isIgnited) return; // 이미 점화/폭발 처리 중이면 그쪽 연출에 맡긴다.
        _isIgnited = true; // 나이 초과로 사라질 때도 더 이상 점화되지 않게 막는다.

        _visualTween?.Kill();
        _visualTween = transform.DOScale(Vector3.zero, visualDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }

    [Button("즉시 점화 테스트")]
    private void TestIgnite() => OnIgnited();
}
