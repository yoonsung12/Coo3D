using System;
using UnityEngine;

// Player와 Enemy가 공통으로 상속받는 캐릭터 기반 클래스다.
public abstract class CharacterBase : MonoBehaviour
{
    // 피격 시 발행된다. CombatStatsTracker/NFBTEnemyAI가 구독해 전투 통계 수집과 보상 계산에 사용한다.
    public event Action<float> OnDamageTaken;

    public abstract void TakeDamage(float amount);

    // 하위 클래스가 TakeDamage 안에서 호출해 피격 이벤트를 발행한다.
    protected void RaiseDamageTaken(float amount) => OnDamageTaken?.Invoke(amount);
}
