using UnityEngine;

// 퍼지 클러스터링(FCM)으로 최근 Player 전투 패턴을 분류하고, 클러스터별 RBFNN Q값 전문가를
// 앙상블해 3가지 전술(Chase/Attack, Evade/Recover, Counter) 중 하나를 선택하는 강화학습 모듈이다.
// MonoBehaviour가 아닌 순수 C# 클래스로, NFBTEnemyAI가 소유하고 전술 재계산 주기(기본 2초)마다 호출한다.
// 2D 원작의 NPCFuzzyRL 구조를 그대로 포팅했다.
public class NPCFuzzyRL
{
    // NFBTEnemyAI.ActiveBranch에 대응하는 전술 이름이다. 인덱스가 곧 행동(action) 번호다.
    public static readonly string[] BranchNames = { "Chase/Attack", "Evade/Recover", "Counter" };

    private const int NumActions = 3;

    private readonly SlidingWindowFCM _fcm;
    private readonly QValueRBFNN[] _experts;
    private readonly ExperienceReplay _replay;
    private readonly float _epsilonGreedy;
    private readonly int _batchSize;

    private float[] _lastMembership;
    private float[] _lastState;
    private int _lastAction;

    public NPCFuzzyRL(int windowSize = 50, int nClusters = 3, float epsilonGreedy = 0.1f,
                       int replayMaxSize = 100, int batchSize = 4)
    {
        _fcm = new SlidingWindowFCM(windowSize, nClusters);

        _experts = new QValueRBFNN[nClusters];
        for (int i = 0; i < nClusters; i++)
            _experts[i] = new QValueRBFNN(inputDim: 2, outputDim: NumActions, nCenters: 5, lr: 0.1f);

        _replay = new ExperienceReplay(replayMaxSize);
        _epsilonGreedy = epsilonGreedy;
        _batchSize = batchSize;
    }

    // 최근 전투 로그(공격빈도/명중률/DPS)를 슬라이딩 윈도우에 추가하고,
    // 이 로그가 각 클러스터에 얼마나 속하는지(소속도) 계산해 캐시한다.
    public void OnPlayerActionLog(float[] log)
    {
        _fcm.AddLog(log);
        if (_fcm.HasEnoughData)
            _fcm.Refit();

        _lastMembership = _fcm.SoftPredict(log);
    }

    // gameState=[거리 정규화, 체력 비율]을 받아 앙상블 Q값이 가장 높은 전술 인덱스를 반환한다.
    // epsilonGreedy 확률만큼 무작위 탐험을 섞는다(ε-greedy).
    public int ComputeTactic(float[] gameState)
    {
        if (_lastMembership == null)
        {
            // 아직 OnPlayerActionLog가 한 번도 호출되지 않았으면 균등 소속도를 가정한다.
            _lastMembership = new float[_experts.Length];
            for (int i = 0; i < _lastMembership.Length; i++)
                _lastMembership[i] = 1f / _experts.Length;
        }

        int action;
        if (Random.value < _epsilonGreedy)
        {
            action = Random.Range(0, NumActions);
        }
        else
        {
            // 클러스터별 전문가 예측을 소속도로 가중합해 하나의 앙상블 Q값으로 합친다.
            float[] ensembleQ = new float[NumActions];
            for (int i = 0; i < _experts.Length; i++)
            {
                float[] q = _experts[i].Predict(gameState);
                for (int a = 0; a < NumActions; a++)
                    ensembleQ[a] += _lastMembership[i] * q[a];
            }

            action = ArgMax(ensembleQ);
        }

        _lastState = gameState;
        _lastAction = action;

        return action;
    }

    // 마지막으로 선택한 전술에 대한 보상을 받아 경험 리플레이에 저장하고,
    // 버퍼가 충분히 쌓이면 클러스터별 전문가를 미니배치로 학습시킨다.
    public void OnRewardReceived(float reward)
    {
        if (_lastState == null || _lastMembership == null) return;

        _replay.Add(_lastState, _lastMembership, _lastAction, reward);

        if (_replay.Count < _batchSize) return;

        var batch = _replay.SampleMiniBatch(_batchSize);
        var states = new float[batch.Count][];
        var actions = new int[batch.Count];
        var rewards = new float[batch.Count];

        for (int i = 0; i < batch.Count; i++)
        {
            states[i] = batch[i].state;
            actions[i] = batch[i].action;
            rewards[i] = batch[i].reward;
        }

        // 클러스터(전문가)마다 이 미니배치에 대한 소속도만 뽑아 가중 학습시킨다.
        for (int c = 0; c < _experts.Length; c++)
        {
            var memberships = new float[batch.Count];
            for (int i = 0; i < batch.Count; i++)
                memberships[i] = batch[i].membership[c];

            _experts[c].BatchUpdate(states, actions, rewards, memberships);
        }
    }

    private int ArgMax(float[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] > values[best]) best = i;
        return best;
    }
}
