using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 아래에서 위로는 그냥 통과하고, 위에서는 밟고 설 수 있는 한 방향 발판이다.
// 서 있는 상태에서 PlayerController가 "발판 통과(드롭스루)"를 요청하면 잠깐 충돌을 무시해서 아래로 떨어뜨린다.
// CharacterController도 내부적으로 Collider이기 때문에 Physics.IgnoreCollision을 그대로 쓸 수 있다.
[RequireComponent(typeof(Collider))]
public class OneWayPlatform : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("Player")]
    private PlayerController player;
    // Inspector에서 씬의 Player 오브젝트를 연결한다.

    [SerializeField, LabelText("Player CharacterController")]
    private CharacterController playerController;
    // Physics.IgnoreCollision에 넘길 실제 Collider(=CharacterController)다. Player와 같은 오브젝트를 연결한다.

    [Title("판정 설정")]
    [SerializeField, LabelText("착지 판정 여유값")]
    private float surfaceSkin = 0.1f;
    // 발이 발판 윗면보다 이 값만큼 아래에 있어야 "아래에 있다(통과 가능)"로 판정한다.
    // 너무 작으면 발판 위에 서 있을 때 미세한 흔들림으로 매 프레임 충돌이 켜졌다 꺼졌다 할 수 있다.

    [SerializeField, LabelText("드롭스루 유지 시간")]
    private float dropThroughDuration = 0.4f;
    // 드롭스루 요청을 받았을 때, 이 시간 동안은 발 위치와 무관하게 강제로 충돌을 무시한다.
    // 플레이어가 발판 두께를 완전히 벗어나기에 충분한 시간으로 잡는다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 충돌 무시 중")]
    private bool _isIgnoring;

    [ReadOnly, ShowInInspector, LabelText("드롭스루 진행 중")]
    private bool _isDropping;

    private Collider _platformCollider;

    private void Awake()
    {
        _platformCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (player != null)
            player.OnDropThroughRequested += HandleDropThroughRequested;
    }

    private void OnDisable()
    {
        if (player != null)
            player.OnDropThroughRequested -= HandleDropThroughRequested;

        // 오브젝트가 비활성화될 때 무시 상태를 남겨두면 다시 켜져도 계속 통과되는 채로 남을 수 있어 원래대로 되돌린다.
        if (_isIgnoring && playerController != null)
        {
            Physics.IgnoreCollision(playerController, _platformCollider, false);
            _isIgnoring = false;
        }
    }

    private void Update()
    {
        if (_isDropping || playerController == null) return;

        // 플레이어의 발(=CharacterController 바닥면)이 발판 윗면보다 아래에 있으면 통과시킨다.
        bool isBelowSurface = playerController.bounds.min.y < _platformCollider.bounds.max.y - surfaceSkin;
        SetIgnoring(isBelowSurface);
    }

    private void SetIgnoring(bool ignore)
    {
        if (ignore == _isIgnoring) return;

        Physics.IgnoreCollision(playerController, _platformCollider, ignore);
        _isIgnoring = ignore;
    }

    // PlayerController가 "아래+점프"를 눌렀을 때 씬의 모든 OneWayPlatform이 이 이벤트를 받는다.
    // 그중 실제로 플레이어를 떠받치고 있는 발판만 반응해서 떨어뜨린다.
    private void HandleDropThroughRequested()
    {
        if (_isDropping || !IsPlayerStandingOnTop()) return;
        StartCoroutine(DropThroughRoutine());
    }

    // 발이 발판 윗면 높이에 걸쳐 있고, 수평으로도 발판 범위 안에 있어야 "이 발판 위에 서 있다"로 판정한다.
    private bool IsPlayerStandingOnTop()
    {
        Bounds platformBounds = _platformCollider.bounds;
        float feetY = playerController.bounds.min.y;

        bool onSurfaceHeight = Mathf.Abs(feetY - platformBounds.max.y) <= surfaceSkin;
        bool withinHorizontalRange = platformBounds.min.x <= playerController.bounds.max.x
            && platformBounds.max.x >= playerController.bounds.min.x;

        return onSurfaceHeight && withinHorizontalRange;
    }

    private IEnumerator DropThroughRoutine()
    {
        _isDropping = true;
        SetIgnoring(true);

        yield return new WaitForSeconds(dropThroughDuration);

        _isDropping = false;
        // 남은 판정은 다음 Update()가 발 위치를 다시 확인해서 알아서 정리한다.
    }
}
