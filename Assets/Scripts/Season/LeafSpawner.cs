using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 계절 낙하물인 은행잎을 주기적으로 우수수 생성하는 Spawner다.
// BaseHazard를 상속해 스폰 범위 로직만 재사용한다 (계절 타입/게이지 칸 수 필드는 사용하지 않는다).
public class LeafSpawner : BaseHazard
{
    [Title("은행잎 프리팹")]
    [SerializeField, LabelText("은행잎 프리팹")]
    private GameObject leafPrefab;
    // Inspector에서 LeafDrop 컴포넌트가 붙은 프리팹을 연결한다.

    [Title("우수수 생성 설정")]
    [SerializeField, LabelText("한 번에 떨어질 개수")]
    private int burstCount = 3;
    // 착지 후 사라지지 않고 계속 쌓이는 낙하물이라, 꽃가루보다 적은 개수만 한 번에 떨어뜨린다.

    [SerializeField, LabelText("은행잎 사이 생성 간격")]
    private float burstSpawnDelay = 0.15f;

    [SerializeField, LabelText("우수수 후 쉬는 시간")]
    private float burstCooldown = 5f;
    // 값이 클수록 은행잎이 뜸하게, 천천히 떨어진다.

    [SerializeField, LabelText("씬 내 최대 개수")]
    private int maxLeaves = 8;
    // 이 개수를 넘으면 새 은행잎을 생성하지 않는다. 착지한 은행잎도 계속 개수에 포함되므로 작게 잡는다.

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
                if (FindObjectsByType<LeafDrop>(FindObjectsSortMode.None).Length < maxLeaves)
                    Instantiate(leafPrefab, GetSpawnPosition(), Quaternion.identity);

                yield return new WaitForSeconds(burstSpawnDelay);
            }

            yield return new WaitForSeconds(burstCooldown);
        }
    }

    [Button("은행잎 1개 생성 테스트")]
    private void TestSpawnOne()
    {
        if (Application.isPlaying)
            Instantiate(leafPrefab, GetSpawnPosition(), Quaternion.identity);
    }
}
