using System.Collections.Generic;
using UnityEngine;

// 슬라이딩 윈도우 방식의 실시간 퍼지 C-평균(Fuzzy C-Means).
// 최근 windowSize 개의 플레이어 행동 로그를 버퍼에 쌓고,
// Refit() 호출마다 버퍼 기준으로 StandardScaler + FCM을 재학습한다.
// 이전 클러스터 중심을 초기값으로 재사용해 수렴 속도를 높인다.
// 2D 원작(NFBT 적 AI)의 SlidingWindowFCM을 그대로 포팅한 클래스다 — Unity 엔진 의존이
// Mathf/Random뿐인 순수 수학 로직이라 3D 전환과 무관하게 동일하게 동작한다.
public class SlidingWindowFCM
{
    private readonly int _windowSize;
    private readonly int _nClusters;
    private readonly float _m;       // 퍼지 지수. 2.0이면 표준 FCM
    private readonly int _maxIter;
    private readonly float _tol;

    private readonly Queue<float[]> _buffer;
    // 현재 클러스터 중심 (스케일된 공간 기준). null이면 아직 학습 전
    private float[][] _centers;

    // StandardScaler 상태 — Refit 때마다 버퍼 기준으로 갱신된다
    private float[] _mean;
    private float[] _std;

    // 버퍼에 FCM을 돌리기에 충분한 데이터가 있는지 여부.
    public bool HasEnoughData => _buffer.Count >= _nClusters;

    public SlidingWindowFCM(int windowSize = 50, int nClusters = 3,
                             float m = 2.0f, int maxIter = 50, float tol = 1e-4f)
    {
        _windowSize = windowSize;
        _nClusters  = nClusters;
        _m          = m;
        _maxIter    = maxIter;
        _tol        = tol;
        _buffer     = new Queue<float[]>();
    }

    // 새 플레이어 행동 로그를 슬라이딩 윈도우 버퍼에 추가한다.
    // 버퍼가 windowSize를 초과하면 가장 오래된 항목을 제거한다.
    public void AddLog(float[] log)
    {
        _buffer.Enqueue(log);
        if (_buffer.Count > _windowSize)
            _buffer.Dequeue();
    }

    // 버퍼의 데이터 전체로 StandardScaler + FCM을 재학습한다.
    // 이전 클러스터 중심을 초기값으로 넘겨 수렴 속도를 높인다.
    // HasEnoughData가 false이면 아무 것도 하지 않는다.
    public void Refit()
    {
        if (!HasEnoughData) return;

        float[][] X = _buffer.ToArray();

        FitScaler(X);
        float[][] Xs = TransformAll(X);
        RunFCM(Xs, _centers);
    }

    // log 하나의 소속도 벡터를 반환한다.
    // 반환값 u[i]: i번 클러스터에 속할 확률 (합산 = 1).
    // Refit이 한 번도 호출되지 않은 경우 균등 소속도를 반환한다.
    public float[] SoftPredict(float[] log)
    {
        if (_centers == null || _mean == null)
        {
            float[] uniform = new float[_nClusters];
            for (int i = 0; i < _nClusters; i++) uniform[i] = 1f / _nClusters;
            return uniform;
        }

        float[] scaled = Transform(log);
        return ComputeMembership(new float[][] { scaled }, _centers)[0];
    }

    // ─── 내부 메서드 ─────────────────────────────────────────────────────────

    private void FitScaler(float[][] X)
    {
        int n = X.Length;
        int d = X[0].Length;
        _mean = new float[d];
        _std  = new float[d];

        for (int j = 0; j < d; j++)
        {
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += X[i][j];
            _mean[j] = sum / n;
        }

        for (int j = 0; j < d; j++)
        {
            float sq = 0f;
            for (int i = 0; i < n; i++)
            {
                float diff = X[i][j] - _mean[j];
                sq += diff * diff;
            }
            _std[j] = Mathf.Max(Mathf.Sqrt(sq / n), 1e-8f);
        }
    }

    private float[] Transform(float[] x)
    {
        float[] scaled = new float[x.Length];
        for (int j = 0; j < x.Length; j++)
            scaled[j] = (x[j] - _mean[j]) / _std[j];
        return scaled;
    }

    private float[][] TransformAll(float[][] X)
    {
        float[][] result = new float[X.Length][];
        for (int i = 0; i < X.Length; i++)
            result[i] = Transform(X[i]);
        return result;
    }

    private void RunFCM(float[][] Xs, float[][] initCenters)
    {
        int n = Xs.Length;
        int d = Xs[0].Length;

        float[][] u = (initCenters != null)
            ? ComputeMembership(Xs, initCenters)
            : RandomMembership(n);

        float[][] centers = null;

        for (int iter = 0; iter < _maxIter; iter++)
        {
            centers = UpdateCenters(Xs, u, d);

            float[][] uNew = ComputeMembership(Xs, centers);

            if (MembershipNorm(uNew, u) < _tol) break;

            u = uNew;
        }

        _centers = centers;
    }

    private float[][] ComputeMembership(float[][] X, float[][] centers)
    {
        int n   = X.Length;
        float exp = 2.0f / (_m - 1f);

        float[][] u = new float[n][];

        for (int k = 0; k < n; k++)
        {
            float[] dist = new float[_nClusters];
            for (int i = 0; i < _nClusters; i++)
                dist[i] = Mathf.Max(EuclideanDist(X[k], centers[i]), 1e-10f);

            float[] dp       = new float[_nClusters];
            float   sumInvDp = 0f;
            for (int i = 0; i < _nClusters; i++)
            {
                dp[i]      = Mathf.Pow(dist[i], exp);
                sumInvDp  += 1f / dp[i];
            }

            u[k] = new float[_nClusters];
            for (int i = 0; i < _nClusters; i++)
                u[k][i] = 1f / (dp[i] * sumInvDp);
        }

        return u;
    }

    private float[][] UpdateCenters(float[][] X, float[][] u, int d)
    {
        float[][] centers = new float[_nClusters][];

        for (int i = 0; i < _nClusters; i++)
        {
            centers[i] = new float[d];
            float sumUm = 0f;

            for (int k = 0; k < X.Length; k++)
            {
                float um = Mathf.Pow(u[k][i], _m);
                sumUm += um;
                for (int j = 0; j < d; j++)
                    centers[i][j] += um * X[k][j];
            }

            if (sumUm < 1e-10f)
            {
                for (int j = 0; j < d; j++)
                {
                    float total = 0f;
                    for (int k = 0; k < X.Length; k++) total += X[k][j];
                    centers[i][j] = total / X.Length;
                }
            }
            else
            {
                for (int j = 0; j < d; j++)
                    centers[i][j] /= sumUm;
            }
        }

        return centers;
    }

    private float[][] RandomMembership(int n)
    {
        float[][] u = new float[n][];
        for (int k = 0; k < n; k++)
        {
            u[k] = new float[_nClusters];
            float sum = 0f;
            for (int i = 0; i < _nClusters; i++)
            {
                u[k][i] = Random.Range(0.01f, 1f);
                sum += u[k][i];
            }
            for (int i = 0; i < _nClusters; i++)
                u[k][i] /= sum;
        }
        return u;
    }

    private float MembershipNorm(float[][] a, float[][] b)
    {
        float sum = 0f;
        for (int k = 0; k < a.Length; k++)
            for (int i = 0; i < _nClusters; i++)
            {
                float diff = a[k][i] - b[k][i];
                sum += diff * diff;
            }
        return Mathf.Sqrt(sum);
    }

    private float EuclideanDist(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return Mathf.Sqrt(sum);
    }
}
