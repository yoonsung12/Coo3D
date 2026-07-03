using UnityEngine;

// 현재 활성화된 체크포인트(리스폰 지점)를 런타임에 등록/조회하는 창구다.
// 씬 전환 시 유지해야 할 별도 GameObject 상태가 아니라 순수 데이터 보관 목적이라
// MonoBehaviour 싱글턴 대신 static 클래스로 가볍게 구현한다.
public static class CheckpointManager
{
    public static Vector3 CurrentCheckpointPosition { get; private set; }
    // 사망 시 리스폰하거나 세이브할 때 사용하는 현재 리스폰 위치다.

    public static string CurrentCheckpointScene { get; private set; }
    // 리스폰 위치가 속한 씬 이름이다. SaveManager가 세이브 데이터에 함께 기록한다.

    public static bool HasCheckpoint { get; private set; }
    // 아직 체크포인트를 한 번도 지나지 않았을 때 PlayerHealth가 시작 위치를 기본 리스폰 지점으로 등록하기 위한 플래그다.

    // RegionCheckpoint/CheckpointTrigger가 플레이어 진입 시 호출해 리스폰 지점을 갱신한다.
    public static void SetCheckpoint(Vector3 position, string sceneName)
    {
        CurrentCheckpointPosition = position;
        CurrentCheckpointScene = sceneName;
        HasCheckpoint = true;
    }
}
