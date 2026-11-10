using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// Enemy health, damage response, and death presentation.
// The red hit color is applied only to this enemy's visual renderers.
public class Enemy : CharacterBase
{
    [Title("체력 설정")]
    [SerializeField, LabelText("최대 체력")]
    private float maxHealth = 100f;

    [ReadOnly, ShowInInspector, LabelText("현재 체력")]
    private float _currentHealth;

    [Title("피격 연출 대상")]
    [SerializeField, LabelText("Visual 자식 오브젝트")]
    private Transform visualBody;

    [Title("피격 연출 설정")]
    [SerializeField, LabelText("빨간색 머티리얼")]
    private Material solidRedMaterial;

    [SerializeField, LabelText("빨간색 유지 시간"), Min(0.01f)]
    private float hitRedDuration = 0.15f;

    [SerializeField, LabelText("비활성 자식 렌더러 포함")]
    private bool includeInactiveRenderers = false;

    [SerializeField, LabelText("진동 강도")]
    private float punchStrength = 0.12f;

    [SerializeField, LabelText("진동 지속 시간")]
    private float punchDuration = 0.25f;

    [Title("사운드")]
    [SerializeField, LabelText("피격 사운드 클립")]
    private AudioClip hitClip;

    [Title("사망 연출 설정")]
    [SerializeField, LabelText("사망 축소 시간")]
    private float deathShrinkDuration = 0.3f;

    private Vector3 _initialLocalPosition;
    private Vector3 _initialLocalScale;
    private Renderer[] _visualRenderers = Array.Empty<Renderer>();
    private Material[][] _originalMaterials = Array.Empty<Material[]>();
    private Tween _redTween;
    private Tween _punchTween;
    private Tween _deathTween;
    private EnemyAnimationController enemyAnim;
    private bool _isDead;

    public bool IsDead => _isDead;
    public float HealthRatio => maxHealth > 0f ? _currentHealth / maxHealth : 0f;
    public float MaxHealth => maxHealth;
    public event Action OnDied;

    private void Awake()
    {
        _currentHealth = maxHealth;
        enemyAnim = GetComponent<EnemyAnimationController>();

        if (visualBody != null)
        {
            _initialLocalPosition = visualBody.localPosition;
            _initialLocalScale = visualBody.localScale;
        }
        CacheVisualRenderers();
    }

    private void CacheVisualRenderers()
    {
        if (visualBody == null)
        {
            Debug.LogWarning("[Enemy] Visual Body가 연결되지 않았습니다.", this);
            return;
        }

        // Both MeshRenderer and SkinnedMeshRenderer are included.
        _visualRenderers = visualBody.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        _originalMaterials = new Material[_visualRenderers.Length][];
        for (int i = 0; i < _visualRenderers.Length; i++)
        {
            _originalMaterials[i] = _visualRenderers[i].sharedMaterials;
        }
    }

    private void OnDisable()
    {
        _redTween?.Kill();
        _punchTween?.Kill();
        _deathTween?.Kill();
        RestoreMaterials();
    }

    private void OnDestroy()
    {
        _redTween?.Kill();
        _punchTween?.Kill();
        _deathTween?.Kill();
        RestoreMaterials();
    }

    public override void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0f) return;

        float actualDamage = Mathf.Min(amount, _currentHealth);
        _currentHealth = Mathf.Max(0f, _currentHealth - amount);

        // Preserve the AI/statistics event.
        RaiseDamageTaken(actualDamage);
        PlayHitEffect();

        if (_currentHealth <= 0f)
            Die();
        else
            enemyAnim?.PlayHit();
    }

    private void PlayHitEffect()
    {
        if (visualBody != null)
        {
            _punchTween?.Kill();
            visualBody.localPosition = _initialLocalPosition;
            _punchTween = visualBody.DOPunchPosition(
                new Vector3(punchStrength, 0f, 0f),
                punchDuration,
                vibrato: 10,
                elasticity: 0.5f
            );
        }

        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, transform.position);

        PlayRedHit();
    }

    // Replace the visual materials briefly: this makes even textured/gray
    // feathers and body parts appear solid red rather than red-tinted.
    private void PlayRedHit()
    {
        if (solidRedMaterial == null)
        {
            Debug.LogWarning("[Enemy] Solid Red Material을 연결하세요.", this);
            return;
        }
        if (_visualRenderers.Length == 0)
        {
            Debug.LogWarning("[Enemy] Visual Body 아래 Renderer를 찾지 못했습니다.", this);
            return;
        }

        _redTween?.Kill();
        RestoreMaterials();

        for (int i = 0; i < _visualRenderers.Length; i++)
        {
            Renderer renderer = _visualRenderers[i];
            if (renderer == null) continue;

            int count = _originalMaterials[i].Length;
            if (count == 0) continue;
            Material[] redMaterials = new Material[count];
            for (int j = 0; j < count; j++)
                redMaterials[j] = solidRedMaterial;
            renderer.sharedMaterials = redMaterials;
        }

        // Independent of Hit Stop (Time.timeScale).
        _redTween = DOVirtual.DelayedCall(hitRedDuration, RestoreMaterials, true);
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < _visualRenderers.Length; i++)
        {
            if (_visualRenderers[i] != null && i < _originalMaterials.Length)
                _visualRenderers[i].sharedMaterials = _originalMaterials[i];
        }
    }

    protected virtual bool PersistsDefeat => true;

    private void Start()
    {
        if (PersistsDefeat && SaveManager.Instance != null &&
            SaveManager.Instance.IsEnemyDefeated(SaveManager.GetObjectId(gameObject)))
        {
            _isDead = true;
            gameObject.SetActive(false);
        }
    }

    private void Die()
    {
        _isDead = true;
        _punchTween?.Kill();

        if (PersistsDefeat && SaveManager.Instance != null)
            SaveManager.Instance.MarkEnemyDefeated(SaveManager.GetObjectId(gameObject));

        OnDied?.Invoke();

        if (visualBody != null)
        {
            _deathTween?.Kill();
            _deathTween = visualBody.DOScale(Vector3.zero, deathShrinkDuration)
                .SetEase(Ease.InBack)
                .SetLink(gameObject)
                .OnComplete(() => gameObject.SetActive(false));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    [Button("데미지 테스트 (20)")]
    private void TestDamage() => TakeDamage(20f);

    [Button("빨간색 단독 테스트 (Play Mode)")]
    private void TestRedHit()
    {
        if (!Application.isPlaying) return;
        PlayRedHit();
    }

    [Button("체력 전체 초기화")]
    private void ResetEnemy()
    {
        _redTween?.Kill();
        _punchTween?.Kill();
        _deathTween?.Kill();
        RestoreMaterials();

        _isDead = false;
        _currentHealth = maxHealth;
        if (visualBody != null)
        {
            visualBody.localScale = _initialLocalScale;
            visualBody.localPosition = _initialLocalPosition;
        }
        gameObject.SetActive(true);
    }
}
