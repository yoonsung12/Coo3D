using UnityEngine;
using Sirenix.OdinInspector;

// PlayerController의 이동 상태를 읽어 Animator 파라미터를 업데이트하는 브릿지 스크립트다.
// PlayerController와 Animator를 직접 연결하지 않고 이 스크립트가 중간에서 연결한다.
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorController : MonoBehaviour
{
    [Title("전환 설정")]
    [SerializeField, LabelText("Walk 진입 속도 임계값")]
    private float walkThreshold = 0.1f;
    // 수평 이동 속도가 이 값 이상이면 Walk 애니메이션으로 전환한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 수평 속도")]
    private float _currentSpeed;

    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    private bool _isGrounded;

    // Animator 파라미터 이름을 미리 해시로 변환해 매 프레임 string 비교 비용을 줄인다.
    private static readonly int SpeedHash        = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash   = Animator.StringToHash("IsGrounded");
    private static readonly int JumpTriggerHash  = Animator.StringToHash("JumpTrigger");
    private static readonly int LandTriggerHash  = Animator.StringToHash("LandTrigger");

    private Animator _animator;
    private CharacterController _cc;
    private PlayerController _playerController;

    // 점프/착지 감지를 위해 이전 프레임 접지 상태를 기억한다.
    private bool _wasGrounded = true;

    private void Awake()
    {
        _animator         = GetComponent<Animator>();
        _cc               = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();
    }

    // LateUpdate: PlayerController.Update()의 Move() 호출 이후 IsGrounded 최신 값을 읽기 위해 사용한다.
    private void LateUpdate()
    {
        UpdateSpeed();
        UpdateGrounded();
    }

    private void UpdateSpeed()
    {
        // 수직 속도(점프/낙하)는 제외하고 수평 이동 속도만 계산한다.
        // 제자리 점프 시 Walk 애니메이션이 재생되지 않도록 하기 위해서다.
        Vector3 horizontal = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        _currentSpeed = horizontal.magnitude;
        _animator.SetFloat(SpeedHash, _currentSpeed);
    }

    private void UpdateGrounded()
    {
        _isGrounded = _playerController.IsGrounded;
        _animator.SetBool(IsGroundedHash, _isGrounded);

        // 이전 프레임에 접지 상태였고 현재 공중이면 점프가 시작된 것으로 판단한다.
        if (_wasGrounded && !_isGrounded)
            _animator.SetTrigger(JumpTriggerHash);

        // 이전 프레임에 공중이었고 현재 접지되면 착지한 것으로 판단한다.
        if (!_wasGrounded && _isGrounded)
            _animator.SetTrigger(LandTriggerHash);

        _wasGrounded = _isGrounded;
    }
}
