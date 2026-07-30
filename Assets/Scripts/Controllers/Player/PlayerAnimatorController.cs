using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorController : MonoBehaviour
{
    [Title("애니메이터 연결")]
    [SerializeField, LabelText("캐릭터 Animator")]
    private Animator animator;

    [Title("전환 설정")]
    [SerializeField, LabelText("Walk 진입 속도 임계값")]
    private float walkThreshold = 0.1f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 수평 속도")]
    private float _currentSpeed;

    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    private bool _isGrounded;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int IsGroundedHash =
        Animator.StringToHash("IsGrounded");

    private static readonly int JumpTriggerHash =
        Animator.StringToHash("JumpTrigger");

    private static readonly int LandTriggerHash =
        Animator.StringToHash("LandTrigger");

    private CharacterController _cc;
    private PlayerController _playerController;
    private bool _wasGrounded = true;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();

        if (animator == null)
            Debug.LogError("PlayerAnimatorController에 캐릭터 Animator를 연결해야 합니다.");
    }

    private void LateUpdate()
    {
        if (animator == null)
            return;

        UpdateSpeed();
        UpdateGrounded();
    }

    private void UpdateSpeed()
    {
        Vector3 horizontal =
            new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);

        _currentSpeed = horizontal.magnitude;
        animator.SetFloat(SpeedHash, _currentSpeed);
    }

    private void UpdateGrounded()
    {
        _isGrounded = _playerController.IsGrounded;
        animator.SetBool(IsGroundedHash, _isGrounded);

        if (_wasGrounded && !_isGrounded)
            animator.SetTrigger(JumpTriggerHash);

        if (!_wasGrounded && _isGrounded)
            animator.SetTrigger(LandTriggerHash);

        _wasGrounded = _isGrounded;
    }
}