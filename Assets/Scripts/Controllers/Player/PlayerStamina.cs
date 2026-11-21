using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 우산 글라이드에 쓰는 스태미나를 관리한다.
// 소비/재생 규칙만 담당하고, 언제 소비할지는 UmbrellaTool이 결정한다.
// 0이 되면 '소진' 상태가 되며, 최대치의 reuseRatio 이상 회복되어야 다시 사용할 수 있다.
public class PlayerStamina : MonoBehaviour
{
    [Title("스태미나 설정")]
    [SerializeField, LabelText("최대 스태미나")]
    private float maxStamina = 100f;
    // 값이 클수록 우산을 오래 펼 수 있다.

    [SerializeField, LabelText("초당 소비량")]
    private float drainPerSecond = 50f;
    // 최대 글라이드 시간 = 최대 스태미나 / 초당 소비량 (기본 2초).

    [SerializeField, LabelText("초당 회복량")]
    private float regenPerSecond = 50f;

    [SerializeField, LabelText("회복 시작 지연(초)")]
    private float regenDelay = 0.5f;
    // 마지막 소비/정지 이후 이 시간이 지나야 회복이 시작된다.

    [SerializeField, LabelText("재사용 가능 비율")]
    [Range(0f, 1f)]
    private float reuseRatio = 0.3f;
    // 소진된 뒤 최대치의 이 비율 이상 회복되어야 다시 쓸 수 있다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 스태미나")]
    private float _current;

    [ReadOnly, ShowInInspector, LabelText("소진 상태")]
    private bool _isExhausted;

    private float _regenTimer;
    // 마지막 PauseRegen/Drain 이후 경과 시간이다.

    public float Current => _current;
    public float Max => maxStamina;
    public float Normalized => maxStamina > 0f ? _current / maxStamina : 0f;
    public float ReuseRatio => reuseRatio;
    public bool IsExhausted => _isExhausted;
    public bool CanUse => !_isExhausted && _current > 0f;

    // (현재값, 최대값) - 값이 실제로 바뀔 때만 발생한다.
    public event Action<float, float> OnStaminaChanged;
    public event Action OnExhausted;

    private void Awake()
    {
        // 저장하지 않으므로 항상 가득 찬 상태로 시작한다.
        _current = maxStamina;
        _regenTimer = regenDelay;
    }

    private void Update()
    {
        _regenTimer += Time.deltaTime;
        if (_regenTimer < regenDelay || _current >= maxStamina) return;

        SetCurrent(_current + regenPerSecond * Time.deltaTime);

        if (_isExhausted && _current >= maxStamina * reuseRatio)
            _isExhausted = false;
    }

    // 글라이드 중 매 프레임 호출해 스태미나를 깎는다.
    public void Drain(float deltaTime)
    {
        _regenTimer = 0f;
        SetCurrent(_current - drainPerSecond * deltaTime);

        if (_current <= 0f && !_isExhausted)
        {
            _isExhausted = true;
            OnExhausted?.Invoke();
        }
    }

    // 회복 지연 타이머를 처음으로 되돌린다 (우산을 쓰는 동안 회복 방지).
    public void PauseRegen()
    {
        _regenTimer = 0f;
    }

    private void SetCurrent(float value)
    {
        value = Mathf.Clamp(value, 0f, maxStamina);
        if (Mathf.Approximately(value, _current)) return;

        _current = value;
        OnStaminaChanged?.Invoke(_current, maxStamina);
    }

    [Button("소진 테스트")]
    private void TestExhaust()
    {
        if (!Application.isPlaying) return;
        _regenTimer = 0f;
        SetCurrent(0f);
        if (!_isExhausted)
        {
            _isExhausted = true;
            OnExhausted?.Invoke();
        }
    }

    [Button("가득 채우기")]
    private void TestFill()
    {
        if (!Application.isPlaying) return;
        _isExhausted = false;
        SetCurrent(maxStamina);
    }
}
