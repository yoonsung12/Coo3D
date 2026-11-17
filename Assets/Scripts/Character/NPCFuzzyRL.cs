using UnityEngine;

// 퍼지 클러스터링(FCM)으로 최근 Player 전투 패턴을 분류하고, 클러스터별 RBFNN Q값 전문가를
// 앙상블해 3가지 전술(Chase/Attack, Evade/Recover, Counter) 중 하나를 선택하는 강화학습 모듈이다.
// MonoBehaviour가 아닌 순수 C# 클래스로, NFBTEnemyAI가 소유하고 전술 재계산 주기(기본 2초)마다 호출한다.
// 2D 원작의 NPCFuzzyRL 구조를 그대로 포팅했다.
public class NPCFuzzyRL
{
    // NFBTEnemyAI.ActiveBranch에 대응하는 전술 이름이다. 인덱스가 곧 행동(action) 번호다.
    public static readonly string[] BranchNames = { "Chase/Attack", "Evade/Recover", "Counter" };

    // 공격 선택 학습 경로(ChooseAttack)용 행동 이름이다. 인덱스 0/1/2가 곧 행동 번호다.
    // 행동 수는 BranchNames와 같은 3이라 같은 RBFNN 구조를 그대로 재사용한다.
    public static readonly string[] AttackNames = { "쪼기", "도약", "원거리" };

    // ChooseAttack이 내린 결정을 기록해 둔다. 도약 착지/투사체 명중처럼 결과가 늦게 확정되는 공격은
    // 결정 시점의 상태·소속도로 보상을 붙여야 하므로, 결정 당시 값을 복사해 들고 있다가 RecordResult에 넘긴다.
    public struct AttackDecision
    {
        public float[] state;        // 결정 시점 게임 상태(복사본)
        public float[] membership;   // 결정 시점 클러스터 소속도(복사본)
        public int action;           // 선택된 공격 인덱스
        public bool isValid;         // false면 기본값(아직 결정 없음)이라 RecordResult가 무시한다
    }

    private const int NumActions = 3;

    private readonly SlidingWindowFCM _fcm;
    private readonly QValueRBFNN[] _experts;
    private readonly ExperienceReplay _replay;
    private readonly float _epsilonGreedy;
    private readonly int _batchSize;

    private float[] _lastMembership;
    private float[] _lastState;
    private int _lastAction;

    // 디버그용으로 마지막 ChooseAttack의 앙상블 Q값/소속도를 노출한다(읽기 전용, 결정 전에는 null).
    public float[] LastQValues { get; private set; }
    public float[] LastMembership { get; private set; }

    // useStandardization: 공격 선택 경로처럼 피처가 이미 0~1이면 false로 넘겨 FCM 표준화를 끈다.
    public NPCFuzzyRL(int windowSize = 50, int nClusters = 3, float epsilonGreedy = 0.1f,
                       int replayMaxSize = 100, int batchSize = 4, bool useStandardization = true)
    {
        _fcm = new SlidingWindowFCM(windowSize, nClusters, useStandardization: useStandardization);

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
            action = ArgMax(ComputeEnsembleQ(gameState));
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

        TrainFromReplay();
    }

    // 클러스터별 전문가 예측을 소속도로 가중합해 하나의 앙상블 Q값으로 합친다.
    // ComputeTactic과 ChooseAttack이 같은 계산을 쓰도록 분리했다.
    private float[] ComputeEnsembleQ(float[] gameState)
    {
        float[] ensembleQ = new float[NumActions];
        for (int i = 0; i < _experts.Length; i++)
        {
            float[] q = _experts[i].Predict(gameState);
            for (int a = 0; a < NumActions; a++)
                ensembleQ[a] += _lastMembership[i] * q[a];
        }
        return ensembleQ;
    }

    // 버퍼가 충분히 쌓였으면 미니배치를 뽑아 클러스터별 전문가를 학습시킨다.
    // OnRewardReceived와 RecordResult가 같은 학습 절차를 공유하도록 분리했다.
    private void TrainFromReplay()
    {
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

    // 지금 쓸 수 있는 공격(available[i]==true) 중에서 하나를 고른다.
    // 쿨타임 중인 공격을 고르면 실제로는 쪼기가 실행되어 보상이 엉뚱한 행동 칸에 쌓이던 문제를 막기 위해,
    // 후보를 사용 가능한 공격으로만 제한한다. 후보가 없으면 쪼기(0)가 항상 마지막 보루다.
    // bonus는 공격별 임시 가산점이다. 이번 판단의 비교에만 쓰이고 학습(보상 기록)과 LastQValues에는 영향이 없다.
    public AttackDecision ChooseAttack(float[] gameState, bool[] available, float[] bonus = null)
    {
        if (_lastMembership == null)
        {
            // 아직 OnPlayerActionLog가 한 번도 호출되지 않았으면 균등 소속도를 가정한다.
            _lastMembership = new float[_experts.Length];
            for (int i = 0; i < _lastMembership.Length; i++)
                _lastMembership[i] = 1f / _experts.Length;
        }

        // 후보 수집
        var candidates = new System.Collections.Generic.List<int>(NumActions);
        for (int a = 0; a < NumActions; a++)
            if (available != null && a < available.Length && available[a]) candidates.Add(a);
        if (candidates.Count == 0) candidates.Add(0);

        float[] ensembleQ = ComputeEnsembleQ(gameState);

        int action;
        if (Random.value < _epsilonGreedy)
        {
            action = candidates[Random.Range(0, candidates.Count)];
        }
        else
        {
            action = candidates[0];
            float best = ensembleQ[action] + (bonus != null && action < bonus.Length ? bonus[action] : 0f);
            for (int i = 1; i < candidates.Count; i++)
            {
                int a = candidates[i];
                float score = ensembleQ[a] + (bonus != null && a < bonus.Length ? bonus[a] : 0f);
                if (score > best)
                {
                    action = a;
                    best = score;
                }
            }
        }

        // 결과가 나중에 확정되므로, 이후 값이 바뀌어도 영향 받지 않게 복사본을 저장한다.
        var decision = new AttackDecision
        {
            state = (float[])gameState.Clone(),
            membership = (float[])_lastMembership.Clone(),
            action = action,
            isValid = true
        };

        LastQValues = ensembleQ;
        LastMembership = decision.membership;

        // 기존 OnRewardReceived(피격 벌점)가 "가장 최근에 고른 공격"에 기록되도록 갱신한다.
        _lastState = decision.state;
        _lastAction = action;

        return decision;
    }

    // 공격 결과(도약 착지, 투사체 명중/빗나감 등)가 확정된 시점에 그 결정에 정확히 보상을 붙여 학습한다.
    // 가장 최근 결정이 아니라 넘겨받은 decision에 기록하므로 늦게 끝나는 공격도 올바른 칸에 쌓인다.
    public void RecordResult(AttackDecision decision, float reward)
    {
        if (!decision.isValid) return;

        _replay.Add(decision.state, decision.membership, decision.action, reward);

        TrainFromReplay();
    }

    private int ArgMax(float[] values)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] > values[best]) best = i;
        return best;
    }
}
