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

    private float _pendingPushDirX;
    // OnBlown에서 받은 바람의 X 방향이다 (+1 오른쪽, -1 왼쪽).

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
            Instantiate(rainDropPrefab, GetSpawnPosition(), Quaternion.identity);
        }
    }

    // 매 프레임 구름 위치를 갱신한다.
    // 선풍기 바람을 받는 동안은 천천히 밀리고, 바람이 멈추면 원위치로 천천히 복귀한다.
    private void UpdateCloudPosition()
    {
        bool isBeingBlown = Time.time - _lastBlownTime < BlownCooldown;

        if (isBeingBlown)
        {
            float targetOffset = _pendingPushDirX * maxPushDistance;
            _currentOffset = Mathf.MoveTowards(_currentOffset, targetOffset, pushSpeed * Time.deltaTime);
        }
        else
        {
            _currentOffset = Mathf.MoveTowards(_currentOffset, 0f, returnSpeed * Time.deltaTime);
        }

        transform.position = new Vector3(_homePosition.x + _currentOffset, _homePosition.y, _homePosition.z);
    }

    // FanTool의 바람 판정에 감지되면 매 프레임 호출된다.
    // 바람 방향을 저장하고 _lastBlownTime을 갱신한다. 실제 이동은 UpdateCloudPosition에서 처리한다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        _lastBlownTime = Time.time;
        _pendingPushDirX = Mathf.Sign(direction.x);
        // X 방향만 사용한다. 구름은 좌우로만 밀린다.
    }

    [Title("테스트")]
    [Button("오른쪽으로 밀기 테스트")]
    private void TestPushRight()
    {
        if (Application.isPlaying) OnBlown(Vector3.right, 8f);
    }

    [Button("왼쪽으로 밀기 테스트")]
    private void TestPushLeft()
    {
        if (Application.isPlaying) OnBlown(Vector3.left, 8f);
    }

    [Button("강제 복귀")]
    private void TestReturn()
    {
        if (Application.isPlaying) _lastBlownTime = -999f;
    }
}
