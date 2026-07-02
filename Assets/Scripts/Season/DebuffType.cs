// 시즌 게이지가 가득 찼을 때 발동하는 디버프 종류다.
// 어떤 계절 게이지가 가장 많이 쌓였는지에 따라 발동되는 디버프가 달라진다.
public enum DebuffType
{
    None,
    Bound,   // 봄: 이동 속박
    Slow,    // 여름: 이동 속도 저하
    Reverse, // 가을: 좌우 방향 반전
    Frozen   // 겨울: 모든 이동 정지
}
