using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 선풍기로 미는 상자(WoodenBox)가 구석에 끼거나 밖으로 떨어졌을 때 처음 자리로 되돌리는 컴포넌트다.
// - 일정 시간 동안 바람을 맞지 않으면 사라졌다가 처음 자리에 다시 나타난다.
// - 처음 높이보다 많이 떨어지면 기다리지 않고 바로 되돌린다.
// - 퍼즐을 풀어 Lock()되면 더 이상 되돌리지 않는다.
[RequireComponent(typeof(Rigidbody), typeof(WoodenBox))]
public class BoxRespawner : MonoBehaviour
{
    [Title("리스폰 조건")]
    [SerializeField, LabelText("방치 시간(초)")]
    private float idleRespawnTime = 10f;
    // 마지막으로 바람을 맞은 뒤 이 시간이 지나면 처음 자리로 되돌린다. 짧을수록 끼었을 때 빨리 돌아온다.

    [SerializeField, LabelText("낙하 판정 깊이")]
    private float fallDepth = 2f;
    // 처음 높이보다 이만큼 아래로 내려가면 밖으로 떨어진 것으로 보고 즉시 되돌린다.

    [SerializeField, LabelText("처음 자리 판정 거리")]
    private float homeTolerance = 0.5f;
    // 처음 위치에서 이 거리 안에 있으면 "안 움직인 상태"로 보고 방치 타이머를 돌리지 않는다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("사라지는 시간")]
    private float vanishDuration = 0.3f;

    [SerializeField, LabelText("나타나는 시간")]
    private float appearDuration = 0.4f;

    [SerializeField, LabelText("사라질 때 Ease")]
    private Ease vanishEase = Ease.InBack;

    [SerializeField, LabelText("나타날 때 Ease")]
    private Ease appearEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("리스폰까지 남은 시간")]
    private float _remainingTime;

    [ReadOnly, ShowInInspector, LabelText("잠김(퍼즐 완료)")]
    private bool _isLocked;

    [ReadOnly, ShowInInspector, LabelText("리스폰 연출 중")]
    private bool _isRespawning;

    private Rigidbody _rb;
    private WoodenBox _woodenBox;
    private Vector3 _homePosition;
    private Quaternion _homeRotation;
    private Vector3 _homeScale;
    private float _lastBlownTime;
    private Sequence _respawnSequence;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _woodenBox = GetComponent<WoodenBox>();

        // 씬에 배치된 위치를 "처음 자리"로 기억한다.
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        _homeScale = transform.localScale;
        _lastBlownTime = Time.time;
    }

    private void OnEnable() => _woodenBox.Blown += HandleBlown;

    private void OnDisable() => _woodenBox.Blown -= HandleBlown;

    private void OnDestroy() => _respawnSequence?.Kill();

    private void HandleBlown() => _lastBlownTime = Time.time;

    private void Update()
    {
        if (_isLocked || _isRespawning) return;

        // 밖으로 떨어졌다면 방치 시간과 상관없이 바로 되돌린다.
        if (transform.position.y < _homePosition.y - fallDepth)
        {
            Respawn();
            return;
        }

        // 처음 자리에 그대로 있으면 되돌릴 이유가 없으므로 타이머를 계속 새로 시작한다.
        if (Vector3.Distance(transform.position, _homePosition) <= homeTolerance)
        {
            _lastBlownTime = Time.time;
            _remainingTime = idleRespawnTime;
            return;
        }

        _remainingTime = idleRespawnTime - (Time.time - _lastBlownTime);
        if (_remainingTime <= 0f)
            Respawn();
    }

    // 퍼즐 완료 시 BoxLaneGoal이 호출한다. 진행 중인 리스폰 연출도 멈춰 상자가 그 자리에 남게 한다.
    public void Lock()
    {
        _isLocked = true;
        _respawnSequence?.Kill();
        _isRespawning = false;
        transform.localScale = _homeScale;
    }

    // 퍼즐 리셋 시 BoxLaneGoal이 호출한다.
    public void Unlock()
    {
        _isLocked = false;
        _lastBlownTime = Time.time;
    }

    private void Respawn()
    {
        _isRespawning = true;

        // 연출 중에는 물리를 멈춰, 작아지는 동안 굴러가거나 바람에 밀리지 않게 한다.
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;

        _respawnSequence?.Kill();
        _respawnSequence = DOTween.Sequence()
            .Append(transform.DOScale(Vector3.zero, vanishDuration).SetEase(vanishEase))
            .AppendCallback(() =>
                // kinematic 상태라 transform을 직접 옮겨도 물리 충돌 보정이 걸리지 않는다.
                transform.SetPositionAndRotation(_homePosition, _homeRotation))
            .Append(transform.DOScale(_homeScale, appearDuration).SetEase(appearEase))
            .OnComplete(() =>
            {
                _rb.isKinematic = false;
                _isRespawning = false;
                _lastBlownTime = Time.time;
            });
    }

    [Button("리스폰 테스트")]
    private void TestRespawn()
    {
        if (!_isLocked) Respawn();
    }
}
