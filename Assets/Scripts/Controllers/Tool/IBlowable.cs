using UnityEngine;

// 선풍기 바람이나 WindZone에 반응하는 오브젝트가 구현해야 할 인터페이스다.
public interface IBlowable
{
    // direction: 바람이 부는 방향 (normalized), force: 바람의 세기
    // impulse: true이면 즉각적인 충격(블라스트), false이면 지속 힘(일반 바람)
    void OnBlown(Vector3 direction, float force, bool impulse = false);
}
