using System.Collections.Generic;
using UnityEngine;

// (상태, 소속도, 행동, 보상) 경험을 저장하는 링 버퍼다.
// 버퍼가 maxSize를 초과하면 가장 오래된 경험부터 제거한다.
// SampleMiniBatch()로 무작위 미니배치를 추출해 RBFNN 학습에 사용한다.
// 2D 원작의 ExperienceReplay를 그대로 포팅했다 — 순수 C# 컬렉션 로직이라 3D 전환과 무관하다.
public class ExperienceReplay
{
    // 경험 리플레이 버퍼에 저장되는 단일 경험 데이터.
    public struct Experience
    {
        public float[] state;      // 게임 상태 벡터 [거리, 체력]
        public float[] membership; // FCM 소속도 벡터 [클러스터0, 1, 2]
        public int     action;     // 선택된 전술 인덱스 (0/1/2)
        public float   reward;     // 수신한 보상값
    }

    private readonly int              _maxSize;
    private readonly List<Experience> _buffer;

    public int Count => _buffer.Count;

    public ExperienceReplay(int maxSize = 100)
    {
        _maxSize = maxSize;
        _buffer  = new List<Experience>(maxSize);
    }

    // 새 경험을 버퍼에 추가한다. 버퍼가 maxSize를 초과하면 가장 오래된 경험을 제거한다.
    public void Add(float[] state, float[] membership, int action, float reward)
    {
        _buffer.Add(new Experience
        {
            state      = state,
            membership = membership,
            action     = action,
            reward     = reward,
        });

        if (_buffer.Count > _maxSize)
            _buffer.RemoveAt(0);
    }

    // 버퍼에서 무작위로 batchSize개의 경험을 샘플링한다.
    // 버퍼 크기가 batchSize보다 작으면 버퍼 전체를 반환한다. Fisher-Yates 셔플로 중복 없이 추출한다.
    public List<Experience> SampleMiniBatch(int batchSize)
    {
        int actualSize = Mathf.Min(batchSize, _buffer.Count);
        List<Experience> batch = new List<Experience>(actualSize);

        List<int> indices = new List<int>(_buffer.Count);
        for (int i = 0; i < _buffer.Count; i++) indices.Add(i);

        for (int i = 0; i < actualSize; i++)
        {
            int r = Random.Range(i, indices.Count);
            int tmp = indices[i]; indices[i] = indices[r]; indices[r] = tmp;
            batch.Add(_buffer[indices[i]]);
        }

        return batch;
    }
}
