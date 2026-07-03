using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 적이 사망했을 때 확률적으로 아이템을 드롭시킨다. Enemy와 같은 오브젝트에 붙여서 사용한다.
[RequireComponent(typeof(Enemy))]
public class ItemDropper : MonoBehaviour
{
    [Serializable]
    private class DropEntry
    {
        [LabelText("드롭 프리팹")]
        public GameObject prefab;

        [LabelText("드롭 확률 (0~1)"), Range(0f, 1f)]
        public float dropChance = 0.3f;
    }

    [Title("드롭 목록")]
    [SerializeField, LabelText("드롭 항목들")]
    private DropEntry[] dropEntries;
    // 항목마다 독립적으로 확률 판정한다. 여러 개가 동시에 드롭될 수도, 하나도 안 드롭될 수도 있다.

    private Enemy _enemy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _enemy.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        _enemy.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (dropEntries == null) return;

        foreach (var entry in dropEntries)
        {
            if (entry.prefab == null) continue;

            if (UnityEngine.Random.value <= entry.dropChance)
                Instantiate(entry.prefab, transform.position, Quaternion.identity);
        }
    }
}
