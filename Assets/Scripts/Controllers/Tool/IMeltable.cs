// 횃불 열기에 녹을 수 있는 얼음 오브젝트가 구현해야 하는 인터페이스다.
public interface IMeltable
{
    // 횃불이 근처에 있는 동안 매 프레임 호출된다.
    // heatAmount: 이 프레임에 받은 열량 (heatPerSecond * Time.deltaTime)
    void OnMelted(float heatAmount);
}
