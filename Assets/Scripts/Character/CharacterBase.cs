
using System;
using UnityEngine;

// Player와 Enemy가 공통으로 상속받는 캐릭터 기반 클래스
public abstract class CharacterBase : MonoBehaviour
{
    // 기존 피격 이벤트 유지
    public event Action<float> OnDamageTaken;

    [Header("피격 색상 효과")]
    [SerializeField] private DamageFlash damageFlash;

    public abstract void TakeDamage(float amount);

    protected virtual void Awake()
    {
        if (damageFlash == null)
            damageFlash = GetComponentInChildren<DamageFlash>();
    }

    // 실제 데미지가 적용된 뒤 하위 클래스에서 호출
    protected void RaiseDamageTaken(float amount)
    {
        // 기존 전투 통계 이벤트
        OnDamageTaken?.Invoke(amount);

        // 빨간색 피격 효과
        if (amount > 0f)
            damageFlash?.PlayFlash();
    }
}
