using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 계절 낙하물인 은행열매을 주기적으로 우수수 생성하는 Spawner다.
// BaseHazard를 상속해 스폰 범위 로직만 재사용한다 (계절 타입/게이지 칸 수 필드는 사용하지 않는다).
public class GinkgoSpawner : BaseHazard
{
    [Title("은행열매 프리팹")]
    [SerializeField, LabelText("은행열매 프리팹")]
    private GameObject leafPrefab;
    // Inspector에서 GinkgoFruit 컴포넌트가 붙은 프리팹을 연결한다.

    [Title("우수수 생성 설정")]
    [SerializeField, LabelText("한 번에 떨어질 개수")]
    private int burstCount = 3;
    // 한 번의 우수수에서 연달아 떨어뜨릴 개수다.

    [SerializeField, LabelText("은행열매 사이 생성 간격")]
    private float burstSpawnDelay = 0.15f;

    [SerializeField, LabelText("우수수 후 쉬는 시간")]
    private float burstCooldown = 5f;
    // 값이 클수록 은행열매이 뜸하게, 천천히 떨어진다.

    [SerializeField, LabelText("씬 내 최대 개수")]
    private int maxLeaves = 8;
    // 이 스포너가 만든 은행열매가 이 개수를 넘으면 새로 생성하지 않는다. 착지한 은행열매도 포함된다.

    [SerializeField, LabelText("착지 후 소멸 시간")]
    private float landedLifetime = 0f;
    // 0이면 착지한 은행이 계속 남는다(보스전처럼 바닥 위험물로 쓸 때).
    // 0보다 크면 그 시간 뒤 사라져서 계속 떨어뜨릴 수 있다.

    // 이 스포너가 만든 은행만 센다. 다른 스포너/퍼즐의 은행 때문에 이 스포너가 멈추지 않게 하고,
    // 매번 씬 전체를 검색(FindObjectsByType)하지 않기 위해 직접 목록을 관리한다.
    private readonly List<GinkgoFruit> _spawnedFruits = new List<GinkgoFruit>();

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
                _spawnedFruits.RemoveAll(f => f == null); // 사라진 은행은 목록에서 제거
                if (_spawnedFruits.Count < maxLeaves)
                    SpawnFruit();

                yield return new WaitForSeconds(burstSpawnDelay);
            }

            yield return new WaitForSeconds(burstCooldown);
        }
    }

    // 은행 1개를 생성하고 착지 후 소멸 시간을 전달한 뒤 목록에 등록한다.
    private void SpawnFruit()
    {
        GameObject obj = Instantiate(leafPrefab, GetSpawnPosition(), Quaternion.identity);
        GinkgoFruit fruit = obj.GetComponent<GinkgoFruit>();
        if (fruit == null) return;

        fruit.SetLandedLifetime(landedLifetime);
        _spawnedFruits.Add(fruit);
    }

    [Button("은행열매 1개 생성 테스트")]
    private void TestSpawnOne()
    {
        if (Application.isPlaying)
            SpawnFruit();
    }
}
