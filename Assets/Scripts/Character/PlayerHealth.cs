using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// Player의 체력을 관리한다. CharacterBase를 상속해 SwordHitbox 같은 기존 타격 판정이
// Enemy와 동일한 방식으로 Player에게도 데미지를 줄 수 있게 한다.
public class PlayerHealth : CharacterBase
{
    // SaveManager, CheckpointTrigger 등 다른 시스템이 GameObject.Find 없이 플레이어 체력에 접근하기 위한 참조다.
    // 씬에 플레이어가 하나뿐이라는 전제하에 자기 자신을 등록하는 방식으로만 제한적으로 사용한다.
    public static PlayerHealth Instance { get; private set; }

    [Title("체력 설정")]
    [SerializeField, LabelText("최대 체력")]
    private float maxHealth = 100f;
    // 값을 조절해 플레이어의 최대 체력을 설정한다.

    [ReadOnly, ShowInInspector, LabelText("현재 체력")]
    private float _currentHealth;
    // Play Mode에서 실시간으로 체력 변화를 확인하기 위한 읽기 전용 표시다.

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => maxHealth;

    [Title("피격 연출 설정")]
    [SerializeField, LabelText("피격 대상 MeshRenderer")]
    private MeshRenderer visualBody;
    // Inspector에서 Player/VisualBody의 MeshRenderer를 연결한다. 피격/사망 시 색상 연출에 사용한다.

    [SerializeField, LabelText("피격 플래시 색상")]
    private Color hitFlashColor = Color.red;

    [SerializeField, LabelText("플래시 지속 시간")]
    private float hitFlashDuration = 0.15f;

    [SerializeField, LabelText("진동 강도")]
    private float punchStrength = 0.12f;

    [SerializeField, LabelText("진동 지속 시간")]
    private float punchDuration = 0.25f;

    [Title("사운드")]
    [SerializeField, LabelText("피격 사운드 클립")]
    private AudioClip hitClip;
    // 비워두면 소리 없이 연출만 동작한다.

    [Title("사망 연출 설정")]
    [SerializeField, LabelText("사망 시 어두워지는 색상")]
    private Color deathDimColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [SerializeField, LabelText("사망 연출 시간")]
    private float deathFadeDuration = 0.6f;

    // 체력이 바뀔 때마다 발행된다. UI(PlayerHealthUI)가 이 이벤트를 구독해 체력바를 갱신한다.
    public event Action<float, float> OnHealthChanged;

    // 체력이 0이 되어 사망했을 때 한 번 발행된다. 리스폰/게임오버 UI 등 후속 기능이 이 이벤트를 구독해 확장할 수 있다.
    public event Action OnDeath;

    private Color _originalColor;
    private Vector3 _initialLocalPosition;
    private PlayerController _playerController;
    private CharacterController _characterController;
    // 체크포인트 리스폰/세이브 로드 시 위치를 안전하게 옮기기 위해 참조해 둔다.

    private Tween _flashTween;
    private Tween _punchTween;

    private bool _isDead;
    // 이미 사망 처리된 경우 추가 타격을 무시하기 위한 플래그다.

    private void Awake()
    {
        Instance = this;

        _playerController = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
        _currentHealth = maxHealth;
        _initialLocalPosition = transform.localPosition;
        // 진동 연출이 중첩될 때 시작 위치가 밀리지 않도록 초기 위치를 기억한다.

        if (visualBody != null)
            // material 접근 시 인스턴스 머티리얼이 생성되어 Player만의 색상을 독립적으로 변경할 수 있다.
            _originalColor = visualBody.material.GetColor("_BaseColor");

        // 아직 체크포인트를 한 번도 지나지 않았다면, 시작 위치를 기본 리스폰 지점으로 등록한다.
        // 이렇게 하지 않으면 첫 체크포인트를 밟기 전에 죽었을 때 월드 원점(0,0,0)으로 리스폰하게 된다.
        if (!CheckpointManager.HasCheckpoint)
            CheckpointManager.SetCheckpoint(transform.position, gameObject.scene.name);
    }

    private void Start()
    {
        // UI가 시작 시점의 체력을 바로 반영할 수 있도록 한 번 이벤트를 발행한다.
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

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
        RaiseDamageTaken(amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        PlayHitEffect();

        if (_currentHealth <= 0f)
            Die();
    }

    private void PlayHitEffect()
    {
        // 연속 타격 시 이전 진동 Tween을 중단하고 시작 위치로 되돌린 뒤 새 진동을 시작한다.
        _punchTween?.Kill();
        transform.localPosition = _initialLocalPosition;
        _punchTween = transform.DOPunchPosition(
            new Vector3(punchStrength, 0f, 0f),
            punchDuration,
            vibrato: 10,
            elasticity: 0.5f
        );

        if (hitClip != null)
            AudioSource.PlayClipAtPoint(hitClip, transform.position);

        if (visualBody == null) return;

        // 피격 순간 플래시 색으로 빠르게 전환 후 원래 색으로 부드럽게 복귀한다.
        _flashTween?.Kill();
        _flashTween = DOTween.Sequence()
            .Append(visualBody.material.DOColor(hitFlashColor, "_BaseColor", hitFlashDuration * 0.3f))
            .Append(visualBody.material.DOColor(_originalColor, "_BaseColor", hitFlashDuration * 0.7f));
    }

    // 체력이 0이 되면 호출된다. 사망 연출 후 체크포인트 위치로 리스폰하고 전체 회복한다.
    // 씬 전환은 하지 않고 현재 씬 안에서만 리스폰한다. 스테이지가 여러 개로 늘어나면
    // SaveManager.LoadGame 쪽 로직으로 확장할 예정이다.
    private void Die()
    {
        _isDead = true;

        _flashTween?.Kill();
        _punchTween?.Kill();

        // PlayerController를 비활성화해 더 이상 이동/점프 입력을 받지 않게 한다.
        if (_playerController != null)
            _playerController.enabled = false;

        OnDeath?.Invoke();

        if (visualBody != null)
            visualBody.material.DOColor(deathDimColor, "_BaseColor", deathFadeDuration)
                .OnComplete(RespawnAtCheckpoint);
        else
            RespawnAtCheckpoint();
    }

    // 사망 연출이 끝난 뒤 호출된다. 체크포인트 위치로 옮기고 체력/조작을 원래대로 되돌린다.
    private void RespawnAtCheckpoint()
    {
        _isDead = false;
        _currentHealth = maxHealth;

        Respawn(CheckpointManager.CurrentCheckpointPosition);

        if (visualBody != null)
            visualBody.material.SetColor("_BaseColor", _originalColor);

        if (_playerController != null)
            _playerController.enabled = true;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // CharacterController를 잠시 꺼서 위치를 옮긴 뒤 다시 켠다.
    // CharacterController는 내부적으로 충돌을 계산하며 이동하므로, 켜진 상태에서 transform.position만
    // 직접 바꾸면 다음 프레임에 충돌 보정이 걸려 순간이동이 의도대로 되지 않을 수 있다.
    // 체크포인트 리스폰, SaveManager.LoadGame 양쪽에서 공통으로 사용한다.
    public void Respawn(Vector3 position)
    {
        if (_characterController != null)
        {
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }
        else
        {
            transform.position = position;
        }
    }

    // 체력을 amount만큼 회복시킨다. 최대 체력을 넘지 않는다. 체크포인트, 회복 아이템 등에서 사용한다.
    public void Heal(float amount)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // 최대 체력을 amount만큼 늘리고, 늘어난 만큼 현재 체력도 함께 채운다. MaxHP 아이템에서 사용한다.
    public void IncreaseMaxHealth(float amount)
    {
        maxHealth += amount;
        _currentHealth += amount;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    [Button("데미지 테스트 (20)")]
    private void TestDamage() => TakeDamage(20f);

    [Button("체력 전체 초기화")]
    private void ResetHealth()
    {
        _isDead = false;
        _currentHealth = maxHealth;
        transform.localPosition = _initialLocalPosition;

        if (visualBody != null)
            visualBody.material.SetColor("_BaseColor", _originalColor);

        if (_playerController != null)
            _playerController.enabled = true;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }
}
