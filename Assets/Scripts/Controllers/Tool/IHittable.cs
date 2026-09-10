// 검(SwordHitbox)에 맞았을 때 반응해야 하는, 체력을 가지지 않은 환경 오브젝트가 구현하는 인터페이스다.
// CharacterBase(체력/피격 연출이 있는 대상)와는 별개로, 고드름처럼 "맞으면 특정 동작을 한다"는
// 오브젝트를 위해 사용한다.
public interface IHittable
{
    // 검에 맞았을 때 호출된다.
    void OnHit();
}
