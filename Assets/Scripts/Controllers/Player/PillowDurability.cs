using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 배게의 내구도를 관리한다.
// 공격할 때마다 내구도가 감소하고, 깃털 아이템으로 회복한다.
// 내구도가 0이 되면 공격력 배율을 낮춰 전투 피드백을 준다.
[RequireComponent(typeof(SwordAttack))]
public class PillowDurability : MonoBehaviour
{
    public static PillowDurability Instance { get; private set; }

    [Title("내구도 설정")]
    [SerializeField, LabelText("최대 내구도")]
    private int maxDurability = 20;
    // Inspector에서 배게 내구도 최대값을 설정한다. 값이 높을수록 오래 사용할 수 있다.

    [SerializeField, LabelText("공격당 내구도 감소량")]
    private int durabilityPerAttack = 1;

    [SerializeField, LabelText("깃털 1개당 회복량")]
    private int durabilityPerFeather = 3;

    [Title("내구도 0 패널티")]
    [SerializeField, LabelText("내구도 소진 시 데미지 배율")]
    [Range(0.1f, 1f)]
    private float depletedDamageMultiplier = 0.5f;
    // 내구도가 0일 때 곱해지는 데미지 배율이다. 0.5이면 공격력이 절반으로 줄어든다.

    [Title("깃털 사용 입력")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다.
    // 2D 원본은 F키였지만, 이 프로젝트는 F키가 배정되어 있지 않아 현재 코드에서 전혀
    // 쓰이지 않는 기존 "Crouch"(C키) 액션을 재사용한다. 새 바인딩을 추가하지 않기 위함이다.

    [Title("현재 상태 (런타임)")]
    [SerializeField, LabelText("현재 내구도"), ReadOnly]
    private int currentDurability;

    [SerializeField, LabelText("내구도 소진 여부"), ReadOnly]
    private bool isDepleted;

    [SerializeField, LabelText("획득한 깃털 수 (누적)"), ReadOnly]
    private int featherCount;
    // 게임 중 누적으로 획득한 깃털 총 수. 통계용이며 소비되지 않는다.

    [SerializeField, LabelText("보유 중인 깃털 수"), ReadOnly]
    private int heldFeatherCount;
    // 현재 사용 가능한 깃털 재고다. 줍으면 증가하고 사용하면 감소한다.

    // 보유 깃털 수가 변경될 때마다 발행된다. FeatherCounterUI가 구독한다.
    public event Action<int> OnFeatherCountChanged;

    // 내구도가 변경될 때마다 발행된다. PillowDurabilityUI가 구독한다.
    // 인자: (currentDurability, maxDurability, isDepleted)
    public event Action<int, int, bool> OnDurabilityChanged;

    public int CurrentDurability => currentDurability;
    public int MaxDurability => maxDurability;
    public bool IsDepleted => isDepleted;
    public int HeldFeatherCount => heldFeatherCount;

    private SwordAttack _swordAttack;
    private InputAction _crouchAction;

    private void Awake()
    {
        Instance = this;

        _swordAttack = GetComponent<SwordAttack>();
        currentDurability = maxDurability;
        featherCount = 0;
        heldFeatherCount = 0;

        if (inputActionAsset != null)
        {
            var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
            _crouchAction = playerMap.FindAction("Crouch", throwIfNotFound: true);
        }
    }

    private void Start()
    {
        // UI가 구독한 뒤 초기값을 받을 수 있도록 Start에서 초기 이벤트를 발행한다.
        OnFeatherCountChanged?.Invoke(heldFeatherCount);
        OnDurabilityChanged?.Invoke(currentDurability, maxDurability, isDepleted);
    }

    private void OnEnable()
    {
        if (_swordAttack != null)
            _swordAttack.OnAttackFired += HandleAttackFired;

        if (_crouchAction != null)
        {
            _crouchAction.Enable();
            _crouchAction.performed += OnCrouchPerformed;
        }
    }

    private void OnDisable()
    {
        if (_swordAttack != null)
            _swordAttack.OnAttackFired -= HandleAttackFired;

        if (_crouchAction != null)
        {
            _crouchAction.performed -= OnCrouchPerformed;
            _crouchAction.Disable();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnCrouchPerformed(InputAction.CallbackContext ctx) => UseFeather();

    // 공격할 때마다 호출된다. 내구도를 1 감소시키고 소진 여부를 갱신한다.
    private void HandleAttackFired()
    {
        if (currentDurability <= 0) return;

        currentDurability -= durabilityPerAttack;
        currentDurability = Mathf.Max(currentDurability, 0);

        bool wasDepleted = isDepleted;
        isDepleted = currentDurability <= 0;

        OnDurabilityChanged?.Invoke(currentDurability, maxDurability, isDepleted);

        if (isDepleted && !wasDepleted)
            PlayDepletedEffect();
    }

    // 내구도를 직접 회복한다. UseFeather()에서 내부적으로 호출한다.
    public void AddDurability(int amount)
    {
        currentDurability = Mathf.Min(currentDurability + amount, maxDurability);

        bool wasDepletedBefore = isDepleted;
        isDepleted = currentDurability <= 0;

        OnDurabilityChanged?.Invoke(currentDurability, maxDurability, isDepleted);
    }

    // 깃털 아이템을 획득할 때 FeatherItem에서 호출된다.
    // 내구도를 즉시 회복하지 않고 보유 재고에 추가한다. Crouch 입력으로 UseFeather()를 호출해 소비한다.
    public void CollectFeather()
    {
        featherCount++;
        heldFeatherCount++;
        OnFeatherCountChanged?.Invoke(heldFeatherCount);
    }

    // 깃털 사용 입력 시 호출된다. 보유 깃털 1개를 소비해 내구도를 회복한다.
    public bool UseFeather()
    {
        if (heldFeatherCount <= 0) return false;
        if (currentDurability >= maxDurability) return false;

        heldFeatherCount--;
        OnFeatherCountChanged?.Invoke(heldFeatherCount);

        AddDurability(durabilityPerFeather);
        PlayUseFeatherEffect();
        return true;
    }

    // 현재 내구도에 따른 데미지 배율을 반환한다. SwordHitbox가 최종 데미지 계산에 사용한다.
    public float GetDamageMultiplier() => isDepleted ? depletedDamageMultiplier : 1f;

    // 내구도 소진 시 시각 피드백. 배게가 헐거워진 느낌을 주기 위해 플레이어를 짧게 흔든다.
    private void PlayDepletedEffect()
    {
        transform.DOShakePosition(0.3f, strength: 0.15f, vibrato: 15).SetRelative(true);
    }

    // 깃털 사용 성공 시 시각 피드백. 솜이 다시 들어차는 느낌을 주기 위해 살짝 위로 튀긴다.
    private void PlayUseFeatherEffect()
    {
        transform.DOPunchPosition(new Vector3(0f, 0.12f, 0f), 0.25f, vibrato: 5, elasticity: 0.5f).SetRelative(true);
    }

    [Button("내구도 초기화")]
    private void TestResetDurability()
    {
        currentDurability = maxDurability;
        isDepleted = false;
        OnDurabilityChanged?.Invoke(currentDurability, maxDurability, isDepleted);
    }

    [Button("깃털 1개 획득 테스트")]
    private void TestCollectFeather() => CollectFeather();

    [Button("깃털 사용 테스트")]
    private void TestUseFeather() => UseFeather();

    [Button("내구도 소진 테스트")]
    private void TestDepleteDurability()
    {
        currentDurability = 0;
        isDepleted = true;
        OnDurabilityChanged?.Invoke(currentDurability, maxDurability, isDepleted);
    }
}
