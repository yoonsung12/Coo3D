using System;
using Sirenix.OdinInspector;
using UnityEngine;

// ES3로 영구 저장되는 누적 점수를 관리하는 싱글턴이다.
// 세이브 슬롯(SaveManager)과는 별개로, 슬롯을 새로 시작해도 계속 유지되는 전역 누적 점수를 담는다.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private const string ScoreKey = "totalScore";

    [ReadOnly, ShowInInspector, LabelText("누적 점수")]
    private int _totalScore;

    public int TotalScore => _totalScore;

    // 점수가 바뀔 때마다 발행된다. 점수 UI가 이 이벤트를 구독해 표시를 갱신한다.
    public event Action<int> OnScoreChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 슬롯 파일과 무관한 기본 ES3 파일에서 불러온다. 처음 실행이면 0으로 시작한다.
        _totalScore = ES3.Load(ScoreKey, 0);
    }

    // 점수를 amount만큼 더하고 즉시 ES3에 저장한다. 코인 아이템 등에서 사용한다.
    public void AddScore(int amount)
    {
        _totalScore += amount;
        ES3.Save(ScoreKey, _totalScore);
        OnScoreChanged?.Invoke(_totalScore);
    }
}
