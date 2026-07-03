using UnityEngine;

// 게임 상태 [거리, 체력]을 입력받아 전술별 Q-Value를 출력하는 RBFNN이다.
// Gaussian RBF 기저함수 + 선형 출력층으로 구성된다.
// BatchUpdate()로 미니배치 그래디언트 방식 학습을 수행하며,
// 퍼지 소속도가 임계값 미만인 샘플은 해당 전문가 학습에서 제외한다.
// 2D 원작의 QValueRBFNN을 그대로 포팅했다 — 순수 수학 로직이라 3D 전환과 무관하다.
public class QValueRBFNN
{
    private readonly int   _nCenters;
    private readonly int   _outputDim;
    private readonly float _lr;
    private readonly float _width;               // Gaussian 기저함수 너비
    private readonly float _membershipThreshold; // 소속도 임계값 필터링

    // RBF 기저함수 중심 [nCenters, inputDim] — [0,1] 범위로 랜덤 초기화
    private readonly float[][] _centers;

    // 가중치 행렬 [nCenters, outputDim] — 작은 랜덤값으로 초기화
    private readonly float[,] _weights;

    // 출력층 편향 [outputDim] — 0으로 초기화
    private readonly float[] _bias;

    public QValueRBFNN(int inputDim = 2, int outputDim = 3, int nCenters = 5,
                       float lr = 0.1f, float width = 0.5f, float membershipThreshold = 0.15f)
    {
        _nCenters            = nCenters;
        _outputDim           = outputDim;
        _lr                  = lr;
        _width               = width;
        _membershipThreshold = membershipThreshold;

        _centers = new float[nCenters][];
        for (int i = 0; i < nCenters; i++)
        {
            _centers[i] = new float[inputDim];
            for (int j = 0; j < inputDim; j++)
                _centers[i][j] = Random.value;
        }

        _weights = new float[nCenters, outputDim];
        for (int i = 0; i < nCenters; i++)
            for (int j = 0; j < outputDim; j++)
                _weights[i, j] = Random.Range(-0.1f, 0.1f);

        _bias = new float[outputDim];
    }

    // 게임 상태 벡터를 받아 전술별 Q-Value를 반환한다.
    public float[] Predict(float[] state)
    {
        float[] phi    = Basis(state);
        float[] output = new float[_outputDim];

        for (int j = 0; j < _outputDim; j++)
        {
            output[j] = _bias[j];
            for (int i = 0; i < _nCenters; i++)
                output[j] += phi[i] * _weights[i, j];
        }

        return output;
    }

    // 미니배치 그래디언트로 가중치를 갱신한다.
    // memberships[i]가 임계값 미만인 샘플은 이 클러스터와 관련이 낮으므로 건너뛴다.
    public void BatchUpdate(float[][] states, int[] actions, float[] rewards, float[] memberships)
    {
        float[,] gradW = new float[_nCenters, _outputDim];
        float[]  gradB = new float[_outputDim];
        int validCount = 0;

        for (int i = 0; i < states.Length; i++)
        {
            if (memberships[i] < _membershipThreshold) continue;

            float[] phi   = Basis(states[i]);
            float[] pred  = Predict(states[i]);
            float   error = rewards[i] - pred[actions[i]];

            for (int c = 0; c < _nCenters; c++)
                gradW[c, actions[i]] += error * phi[c];
            gradB[actions[i]] += error;

            validCount++;
        }

        if (validCount == 0) return;

        for (int i = 0; i < _nCenters; i++)
            for (int j = 0; j < _outputDim; j++)
                _weights[i, j] += _lr * (gradW[i, j] / validCount);

        for (int j = 0; j < _outputDim; j++)
            _bias[j] += _lr * (gradB[j] / validCount);
    }

    // Gaussian RBF 기저함수: phi_i = exp(-0.5 * dist² / width²)
    private float[] Basis(float[] x)
    {
        float[] phi = new float[_nCenters];
        for (int i = 0; i < _nCenters; i++)
        {
            float distSq = 0f;
            for (int j = 0; j < x.Length; j++)
            {
                float d = x[j] - _centers[i][j];
                distSq += d * d;
            }
            phi[i] = Mathf.Exp(-0.5f * distSq / (_width * _width));
        }
        return phi;
    }
}
