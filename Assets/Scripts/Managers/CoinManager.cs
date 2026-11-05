using System;
using Sirenix.OdinInspector;
using UnityEngine;

// 자판기에서 사용하는 런타임 전용 화폐를 관리하는 싱글턴이다.
// ScoreManager의 누적 점수와 달리 체크포인트 저장 시점의 값만 SaveManager가 세이브 파일에 기록한다.
// 사망 후 계속하기를 하면 그 저장 시점의 코인으로 복귀하고, 새 게임을 시작하면 0으로 초기화된다.
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("보유 코인")]
    private int _coinCount;

    public int CoinCount => _coinCount;

    // 코인 수가 바뀔 때마다 발행된다. 코인 UI가 이 이벤트를 구독해 표시를 갱신한다.
    public event Action<int> OnCoinChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 코인을 amount만큼 추가한다. 코인 아이템을 먹었을 때 사용한다.
    public void AddCoin(int amount)
    {
        _coinCount += amount;
        OnCoinChanged?.Invoke(_coinCount);
    }

    // 코인 수를 amount로 직접 설정한다. 저장 불러오기(사망 후 계속하기 포함)에서 저장 시점의 코인으로 되돌릴 때 쓴다.
    public void SetCoin(int amount)
    {
        _coinCount = Mathf.Max(amount, 0);
        OnCoinChanged?.Invoke(_coinCount);
    }

    // 코인을 amount만큼 소비를 시도한다. 보유량이 부족하면 false를 반환하고 아무것도 바뀌지 않는다.
    // 자판기가 구매 가능 여부를 판단할 때 사용한다.
    public bool TrySpendCoin(int amount)
    {
        if (_coinCount < amount) return false;

        _coinCount -= amount;
        OnCoinChanged?.Invoke(_coinCount);
        return true;
    }
}
