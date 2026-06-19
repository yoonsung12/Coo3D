using UnityEngine;

// Player와 Enemy가 공통으로 상속받는 캐릭터 기반 클래스다.
public abstract class CharacterBase : MonoBehaviour
{
    public abstract void TakeDamage(float amount);
}
