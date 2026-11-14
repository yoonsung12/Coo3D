using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 코인, 회복 아이템처럼 "바닥에 놓였다가 플레이어가 다가오면 끌려와 흡수되는" 아이템의 공통 로직이다.
// 스폰 팝인 연출, 거리 감지 자동 흡수, 일정 시간 후 자동 소멸을 담당한다.
// 실제로 무엇을 주는지는 하위 클래스가 ApplyEffect()에서 구현한다 (코인 지급, 최대 체력 증가 등).
public abstract class BasePickupItem : MonoBehaviour
{
    [Title("흡수 설정")]
    [SerializeField, LabelText("자석 흡수 사용")]
    private bool useAttract = true;
    // 켜면 감지 범위 안에서 플레이어 쪽으로 끌려간다(코인 등 기존 방식).
    // 끄면 제자리에 머물고, 플레이어가 "획득 거리" 안까지 직접 다가와야 먹힌다(회복 하트).

    [SerializeField, LabelText("감지 범위"), ShowIf(nameof(useAttract))]
    private float attractRange = 3f;
    // 플레이어가 이 거리 안으로 들어오면 아이템이 플레이어 쪽으로 끌려가기 시작한다.

    [SerializeField, LabelText("흡수 속도"), ShowIf(nameof(useAttract))]
    private float attractSpeed = 8f;
    // 값이 클수록 플레이어에게 더 빠르게 빨려 들어간다.

    [SerializeField, LabelText("획득 거리")]
    private float pickupDistance = 0.3f;
    // 플레이어와 이 거리 이하로 가까워지면 즉시 획득 처리한다.

    [Title("스폰 연출")]
    [SerializeField, LabelText("스폰 팝인 시간")]
    private float spawnPunchDuration = 0.3f;
    // 아이템이 생성될 때 크기 0에서 1로 커지는 데 걸리는 시간이다.

    [Title("자동 소멸")]
    [SerializeField, LabelText("자동 소멸 시간 (0이면 소멸 안 함)")]
    private float lifetime = 20f;
    // 플레이어가 줍지 않고 이 시간이 지나면 자동으로 사라진다.

    private Transform _target;
    private bool _isCollected;
    private Tween _spawnTween;

    private void Awake()
    {
        transform.localScale = Vector3.zero;
    }

    // CoinItem처럼 하위 클래스가 자기만의 스폰 연출(회전 등)을 추가해야 할 수 있어 virtual로 열어둔다.
    // Unity는 Awake/Start 같은 메시지 메서드를 override 없이 하위 클래스가 같은 이름으로 다시 선언하면
    // 부모 쪽 버전을 호출하지 않고 덮어써 버리므로, 반드시 override + base.Start() 형태로 확장해야 한다.
    protected virtual void Start()
    {
        // DOTween으로 스폰 시 팝인 연출을 재생한다.
        _spawnTween = transform.DOScale(Vector3.one, spawnPunchDuration).SetEase(Ease.OutBack);

        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    protected virtual void OnDestroy()
    {
        _spawnTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    private void Update()
    {
        if (_isCollected) return;

        if (!useAttract)
        {
            // 자석 흡수를 끈 아이템은 움직이지 않고, 플레이어 몸이 획득 거리 안에 들어왔을 때만 먹힌다.
            if (PlayerHealth.Instance == null) return;
            if (DistanceToPlayerBody(PlayerHealth.Instance.transform) <= pickupDistance && CanCollect())
                Collect();
            return;
        }

        if (_target == null)
        {
            // GameObject.Find 대신, PlayerHealth가 스스로 등록해 둔 Instance로 플레이어 위치를 확인한다.
            if (PlayerHealth.Instance == null) return;

            float distanceToPlayer = Vector3.Distance(transform.position, PlayerHealth.Instance.transform.position);
            if (distanceToPlayer > attractRange) return;

            _target = PlayerHealth.Instance.transform;
        }

        float distance = Vector3.Distance(transform.position, _target.position);

        if (distance <= pickupDistance)
        {
            if (CanCollect()) Collect();
            return;
        }

        // 한 번 감지된 뒤로는 플레이어 쪽으로 계속 끌려간다.
        transform.position = Vector3.MoveTowards(transform.position, _target.position, attractSpeed * Time.deltaTime);
    }

    private void Collect()
    {
        _isCollected = true;
        _spawnTween?.Kill();

        ApplyEffect();
        Destroy(gameObject);
    }

    private CharacterController _playerBody;
    // 자석 흡수를 끈 아이템이 플레이어 몸(캡슐)까지의 거리를 재기 위해 캐시한다.

    // 플레이어 발끝 한 점이 아니라 CharacterController 캡슐(발~머리) 표면까지의 거리를 잰다.
    // 발끝 기준이면 아이템이 머리 높이쯤 떠 있을 때 바로 밑에 서도 거리가 멀게 나와 먹을 수 없었다.
    private float DistanceToPlayerBody(Transform player)
    {
        if (_playerBody == null || _playerBody.transform != player)
            _playerBody = player.GetComponent<CharacterController>();
        if (_playerBody == null)
            return Vector3.Distance(transform.position, player.position);

        // 캡슐의 위/아래 반구 중심을 잇는 선분에서 아이템과 가장 가까운 점을 구한 뒤, 반지름만큼 빼서 표면까지 거리로 만든다.
        Vector3 center = player.TransformPoint(_playerBody.center);
        float half = Mathf.Max(0f, _playerBody.height * 0.5f - _playerBody.radius);
        Vector3 top = center + Vector3.up * half;
        Vector3 bottom = center - Vector3.up * half;

        Vector3 segment = top - bottom;
        float t = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(transform.position - bottom, segment) / segment.sqrMagnitude) : 0f;
        Vector3 closest = bottom + segment * t;
        return Mathf.Max(0f, Vector3.Distance(transform.position, closest) - _playerBody.radius);
    }

    // 지금 먹을 수 있는지 하위 클래스가 판단한다. false면 아이템이 그대로 남는다.
    // 예: 회복 하트는 체력이 가득 차 있으면 먹지 않고 바닥에 남겨 둔다. 기본은 항상 먹을 수 있음.
    protected virtual bool CanCollect() => true;

    protected abstract void ApplyEffect();
}
