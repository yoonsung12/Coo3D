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

    [SerializeField, LabelText("블라스트 자세 해제 속도")]
    private float blastEndSpeed = 0.5f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 수평 속도")]
    private float _currentSpeed;

    [ReadOnly, ShowInInspector, LabelText("접지 여부")]
    private bool _isGrounded;

    [ReadOnly, ShowInInspector, LabelText("선풍기 충전 중")]
    private bool _isFanCharging;

    [ReadOnly, ShowInInspector, LabelText("블라스트 반동 중")]
    private bool _isFanBlasting;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int LandTriggerHash = Animator.StringToHash("LandTrigger");
    private static readonly int HitTriggerHash = Animator.StringToHash("Hit");
    private static readonly int DeathTriggerHash = Animator.StringToHash("Death");
    private static readonly int FanChargeHash = Animator.StringToHash("FanCharge");
    private static readonly int FanBlastHash = Animator.StringToHash("FanBlast");
    private static readonly int UmbrellaHash =
    Animator.StringToHash("Umbrella");

    private static readonly int UmbrellaGlideHash =
        Animator.StringToHash("UmbrellaGlide");

    private static readonly int UmbrellaBoostHash =
        Animator.StringToHash("UmbrellaBoost");

    private CharacterController _cc;
    private PlayerController _playerController;
    private bool _wasGrounded = true;
    private bool _isDead;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();

        if (animator == null)
            Debug.LogError("PlayerAnimatorController에 캐릭터 Animator를 연결해야 합니다.");
    }

    private void LateUpdate()
    {
        if (animator == null || _isDead)
            return;

        UpdateSpeed();
        UpdateGrounded();
        UpdateFanBlast();
    }

    private void UpdateSpeed()
    {
        Vector3 horizontal = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        _currentSpeed = _playerController.enabled ? horizontal.magnitude : 0f;
        // 컷신·포털 연출 중에는 PlayerController가 꺼져 Move()가 호출되지 않아 _cc.velocity가 마지막 값으로 남는다.
        // 그대로 쓰면 멈춰 있는데도 달리기 애니메이션이 계속 나오므로, 꺼져 있을 때는 정지로 본다.
        animator.SetFloat(SpeedHash, _currentSpeed);
    }

    private void UpdateGrounded()
    {
        _isGrounded = _playerController.IsGrounded;
        animator.SetBool(IsGroundedHash, _isGrounded);

        // 뒤로 날아가는 중에는 이동 애니메이션의 Jump/Land 트리거가 자세를 덮지 않도록 함.
        if (!_isFanBlasting)
        {
            if (_wasGrounded && !_isGrounded)
                animator.SetTrigger(JumpTriggerHash);

            if (!_wasGrounded && _isGrounded)
                animator.SetTrigger(LandTriggerHash);
        }

        _wasGrounded = _isGrounded;
    }

    private void UpdateFanBlast()
    {
        bool shouldBlast = _playerController != null &&
                           _playerController.BlastRecoilSpeed > blastEndSpeed;

        if (_isFanBlasting == shouldBlast)
            return;

        _isFanBlasting = shouldBlast;

        if (_isFanBlasting)
        {
            _isFanCharging = false;
            animator.SetBool(FanChargeHash, false);
            animator.ResetTrigger(JumpTriggerHash);
            animator.ResetTrigger(LandTriggerHash);
        }

        animator.SetBool(FanBlastHash, _isFanBlasting);
    }

    public void PlayHit()
    {
        if (animator == null || _isDead)
            return;
        animator.SetTrigger(HitTriggerHash);
    }

    public void PlayDeath()
    {
        if (animator == null || _isDead)
            return;

        _isDead = true;
        _isFanCharging = false;
        _isFanBlasting = false;
        animator.SetBool(FanChargeHash, false);
        animator.SetBool(FanBlastHash, false);
        animator.ResetTrigger(HitTriggerHash);
        animator.ResetTrigger(JumpTriggerHash);
        animator.ResetTrigger(LandTriggerHash);
        animator.SetTrigger(DeathTriggerHash);
        animator.SetBool(UmbrellaHash, false);
        animator.SetBool(UmbrellaGlideHash, false);
    }

    public void ResetAnimator()
    {
        if (animator == null)
            return;

        _isDead = false;
        _isFanCharging = false;
        _isFanBlasting = false;
        animator.SetBool(FanChargeHash, false);
        animator.SetBool(FanBlastHash, false);
        animator.ResetTrigger(HitTriggerHash);
        animator.ResetTrigger(DeathTriggerHash);
        animator.ResetTrigger(JumpTriggerHash);
        animator.ResetTrigger(LandTriggerHash);
        animator.SetBool(UmbrellaHash, false);
        animator.SetBool(UmbrellaGlideHash, false);

        _wasGrounded = _playerController.IsGrounded;
        animator.Play("Player_Idle", 0, 0f);
    }

    // 충전 1/2/3단계 모두 동일한 자세 사용.
    public void SetFanCharge(bool active)
    {
        if (animator == null || _isDead)
            return;

        _isFanCharging = active;
        animator.SetBool(FanChargeHash, active);
    }

    // 실제 반동 유지 시간은 PlayerController.BlastRecoilSpeed로 매 프레임 판정.
    public void PlayFanBlast()
    {
        if (animator == null || _isDead)
            return;

        _isFanCharging = false;
        _isFanBlasting = true;
        animator.SetBool(FanChargeHash, false);
        animator.ResetTrigger(JumpTriggerHash);
        animator.ResetTrigger(LandTriggerHash);
        animator.SetBool(FanBlastHash, true);
    }
    // 우산 기본 / 글라이드 상태
    public void SetUmbrella(bool isOpen, bool isGliding)
    {
        if (animator == null || _isDead)
            return;

        animator.SetBool(UmbrellaHash, isOpen && !isGliding);
        animator.SetBool(UmbrellaGlideHash, isOpen && isGliding);
    }

    // 우산 부스트 애니메이션 (추후 기능 연결)
    public void PlayUmbrellaBoost()
    {
        if (animator == null || _isDead)
            return;

        animator.SetBool(UmbrellaHash, false);
        animator.SetBool(UmbrellaGlideHash, false);
        animator.SetTrigger(UmbrellaBoostHash);
    }
}
