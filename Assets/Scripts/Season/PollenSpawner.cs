using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 봄 계절 낙하물인 꽃가루를 주기적으로 우수수 생성하는 Spawner다.
// BaseHazard를 상속해 스폰 범위 로직만 재사용한다 (계절 타입/게이지 칸 수 필드는 사용하지 않는다).
public class PollenSpawner : BaseHazard
{
    [Title("꽃가루 프리팹")]
    [SerializeField, LabelText("꽃가루 프리팹")]
    private GameObject pollenPrefab;
    // Inspector에서 Pollen 컴포넌트가 붙은 프리팹을 연결한다.

    [Title("우수수 생성 설정")]
    [SerializeField, LabelText("한 번에 떨어질 개수")]
    private int burstCount = 8;

    [SerializeField, LabelText("꽃가루 사이 생성 간격")]
    private float burstSpawnDelay = 0.08f;

    [SerializeField, LabelText("우수수 후 쉬는 시간")]
    private float burstCooldown = 1.5f;

    [SerializeField, LabelText("씬 내 최대 개수")]
    private int maxPollen = 30;
    // 이 개수를 넘으면 새 꽃가루를 생성하지 않는다.

    private void Start()
    {
        StartCoroutine(SpawnBurstRoutine());
    }

    private IEnumerator SpawnBurstRoutine()
    {
        while (true)
        {
            for (int i = 0; i < burstCount; i++)
            {
                if (FindObjectsByType<Pollen>(FindObjectsSortMode.None).Length < maxPollen)
                    Instantiate(pollenPrefab, GetSpawnPosition(), Quaternion.identity);

                yield return new WaitForSeconds(burstSpawnDelay);
            }

            yield return new WaitForSeconds(burstCooldown);
        }
    }

    [Button("꽃가루 1개 생성 테스트")]
    private void TestSpawnOne()
    {
        if (Application.isPlaying)
            Instantiate(pollenPrefab, GetSpawnPosition(), Quaternion.identity);
    }
}
