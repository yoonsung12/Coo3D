// 씬 전환 시 다음 씬으로 넘겨줄 정보를 담는 정적 데이터 클래스다.
// ES3 저장 없이 런타임 메모리만 사용하며, ScenePortal이 씬 전환 직전에 값을 채우고
// SceneSpawnPoint/SeasonGaugeManager가 새 씬 로드 후 읽고 나서 각자 초기화한다.
public static class SceneTransitionData
{
    // ScenePortal이 씬 전환 직전에 설정하는 스폰 포인트 ID.
    // SceneSpawnPoint.Start()에서 자기 ID와 비교한 뒤 빈 문자열로 초기화한다.
    public static string PendingSpawnId { get; set; } = "";

    // 포털로 넘어오기 직전 플레이어 체력을 이어받아야 하는지 여부와 그 값이다.
    // SceneSpawnPoint가 새 씬의 플레이어 체력에 적용한 뒤 false로 초기화한다.
    public static bool HasPendingHealth { get; set; } = false;
    public static float PendingHealth { get; set; }

    // true면 새 씬의 SeasonGaugeManager가 게이지/디버프 상태를 초기화하지 않고 그대로 이어간다.
    // SeasonGaugeManager.Awake()가 확인한 뒤 false로 초기화한다.
    public static bool PreserveSeasonState { get; set; } = false;
}
