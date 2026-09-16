using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 필드에 매달려 있다가 검에 맞으면 떨어지는 고드름이다. 적 머리 위로 떨어지면 즉시 데미지를 주고
// 부서지지만, 빈 바닥에 떨어지면 콜라이더를 계속 켜둔 채로 남아 플레이어가 밟고 올라설 수 있는
// 발판이 된다. 발판이 된 뒤에는 검으로 지정된 횟수만큼 맞아야 부서지며, 맞을 때마다 색이
// 어두워지고 흔들리는 연출로 금이 가는 느낌을 준다.
// Character/Icicle.cs(보스 겨울 패턴 전용, Boss와 강하게 결합됨)와는 별개의 필드용 오브젝트다.
// SwordHitbox와의 트리거 감지가 성립하려면(Unity 트리거 규칙상 최소 한쪽에 Rigidbody 필요)
// Rigidbody가 있어야 한다 — 물리 낙하가 아니라 순수 연출용 이동(DOTween)이라 Kinematic으로 둔다.
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class FallingIcicle : MonoBehaviour, IHittable
{
    private enum IcicleState { Hanging, Falling, Landed, Shattered }

    [Title("낙하 설정")]
    [SerializeField, LabelText("낙하 거리")]
    private float fallDistance = 3f;
    // 매달린 지점에서 바닥까지 떨어지는 거리다.

    [SerializeField, LabelText("낙하 시간")]
    private float fallDuration = 0.35f;

    [SerializeField, LabelText("낙하 Ease")]
    private Ease fallEase = Ease.InQuad;
    // 중력처럼 갈수록 빨라지는 느낌을 주기 위한 기본값이다.

    [Title("적 착지 판정 설정")]
    [SerializeField, LabelText("적 레이어")]
    private LayerMask enemyLayer;
    // Inspector에서 적(Enemy)이 속한 레이어를 지정한다. 착지 지점에 이 레이어의 CharacterBase가
    // 있으면 "머리 위로 떨어졌다"로 판정해 데미지를 주고 즉시 부순다.

    [SerializeField, LabelText("착지 판정 반경")]
    private float landingCheckRadius = 1f;

    [SerializeField, LabelText("낙하 데미지")]
    private float fallDamage = 15f;

    [Title("파괴(금 가기) 설정")]
    [SerializeField, LabelText("파괴에 필요한 타격 횟수")]
    private int requiredBreakHits = 3;
    // 발판이 된 뒤 이 횟수만큼 검에 맞아야 부서진다.

    [SerializeField, LabelText("타격 시 흔들림 세기")]
    private float crackShakeStrength = 0.1f;

    [SerializeField, LabelText("타격 시 흔들림 시간")]
    private float crackShakeDuration = 0.15f;

    [SerializeField, LabelText("금 가는 색상")]
    private Color crackColor = new Color(0.55f, 0.7f, 0.85f);
    // 타격 횟수가 늘수록 원래 색에서 이 색으로 점점 가까워진다("금이 가는" 느낌을 색으로 표현한다).

    [Title("파괴 연출 설정")]
    [SerializeField, LabelText("파괴 스케일 축소 시간")]
    private float shatterDuration = 0.25f;

    [SerializeField, LabelText("파괴 Ease")]
    private Ease shatterEase = Ease.InBack;

    [SerializeField, LabelText("파편 파티클 (선택)")]
    private ParticleSystem shatterParticle;
    // Inspector에서 얼음 파편 파티클을 연결하면 파괴 순간 재생된다. 비워두면 생략된다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    private IcicleState _state = IcicleState.Hanging;

    [ReadOnly, ShowInInspector, LabelText("현재 타격 수")]
    private int _hitCount;

    private Collider _collider;
    private Rigidbody _rb;
    private Renderer _renderer;
    private Color _originalColor;
    private Tween _moveTween;
    private Tween _colorTween;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _rb = GetComponent<Rigidbody>();
        // 실제 3D 모델이 회전 보정을 위해 자식 오브젝트(Visual)에 있을 수 있어서 자식까지 포함해 찾는다.
        _renderer = GetComponentInChildren<Renderer>();
        _rb.isKinematic = true; // 물리 낙하가 아니라 DOTween 연출로만 움직이므로 물리 영향을 받지 않게 한다.

        if (_renderer != null)
            _originalColor = _renderer.material.GetColor("_BaseColor");
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _colorTween?.Kill();
    }

    // 검(SwordHitbox)에 맞으면 현재 상태에 따라 다르게 반응한다.
    public void OnHit()
    {
        switch (_state)
        {
            case IcicleState.Hanging:
                Fall();
                break;
            case IcicleState.Landed:
                RegisterBreakHit();
                break;
            default:
                // 낙하 중이거나 이미 부서진 상태에서는 반응하지 않는다.
                break;
        }
    }

    private void Fall()
    {
        _state = IcicleState.Falling;
        _collider.enabled = false; // 낙하 중에는 추가 타격/충돌 판정에 걸리지 않게 한다.

        _moveTween?.Kill();
        Vector3 target = transform.position + Vector3.down * fallDistance;
        _moveTween = transform.DOMove(target, fallDuration)
            .SetEase(fallEase)
            .OnComplete(HandleLanded);
    }

    // 착지 지점에 적이 있으면 데미지를 주고 즉시 부순다. 없으면 콜라이더를 다시 켜서
    // 플레이어가 밟고 올라설 수 있는 발판으로 남긴다.
    private void HandleLanded()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, landingCheckRadius, enemyLayer);
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<CharacterBase>(out var target))
            {
                target.TakeDamage(fallDamage);
                Shatter();
                return;
            }
        }

        _state = IcicleState.Landed;
        _collider.enabled = true;
    }

    // 발판이 된 뒤 검에 맞을 때마다 호출된다. 금이 가는 연출(흔들림+색 변화)을 보여주고,
    // 지정된 횟수만큼 맞으면 부순다.
    private void RegisterBreakHit()
    {
        _hitCount++;
        PlayCrackEffect();

        if (_hitCount >= requiredBreakHits)
            Shatter();
    }

    private void PlayCrackEffect()
    {
        _moveTween?.Kill();
        _moveTween = transform.DOShakePosition(crackShakeDuration, crackShakeStrength);

        if (_renderer == null) return;

        float ratio = (float)_hitCount / requiredBreakHits;
        _colorTween?.Kill();
        _colorTween = _renderer.material.DOColor(Color.Lerp(_originalColor, crackColor, ratio), "_BaseColor", crackShakeDuration);
    }

    // 콜라이더를 꺼서 더 이상 발판/타격 판정에 걸리지 않게 한 뒤, 스케일을 줄이며 사라진다.
    // IceBlock.Melt()와 동일하게 gameObject 자체는 비활성화하지 않고 렌더러만 꺼서,
    // 파편 파티클(자식일 경우)이 중간에 함께 꺼지지 않고 끝까지 재생되게 한다.
    private void Shatter()
    {
        _state = IcicleState.Shattered;
        _collider.enabled = false;

        if (shatterParticle != null)
            shatterParticle.Play();

        _moveTween?.Kill();
        _moveTween = transform.DOScale(Vector3.zero, shatterDuration)
            .SetEase(shatterEase)
            .OnComplete(() =>
            {
                if (_renderer != null) _renderer.enabled = false;
            });
    }

    [Button("즉시 낙하 테스트")]
    private void TestFall() => Fall();

    [Button("즉시 파괴 테스트")]
    private void TestShatter() => Shatter();
}
