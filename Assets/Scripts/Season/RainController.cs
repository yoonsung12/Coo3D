using Sirenix.OdinInspector;
using UnityEngine;

// 여름 계절 방해 요소인 비를 스폰 범위 내에서 ON/OFF 주기로 내리게 하는 구름이다.
// IBlowable을 구현해 선풍기 바람에 밀리고, 바람이 없으면 원래 자리로 복귀한다.
[RequireComponent(typeof(Collider))]
public class RainController : BaseHazard, IBlowable
{
    [Title("비 ON/OFF 주기")]
    [SerializeField, LabelText("비 내리는 시간(초)")]
    private float rainOnDuration = 4f;
    // 비가 내리는 시간이다. 이 시간이 지나면 비가 멈춘다.

    [SerializeField, LabelText("비 멈추는 시간(초)")]
    private float rainOffDuration = 3f;
    // 비가 멈추는 시간이다. 이 시간이 지나면 다시 비가 내린다.

    [Title("빗방울 설정")]
    [SerializeField, LabelText("빗방울 프리팹")]
    private GameObject rainDropPrefab;

    [SerializeField, LabelText("빗방울 생성 간격(초)")]
    private float dropInterval = 0.1f;
    // 값이 작을수록 비가 더 촘촘하게 내린다.

    [Title("구름 밀기 설정")]
    [SerializeField, LabelText("밀리는 속도 (유닛/초)"), Range(0.1f, 5f)]
    private float pushSpeed = 1.5f;

    [SerializeField, LabelText("최대 밀림 거리"), Range(1f, 10f)]
    private float maxPushDistance = 4f;

    [SerializeField, LabelText("복귀 속도 (유닛/초)"), Range(0.1f, 5f)]
    private float returnSpeed = 0.8f;
    // pushSpeed보다 작게 설정하면 밀기보다 느리게 복귀한다.

    [SerializeField, LabelText("밀리는 축")]
    private Vector3 pushAxis = Vector3.right;
    // 구름이 밀리고 빗방울이 퍼지는 월드 방향이다. 기본 사이드뷰는 (1, 0, 0) 그대로 둔다.
    // -Z 통로(SideViewAxisZone)처럼 진행 축이 바뀐 곳에 둔 구름은 (0, 0, -1)처럼 통로 진행 방향을 넣는다.

    [SerializeField, LabelText("바람이 멈춰도 그 자리에 머물기")]
    private bool stayWhenPushed;
    // 켜면 선풍기를 멈춰도 원위치로 돌아가지 않는다. 구름을 옮겨 놓아야 풀리는 퍼즐용 구름에 켠다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 밀린 거리")]
    private float _currentOffset;

    [ReadOnly, ShowInInspector, LabelText("비 내리는 중")]
    private bool _isRaining;

    private float _stateTimer;
    private float _dropTimer;

    private Vector3 _homePosition;
    // Awake에서 저장한 구름의 원래 위치다. 복귀 기준점으로 사용한다.

    private float _lastBlownTime = -999f;
    // 마지막으로 OnBlown이 호출된 시간이다. Time.time과 비교해 선풍기가 멈췄는지 판단한다.

    private float _pendingPushSign;
    // OnBlown에서 받은 바람이 밀리는 축의 어느 쪽을 향하는지다 (+1 축 방향, -1 반대 방향).

    private Vector3 PushAxis => pushAxis.sqrMagnitude > 0.0001f ? pushAxis.normalized : Vector3.right;
    // Inspector에 (0, 0, 0)이 들어가도 깨지지 않도록 기본 +X로 대신한다.

    private const float BlownCooldown = 0.15f;
    // 이 시간(초) 이내에 OnBlown이 호출되면 선풍기가 켜진 상태로 간주한다.
    // FanTool은 FixedUpdate에서 호출하므로 약간의 여유를 둔다.

    private void Awake()
    {
        _homePosition = transform.position;
        GetComponent<Collider>().isTrigger = true;
        // 구름은 물리 충돌이 필요 없는 연출용 오브젝트라 감지 전용 트리거로 둔다.
    }

    private void Update()
    {
        UpdateRainState();
        UpdateCloudPosition();

        if (_isRaining)
            SpawnRainDrops();
    }

    private void UpdateRainState()
    {
        _stateTimer += Time.deltaTime;
        float duration = _isRaining ? rainOnDuration : rainOffDuration;

        if (_stateTimer >= duration)
        {
            _stateTimer = 0f;
            _isRaining = !_isRaining;
        }
    }

    private void SpawnRainDrops()
    {
        _dropTimer += Time.deltaTime;

        if (_dropTimer >= dropInterval)
        {
            _dropTimer = 0f;
            Instantiate(rainDropPrefab, GetRainSpawnPosition(), Quaternion.identity);
        }
    }

    // 밀리는 축을 따라 ±spawnHalfWidth 범위에 빗방울을 퍼뜨린다.
    // BaseHazard.GetSpawnPosition()은 X축으로만 퍼뜨려서, -Z 통로에서는 카메라 기준 깊이 방향으로 퍼져 비가 한 줄기로 보인다.
    // 축이 기본(+X)이면 기존과 똑같은 위치가 나온다.
    private Vector3 GetRainSpawnPosition()
    {
        float offset = Random.Range(-spawnHalfWidth, spawnHalfWidth);
        return transform.position + PushAxis * offset;
    }

    // 매 프레임 구름 위치를 갱신한다.
    // 선풍기 바람을 받는 동안은 천천히 밀리고, 바람이 멈추면 원위치로 천천히 복귀한다.
    private void UpdateCloudPosition()
    {
        bool isBeingBlown = Time.time - _lastBlownTime < BlownCooldown;

        if (isBeingBlown)
        {
            float targetOffset = _pendingPushSign * maxPushDistance;
            _currentOffset = Mathf.MoveTowards(_currentOffset, targetOffset, pushSpeed * Time.deltaTime);
        }
        else if (!stayWhenPushed)
        {
            _currentOffset = Mathf.MoveTowards(_currentOffset, 0f, returnSpeed * Time.deltaTime);
        }

        // 원래 위치에서 밀리는 축 방향으로 _currentOffset만큼 떨어진 곳에 둔다.
        transform.position = _homePosition + PushAxis * _currentOffset;
    }

    // FanTool의 바람 판정에 감지되면 매 프레임 호출된다.
    // 바람 방향을 저장하고 _lastBlownTime을 갱신한다. 실제 이동은 UpdateCloudPosition에서 처리한다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        _lastBlownTime = Time.time;
        _pendingPushSign = Mathf.Sign(Vector3.Dot(direction, PushAxis));
        // 바람 방향 중 밀리는 축 성분의 부호만 사용한다. 구름은 이 축을 따라 앞뒤로만 밀린다.
    }

    [Title("테스트")]
    [Button("축 방향으로 밀기 테스트")]
    private void TestPushRight()
    {
        if (Application.isPlaying) OnBlown(PushAxis, 8f);
    }

    [Button("축 반대 방향으로 밀기 테스트")]
    private void TestPushLeft()
    {
        if (Application.isPlaying) OnBlown(-PushAxis, 8f);
    }

    [Button("강제 복귀")]
    private void TestReturn()
    {
        if (Application.isPlaying) _lastBlownTime = -999f;
    }
}
