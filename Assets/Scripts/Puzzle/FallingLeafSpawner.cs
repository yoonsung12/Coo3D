using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 구역 "공중 낙엽 점화" 퍼즐용으로, 위에서 낙엽(LeafMound, 떨어지는 낙엽 모드)을 일정 간격으로 떨어뜨린다.
// 플레이어는 떨어지는 낙엽에 성냥으로 불을 붙이고 선풍기로 덩굴벽까지 날려야 한다.
// 계절 게이지와 무관한 퍼즐 오브젝트라 BaseHazard를 상속하지 않고 단순하게 만든다.
public class FallingLeafSpawner : MonoBehaviour
{
    [Title("낙엽 프리팹")]
    [SerializeField, LabelText("낙엽 프리팹")]
    private LeafMound leafPrefab;
    // Inspector에서 "떨어지는 낙엽"이 켜진 LeafMound 프리팹(FallingLeaf.prefab)을 연결한다.

    [Title("생성 설정")]
    [SerializeField, LabelText("생성 간격(초)")]
    private float spawnInterval = 2.5f;
    // 낙엽이 하나씩 떨어지는 간격이다. 값이 작을수록 자주 떨어져 퍼즐이 쉬워진다.

    [SerializeField, LabelText("동시 최대 개수")]
    private int maxLeaves = 4;
    // 화면에 동시에 떠 있을 수 있는 낙엽 수다. 넘으면 기존 낙엽이 사라질 때까지 생성을 멈춘다.

    [SerializeField, LabelText("스폰 범위 절반 너비")]
    private float spawnHalfWidth = 2f;
    // 이 오브젝트 위치를 중심으로 좌우 ±값 범위 안에서 랜덤하게 생성된다. Y/Z는 이 오브젝트 위치를 그대로 쓴다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 낙엽 수")]
    private int CurrentCount => _spawned.Count;

    private readonly List<LeafMound> _spawned = new List<LeafMound>();
    // 이 스포너가 만든 낙엽만 추적한다. 매번 씬 전체를 검색(FindObjectsByType)하지 않기 위함이다.

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(spawnInterval);
        // 같은 대기 객체를 재사용해 매번 가비지가 생기지 않게 한다.

        while (true)
        {
            // 파괴된 낙엽은 Unity에서 null로 취급되므로 목록에서 정리한 뒤 개수를 센다.
            _spawned.RemoveAll(leaf => leaf == null);

            if (_spawned.Count < maxLeaves)
                SpawnOne();

            yield return wait;
        }
    }

    private void SpawnOne()
    {
        if (leafPrefab == null) return;

        float randomX = Random.Range(-spawnHalfWidth, spawnHalfWidth);
        Vector3 spawnPos = transform.position + new Vector3(randomX, 0f, 0f);
        _spawned.Add(Instantiate(leafPrefab, spawnPos, Quaternion.identity));
    }

    [Button("낙엽 1개 생성 테스트")]
    private void TestSpawnOne()
    {
        if (Application.isPlaying)
            SpawnOne();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 씬 뷰에서 스폰 범위를 선으로 표시해 배치할 때 확인하기 쉽게 한다.
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Vector3 left = transform.position + Vector3.left * spawnHalfWidth;
        Vector3 right = transform.position + Vector3.right * spawnHalfWidth;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.15f);
        Gizmos.DrawWireSphere(right, 0.15f);
    }
#endif
}
