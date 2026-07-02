using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 기본 적 클래스. CharacterBase를 상속받아 칼 히트박스의 타격 대상이 될 수 있다.
// 현재는 타격감 확인을 위해 이동 없이 피격 연출과 체력 감소만 처리한다.
public class Enemy : CharacterBase
{
    [Title("체력 설정")]
    [SerializeField, LabelText("최대 체력")]
    private float maxHealth = 100f;
    // 값을 조절해 적의 최대 체력을 설정한다.

    [ReadOnly, ShowInInspector, LabelText("현재 체력")]
    private float _currentHealth;
    // Play Mode에서 실시간으로 체력 변화를 확인하기 위한 읽기 전용 표시다.

    [Title("피격 연출 설정")]
    [SerializeField, LabelText("피격 플래시 색상")]
    private Color hitFlashColor = Color.white;
    // 피격 순간 이 색으로 번쩍였다가 원래 색으로 돌아온다. 흰색이 기본이다.

    [SerializeField, LabelText("플래시 지속 시간")]
    private float hitFlashDuration = 0.15f;
    // 값이 작을수록 더 날카로운 피격감이 난다.

    [SerializeField, LabelText("진동 강도")]
    private float punchStrength = 0.12f;
    // 피격 시 오브젝트가 좌우로 흔들리는 강도다. 값이 클수록 더 크게 움직인다.

    [SerializeField, LabelText("진동 지속 시간")]
    private float punchDuration = 0.25f;
    // 피격 진동이 사라지는 데 걸리는 시간이다.

    [Title("사운드")]
    [SerializeField, LabelText("피격 사운드 클립")]
    private AudioClip hitClip;
    // 칼에 맞았을 때 재생할 사운드 클립이다. Inspector에서 연결한다.
    // 비워두면 소리 없이 연출만 동작한다.

    [Title("사망 연출 설정")]
    [SerializeField, LabelText("사망 축소 시간")]
    private float deathShrinkDuration = 0.3f;
    // 사망 시 오브젝트가 크기 0으로 줄어드는 데 걸리는 시간이다.

    private MeshRenderer _meshRenderer;
    private Color _originalColor;
    private Vector3 _initialLocalPosition;

    private Tween _flashTween;
    private Tween _punchTween;

    private bool _isDead;
    // 이미 사망 처리된 경우 추가 타격을 무시하기 위한 플래그다.

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        _currentHealth = maxHealth;
        _initialLocalPosition = transform.localPosition;
        // 진동 연출이 중첩될 때 시작 위치가 밀리지 않도록 초기 위치를 기억한다.

        if (_meshRenderer != null)
        {
            // material 접근 시 인스턴스 머티리얼이 생성되어 이 적만의 색상을 독립적으로 변경할 수 있다.
            _originalColor = _meshRenderer.material.GetColor("_BaseColor");
        }
    }

    private void OnDestroy()
    {
        _flashTween?.Kill();
        _punchTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    public override void TakeDamage(float amount)
    {
        // 이미 사망한 경우 타격을 무시한다.
        if (_isDead) return;

        _currentHealth -= amount;
        _currentHealth = Mathf.Max(_currentHealth, 0f);

        PlayHitEffect();

        if (_currentHealth <= 0f)
            Die();
    }

    private void PlayHitEffect()
    {
        // 연속 타격 시 이전 진동 Tween을 중단하고 시작 위치로 되돌린 뒤 새 진동을 시작한다.
        // Kill() 후 localPosition을 직접 리셋하지 않으면 진동 위치가 점점 밀릴 수 있다.
        _punchTween?.Kill();
        transform.localPosition = _initialLocalPosition;
        _punchTween = transform.DOPunchPosition(
            new Vector3(punchStrength, 0f, 0f),
            punchDuration,
            vibrato: 10,
            elasticity: 0.5f
        );

        if (_meshRenderer == null) return;

        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, transform.position);
        // 피격 위치에서 타격음을 재생한다.

        // 피격 순간 플래시 색으로 빠르게 전환 후 원래 색으로 부드럽게 복귀한다.
        _flashTween?.Kill();
        _flashTween = DOTween.Sequence()
            .Append(_meshRenderer.material.DOColor(hitFlashColor, "_BaseColor", hitFlashDuration * 0.3f))
            .Append(_meshRenderer.material.DOColor(_originalColor, "_BaseColor", hitFlashDuration * 0.7f));
    }

    private void Die()
    {
        _isDead = true;

        _flashTween?.Kill();
        _punchTween?.Kill();

        // 크기가 0으로 줄어드는 연출 후 오브젝트를 비활성화한다.
        transform.DOScale(Vector3.zero, deathShrinkDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }

    [Button("데미지 테스트 (20)")]
    private void TestDamage() => TakeDamage(20f);

    [Button("체력 전체 초기화")]
    private void ResetEnemy()
    {
        _isDead = false;
        _currentHealth = maxHealth;
        transform.localScale = Vector3.one;
        transform.localPosition = _initialLocalPosition;

        if (_meshRenderer != null)
            _meshRenderer.material.SetColor("_BaseColor", _originalColor);

        gameObject.SetActive(true);
    }
}
