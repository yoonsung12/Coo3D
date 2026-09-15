using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 겨울 패턴 인트로에서 보스가 "쾅" 착지하는 순간 재생되는 냉기 충격파 연출이다.
// 동심원 링(충격이 퍼져나가는 느낌) + 사방으로 뻗는 얼음 균열(LineRenderer 지그재그)로 구성된다.
// LineRenderer의 폭(widthMultiplier)을 0으로 줄여서 사라지게 하는 방식을 쓰는데, 이는 Material을
// Transparent로 바꾸는 대신 선택한 방법이다(투명 전환은 URP 셰이더 키워드/렌더큐 설정이 꼬이면
// 안 보이거나 이상하게 렌더링될 위험이 있어서, 기존 PollenTrail 연출과 같은 이유로 피했다).
public class BossColdShockwave : MonoBehaviour
{
    [Title("충격 링 설정")]
    [SerializeField, LabelText("링 개수")]
    private int ringCount = 2;
    // Inspector에서 동시에 퍼지는 링 개수를 조절한다. 2개면 살짝 시간차를 둔 이중 파동이 된다.

    [SerializeField, LabelText("링 시작 반경")]
    private float ringStartRadius = 1.0f;
    // 0(점)이 아니라 보스 몸 크기(BoxCollider 2x2x2 기준)만큼부터 시작해서, 몸에서 뿜어져 나오는 느낌을 준다.

    [SerializeField, LabelText("링 최대 반경")]
    private float ringMaxRadius = 36f;
    // BossArena Floor 폭(68)의 절반(34)에 여유를 더해 맵 끝까지 닿도록 잡은 값이다.

    [SerializeField, LabelText("링 확산 시간")]
    private float ringExpandDuration = 1.0f;
    // 맵 전체를 가로지르는 큰 반경이라, 순간이동처럼 보이지 않게 충분히 늘렸다.

    [SerializeField, LabelText("링 사라지는 시간")]
    private float ringFadeDuration = 0.25f;

    [SerializeField, LabelText("링 간 딜레이")]
    private float ringDelayStep = 0.12f;

    [SerializeField, LabelText("링 두께")]
    private float ringWidth = 0.25f;

    [SerializeField, LabelText("링 세그먼트 수")]
    private int ringSegments = 64;
    // 원을 그릴 때 몇 각형으로 근사할지 정한다. 반경이 커진 만큼 매끄럽게 보이도록 늘렸다.

    [SerializeField, LabelText("링 재질")]
    private Material ringMaterial;

    [Title("냉기 균열 설정")]
    [SerializeField, LabelText("균열 개수")]
    private int crackCount = 8;
    // 착지 지점에서 사방으로 뻗어나가는 균열 줄기 개수다.

    [SerializeField, LabelText("균열 길이")]
    private float crackLength = 5f;

    [SerializeField, LabelText("균열 지그재그 단계")]
    private int crackSegments = 4;
    // 값이 클수록 균열 한 줄기가 더 잘게 꺾인다.

    [SerializeField, LabelText("균열 지그재그 강도")]
    private float crackJitter = 0.4f;

    [SerializeField, LabelText("균열 두께")]
    private float crackWidth = 0.08f;

    [SerializeField, LabelText("균열 등장 시간")]
    private float crackPopDuration = 0.12f;
    // 균열이 두께 0에서 원래 두께까지 순식간에 "쩌적" 나타나는 시간이다.

    [SerializeField, LabelText("균열 유지 시간")]
    private float crackHoldDuration = 0.3f;

    [SerializeField, LabelText("균열 사라지는 시간")]
    private float crackFadeDuration = 0.35f;

    [SerializeField, LabelText("균열 재질")]
    private Material crackMaterial;

    private LineRenderer[] _rings;
    private LineRenderer[] _cracks;
    private Sequence[] _ringSequences;
    private Sequence[] _crackSequences;

    private void Awake()
    {
        _rings = new LineRenderer[ringCount];
        _ringSequences = new Sequence[ringCount];
        for (int i = 0; i < ringCount; i++)
            _rings[i] = CreateLine("Ring_" + i, ringMaterial, ringWidth, ringSegments + 1, true);

        _cracks = new LineRenderer[crackCount];
        _crackSequences = new Sequence[crackCount];
        for (int i = 0; i < crackCount; i++)
            _cracks[i] = CreateLine("Crack_" + i, crackMaterial, crackWidth, crackSegments + 1, false);
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 재생 중이던 Tween을 정리해서 이미 없어진 LineRenderer를 건드리는 오류를 막는다.
        KillAllSequences();
    }

    private void OnDisable()
    {
        KillAllSequences();
    }

    private void KillAllSequences()
    {
        if (_ringSequences != null)
            foreach (var s in _ringSequences) s?.Kill();
        if (_crackSequences != null)
            foreach (var s in _crackSequences) s?.Kill();
    }

    private LineRenderer CreateLine(string name, Material mat, float width, int pointCount, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        // 월드 좌표를 직접 계산해서 넣는다(로컬 좌표를 쓰면 EnemyMovement.FaceDirection()이 보스를
        // 좌우로 뒤집을 때 쓰는 Y축 90/90도 회전이 그대로 적용되어, 카메라를 향해야 할 링/균열 평면이
        // 옆으로 돌아가 버리는 문제가 있었다 — AttackPoint/ProjectileSpawnPoint에서 겪은 것과 같은 종류의 버그).
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.widthMultiplier = 0f;
        lr.positionCount = pointCount;
        lr.numCapVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        if (mat != null) lr.material = mat;
        lr.enabled = false;
        return lr;
    }

    // BossWinterPattern이 인트로의 "쾅" 착지 순간에 호출한다.
    [Button("냉기 충격파 테스트")]
    public void Play()
    {
        for (int i = 0; i < _rings.Length; i++)
            PlayRing(i);

        for (int i = 0; i < _cracks.Length; i++)
            PlayCrack(i);
    }

    private void PlayRing(int index)
    {
        LineRenderer lr = _rings[index];
        float delay = index * ringDelayStep;

        _ringSequences[index]?.Kill();
        lr.enabled = true;
        lr.widthMultiplier = ringWidth;
        UpdateRingRadius(lr, ringStartRadius);

        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(delay);
        seq.Append(DOTween.To(() => ringStartRadius, r => UpdateRingRadius(lr, r), ringMaxRadius, ringExpandDuration).SetEase(Ease.OutQuad));
        seq.Append(DOTween.To(() => lr.widthMultiplier, w => lr.widthMultiplier = w, 0f, ringFadeDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => lr.enabled = false);
        _ringSequences[index] = seq;
    }

    // 원 둘레 좌표를 매 프레임 다시 계산해서 링을 키운다. transform.localScale을 쓰지 않는 이유는
    // LineRenderer의 두께가 스케일에 같이 딸려 늘어나 버려서, 링이 커질수록 두꺼워지는 문제가 생기기 때문이다.
    private void UpdateRingRadius(LineRenderer lr, float radius)
    {
        Vector3 origin = transform.position;
        int segs = lr.positionCount - 1;
        for (int p = 0; p <= segs; p++)
        {
            float t = (float)p / segs * Mathf.PI * 2f;
            lr.SetPosition(p, origin + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    private void PlayCrack(int index)
    {
        LineRenderer lr = _cracks[index];

        // 8방향으로 고르게 퍼지되, 방향마다 살짝 랜덤 각도를 줘서 기계적으로 정렬된 느낌을 피한다.
        float baseAngle = (360f / _cracks.Length) * index + Random.Range(-10f, 10f);
        float rad = baseAngle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        Vector3 origin = transform.position;

        int segs = lr.positionCount - 1;
        for (int p = 0; p <= segs; p++)
        {
            float t = (float)p / segs;
            Vector3 point = origin + dir * (crackLength * t);
            if (p > 0 && p < segs)
                point += perp * Random.Range(-crackJitter, crackJitter);
            lr.SetPosition(p, point);
        }

        _crackSequences[index]?.Kill();
        lr.enabled = true;
        lr.widthMultiplier = 0f;

        Sequence seq = DOTween.Sequence();
        seq.Append(DOTween.To(() => lr.widthMultiplier, w => lr.widthMultiplier = w, crackWidth, crackPopDuration).SetEase(Ease.OutQuad));
        seq.AppendInterval(crackHoldDuration);
        seq.Append(DOTween.To(() => lr.widthMultiplier, w => lr.widthMultiplier = w, 0f, crackFadeDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => lr.enabled = false);
        _crackSequences[index] = seq;
    }
}
