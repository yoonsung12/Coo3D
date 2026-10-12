using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 들고 있는 불꽃놀이 개수를 관리하는 컴포넌트다. Player 오브젝트에 붙인다.
// 지하 상자(FireworkItem)에서 얻고, FireworkLaunchSpot에서 설치할 때 하나씩 쓴다.
// 이번 씬 안에서만 쓰는 퍼즐 재료라 저장(SaveManager)은 하지 않는다.
public class FireworkBag : MonoBehaviour
{
    public static FireworkBag Instance { get; private set; }

    [Title("현재 상태")]
    [ReadOnly, ShowInInspector, LabelText("보유 불꽃놀이 수")]
    private int _count;
    // Play Mode에서 현재 몇 개를 들고 있는지 Inspector에서 바로 확인할 수 있다.

    public int Count => _count;

    // 개수가 바뀔 때마다 새 개수를 전달한다. 발사대 안내 문구 갱신에 쓴다.
    public event Action<int> OnCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        _count += amount;
        OnCountChanged?.Invoke(_count);
    }

    // 하나 있으면 소비하고 true, 없으면 아무것도 바꾸지 않고 false를 반환한다.
    public bool TryUse()
    {
        if (_count <= 0) return false;
        _count--;
        OnCountChanged?.Invoke(_count);
        return true;
    }

    [Button("불꽃놀이 +3 테스트")]
    private void TestAdd() => Add(3);
}
