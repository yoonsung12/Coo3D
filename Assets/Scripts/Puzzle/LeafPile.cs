using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 구역 "공중 낙엽 점화" 퍼즐의 바닥 낙엽더미 자리다. 위에서 떨어지는 낙엽(LeafMound, 낙하 모드)이
// 닿으면 흡수되어 한 단계씩 차오르고, 가득 찼을 때 성냥으로 점화하면 그 자리에서 불덩이(LeafMound)를
// 만들어낸 뒤 빈 자리가 된다. 불 안 붙은 더미에 선풍기를 쏘면 한 번에 흩어져 빈 자리가 된다.
// 더미 자체는 사라지지 않는 "다시 채워지는 연료 자리"라서, 실패해도 낙엽이 쌓이면 다시 도전할 수 있다.
[RequireComponent(typeof(Collider))]
public class LeafPile : MonoBehaviour, IIgnitable, IBlowable
{
    [Title("채움 단계 설정")]
    [SerializeField, LabelText("최대 단계")]
    private int maxLevel = 3;
    // 가득 찬 것으로 보는 단계다. 이 단계에 도달해야 성냥으로 점화할 수 있다. 값이 클수록 오래 모아야 한다.

    [SerializeField, LabelText("시작 단계")]
    private int startLevel = 3;
    // 씬 시작 시 더미가 얼마나 차 있는지다. 0이면 빈 자리로 시작해 떨어지는 낙엽으로만 채워진다.

    [Title("연결")]
    [SerializeField, LabelText("더미 비주얼")]
    private Transform visual;
    // Inspector에서 단계에 따라 크기가 바뀔 자식 메시를 연결한다. 콜라이더는 이 오브젝트(루트)에 두어
    // 빈 자리(크기 0)여도 떨어지는 낙엽을 계속 받을 수 있게 한다.

    [SerializeField, LabelText("불덩이 프리팹")]
    private LeafMound burningLeafPrefab;
    // 점화했을 때 이 자리에서 생성할 LeafMound 프리팹(FallingLeaf.prefab)을 연결한다.

    [SerializeField, LabelText("불덩이 생성 높이")]
    private float spawnHeight = 0.5f;
    // 더미 위치 기준으로 불덩이를 얼마나 위에 생성할지다. 덩굴벽 높이에 맞춰 조정한다.

    [SerializeField, LabelText("흩어짐 파티클")]
    private ParticleSystem scatterParticle;
    // 선풍기로 흩어질 때 한 번 터지는 낙엽 파티클이다. 비워두면 크기 연출만 나온다.

    [Title("DOTween 연출 설정")]
    [SerializeField, LabelText("가득 찼을 때 크기")]
    private Vector3 fullScale = new Vector3(1f, 0.5f, 1f);
    // 최대 단계일 때 비주얼 크기다. 중간 단계는 (현재 단계 / 최대 단계) 비율로 줄어든다.

    [SerializeField, LabelText("크기 변화 시간")]
    private float scaleDuration = 0.3f;

    [SerializeField, LabelText("크기 변화 Ease")]
    private Ease scaleEase = Ease.OutBack;

    [SerializeField, LabelText("흡수 펀치 세기")]
    private float absorbPunch = 0.15f;
    // 낙엽이 쌓일 때 살짝 들썩이는 정도다. 0이면 들썩임 없이 크기만 바뀐다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private int _level;

    private bool IsFull => _level >= maxLevel;

    private Sequence _visualSequence;
    // 크기 변화/펀치 연출이 겹치지 않도록 현재 실행 중인 연출을 저장해 두고 새 연출 전에 Kill한다.

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        // 플레이어 이동을 막지 않고, 떨어지는 낙엽 흡수는 트리거로 판정하기 위함이다.

        _level = Mathf.Clamp(startLevel, 0, maxLevel);
        if (visual != null)
            visual.localScale = GetScaleForLevel(_level);
        // 시작 크기는 연출 없이 바로 맞춘다.
    }

    private void OnDestroy()
    {
        _visualSequence?.Kill();
    }

    // 떨어지는 낙엽(LeafMound)이 더미에 닿았을 때 호출된다. 불붙은 낙엽이 쌓여 더미가 가득 차면 불이 옮겨붙는다.
    public void AbsorbLeaf(bool wasBurning)
    {
        if (IsFull)
        {
            // 이미 가득 찼으면 더 쌓이지 않는다. 다만 불씨 낙엽이면 가득 찬 더미에 불이 옮겨붙는다.
            if (wasBurning) Ignite();
            return;
        }

        _level++;
        PlayScaleTween(withPunch: true);

        // "가득 차야만 점화" 규칙과 맞추기 위해, 불씨 낙엽은 먼저 쌓인 뒤 그걸로 가득 찼을 때만 불을 옮긴다.
        if (wasBurning && IsFull)
            Ignite();
    }

    // TorchTool의 SphereCast에 감지되면 호출된다. 가득 찬 더미만 불이 붙는다.
    public void OnIgnited()
    {
        if (!IsFull) return;
        Ignite();
    }

    // FanTool의 바람 판정에 감지되면 호출된다(바람을 쏘는 동안 매 물리 프레임 호출됨).
    // 불 안 붙은 더미는 한 번에 전부 흩어져 빈 자리가 된다. 이미 비어 있으면 아무 일도 없다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (_level <= 0) return;

        _level = 0;
        if (scatterParticle != null)
            scatterParticle.Emit(15);
        // Play() 대신 Emit()으로 정해진 개수만 한 번에 방출해 루프 설정과 상관없이 1회성으로 터지게 한다.

        PlayScaleTween(withPunch: false);
    }

    // 더미를 불덩이로 바꾼다. 불덩이는 생성 즉시 타기 시작하고, 이 자리는 빈 상태로 돌아가 다시 채워지길 기다린다.
    private void Ignite()
    {
        if (burningLeafPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * spawnHeight;
            LeafMound fire = Instantiate(burningLeafPrefab, spawnPos, Quaternion.identity);
            fire.IgniteAsLaunched();
        }

        _level = 0;
        PlayScaleTween(withPunch: false);
    }

    // 현재 단계에 맞는 크기로 부드럽게 바뀌는 연출이다. 낙엽이 쌓일 때는 끝에 살짝 들썩이는 펀치를 붙인다.
    private void PlayScaleTween(bool withPunch)
    {
        if (visual == null) return;

        _visualSequence?.Kill();
        _visualSequence = DOTween.Sequence();
        _visualSequence.Append(visual.DOScale(GetScaleForLevel(_level), scaleDuration).SetEase(scaleEase));

        if (withPunch && absorbPunch > 0f)
            _visualSequence.Append(visual.DOPunchScale(Vector3.one * absorbPunch, 0.25f, 6, 0.5f));
    }

    // 단계 비율(0~1)만큼 가득 찬 크기를 줄인다. 0단계면 크기 0이라 보이지 않는다.
    private Vector3 GetScaleForLevel(int level)
    {
        float ratio = maxLevel > 0 ? (float)level / maxLevel : 0f;
        return fullScale * ratio;
    }

    [Button("낙엽 +1 채우기 테스트")]
    private void TestAddLeaf() => AbsorbLeaf(false);

    [Button("흩날리기 테스트")]
    private void TestScatter() => OnBlown(Vector3.right, 8f);

    [Button("점화 테스트")]
    private void TestIgnite() => OnIgnited();
}
