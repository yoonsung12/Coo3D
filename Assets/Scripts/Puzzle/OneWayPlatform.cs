using Sirenix.OdinInspector;
using UnityEngine;

// 아래에서 위로는 그냥 통과하고, 위에서는 밟고 설 수 있는 한 방향 발판이다.
// CharacterController도 내부적으로 Collider이기 때문에 Physics.IgnoreCollision을 그대로 쓸 수 있다.
[RequireComponent(typeof(Collider))]
public class OneWayPlatform : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("Player CharacterController")]
    private CharacterController playerController;
    // Physics.IgnoreCollision에 넘길 실제 Collider(=CharacterController)다. Player와 같은 오브젝트를 연결한다.

    [Title("판정 설정")]
    [SerializeField, LabelText("착지 판정 여유값")]
    private float surfaceSkin = 0.1f;
    // 발이 발판 윗면보다 이 값만큼 아래에 있어야 "아래에 있다(통과 가능)"로 판정한다.
    // 너무 작으면 발판 위에 서 있을 때 미세한 흔들림으로 매 프레임 충돌이 켜졌다 꺼졌다 할 수 있다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 충돌 무시 중")]
    private bool _isIgnoring;

    private Collider _platformCollider;

    private void Awake()
    {
        _platformCollider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 무시 상태를 남겨두면 다시 켜져도 계속 통과되는 채로 남을 수 있어 원래대로 되돌린다.
        if (_isIgnoring && playerController != null)
        {
            Physics.IgnoreCollision(playerController, _platformCollider, false);
            _isIgnoring = false;
        }
    }

    private void Update()
    {
        if (playerController == null) return;

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
}
