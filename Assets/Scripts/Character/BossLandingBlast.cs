using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 보스 봄 패턴의 돌진 착지 폭발이다. BossSpringPattern이 착지 지점에 Instantiate하면
// 스스로 짧은 경고(깜빡임) 후 자동으로 터져서 반경 안 플레이어에게 데미지를 준다.
// 성냥으로 점화해야 하는 PollenTrail과 달리 타이머로만 작동하고, 보스 자신은 다른 레이어(Enemy)라
// hitLayers(Default)에 걸리지 않아 자연스럽게 제외된다.
public class BossLandingBlast : MonoBehaviour
{
    [Title("경고 연출 설정")]
    [SerializeField, LabelText("경고 지속시간")]
    private float warningDuration = 0.4f;
    // 착지 직후 이 시간만큼 경고 연출을 보여준 뒤 실제로 터진다. 플레이어가 반응해서 피할 시간이다.

    [SerializeField, LabelText("경고 깜빡임 주기")]
    private float warningBlinkInterval = 0.1f;

    [SerializeField, LabelText("경고 색상")]
    private Color warningColor = new Color(1f, 0.3f, 0.1f);

    [Title("폭발 설정")]
    [SerializeField, LabelText("폭발 반경")]
    private float explosionRadius = 2.5f;
    // 이 값에 맞춰 시각 오브젝트 크기(X/Y)도 자동으로 조절된다.

    [SerializeField, LabelText("플레이어 데미지")]
    private float damage = 10f;

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player가 속한 레이어(Default)를 지정한다. Boss는 Enemy 레이어라 자동으로 제외된다.

    [Title("폭발 연출 설정")]
    [SerializeField, LabelText("폭발 색상")]
    private Color explodeColor = new Color(1f, 0.6f, 0.1f);

    [SerializeField, LabelText("폭발 연출 시간")]
    private float explodeVisualDuration = 0.25f;

    private MeshRenderer _meshRenderer;
    private Tween _tween;

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();

        // 프리팹에 원래 설정된 Z(두께) 스케일은 유지하고, X/Y만 폭발 반경에 맞춰 지름으로 키운다.
        // 사이드뷰 카메라가 Z축 방향을 바라보고 있어서, X-Y 평면에 놓인 원반이어야 화면에 온전히 보인다.
        float diameter = explosionRadius * 2f;
        Vector3 baseScale = transform.localScale;
        transform.localScale = new Vector3(diameter, diameter, baseScale.z);
    }

    private void Start()
    {
        StartCoroutine(WarningThenExplodeRoutine());
    }

    private void OnDestroy()
    {
        _tween?.Kill();
    }

    private IEnumerator WarningThenExplodeRoutine()
    {
        if (_meshRenderer != null)
        {
            // 원래 색상과 경고 색상 사이를 Yoyo로 왕복시켜 깜빡이는 경고 연출을 만든다.
            _tween = _meshRenderer.material
                .DOColor(warningColor, "_BaseColor", warningBlinkInterval)
                .SetLoops(-1, LoopType.Yoyo);
        }

        yield return new WaitForSeconds(warningDuration);

        Explode();
    }

    // 반경 안의 CharacterBase(플레이어)에게 데미지를 준다.
    private void Explode()
    {
        _tween?.Kill();

        Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius, hitLayers);
        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<CharacterBase>(out var target))
                target.TakeDamage(damage);
        }

        PlayExplodeVisual();
    }

    private void PlayExplodeVisual()
    {
        if (_meshRenderer != null)
            _meshRenderer.material.DOColor(explodeColor, "_BaseColor", explodeVisualDuration * 0.3f);

        _tween = transform.DOScale(transform.localScale * 1.3f, explodeVisualDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => Destroy(gameObject));
    }
}
