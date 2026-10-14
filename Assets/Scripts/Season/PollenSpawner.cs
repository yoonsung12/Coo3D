using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 봄 계절 낙하물인 꽃가루를 일정한 간격으로 하나씩 생성하는 Spawner다.
// 예전에는 몇 개를 한꺼번에 쏟아낸 뒤 쉬는 방식이라 "우수수 → 없음"이 반복돼 보였으므로, 간격을 고르게 바꿨다.
// BaseHazard를 상속해 스폰 범위 로직만 재사용한다 (계절 타입/게이지 칸 수 필드는 사용하지 않는다).
public class PollenSpawner : BaseHazard
{
    [Title("꽃가루 프리팹")]
    [SerializeField, LabelText("꽃가루 프리팹")]
    private GameObject pollenPrefab;
    // Inspector에서 Pollen 컴포넌트가 붙은 프리팹을 연결한다.

    [Title("생성 설정")]
    [SerializeField, LabelText("생성 간격(초)"), Min(0.05f)]
    private float spawnInterval = 0.35f;
    // 이 시간마다 꽃가루를 1개씩 생성한다. 값이 작을수록 더 촘촘하게 떨어진다.
    // 꽃가루 수명(Pollen의 "수명")과 곱한 값이 화면에 동시에 떠 있는 대략적인 개수가 된다.

    [SerializeField, LabelText("씬 내 최대 개수")]
    private int maxPollen = 30;
    // 이 개수를 넘으면 새 꽃가루를 생성하지 않는다.

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // 같은 간격을 매번 새로 만들지 않도록 한 번만 만들어 재사용한다.
        var wait = new WaitForSeconds(spawnInterval);

        while (true)
        {
            if (FindObjectsByType<Pollen>(FindObjectsSortMode.None).Length < maxPollen)
                Instantiate(pollenPrefab, GetSpawnPosition(), Quaternion.identity);

            yield return wait;
        }
    }

    [Button("꽃가루 1개 생성 테스트")]
    private void TestSpawnOne()
    {
        if (Application.isPlaying)
            Instantiate(pollenPrefab, GetSpawnPosition(), Quaternion.identity);
    }
}
