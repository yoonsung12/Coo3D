
using UnityEngine;

public class EnemyAnimationController : MonoBehaviour
{
    [Header("애니메이터 연결")]
    [SerializeField] private Animator animator;

    private static readonly int IsMoving =
        Animator.StringToHash("IsMoving");

    private static readonly int Attack =
        Animator.StringToHash("Attack");

    private static readonly int Rush =
        Animator.StringToHash("Rush");

    private static readonly int Shot =
        Animator.StringToHash("Shot");

    private static readonly int Hit =
        Animator.StringToHash("Hit");

    private Enemy enemy;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // 걷기 / 대기
    public void SetMoving(bool moving)
    {
        if (animator == null || enemy.IsDead) return;

        animator.SetBool(IsMoving, moving);
    }

    // 근거리 공격
    public void PlayAttack()
    {
        PlayTrigger(Attack);
    }

    // 돌진
    public void PlayRush()
    {
        PlayTrigger(Rush);
    }

    // 원거리 발사
    public void PlayShot()
    {
        PlayTrigger(Shot);
    }

    // 피격
    public void PlayHit()
    {
        PlayTrigger(Hit);
    }

    private void PlayTrigger(int trigger)
    {
        if (animator == null || enemy.IsDead) return;

        // 다른 공격 Trigger와 충돌하지 않도록 초기화
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(Rush);
        animator.ResetTrigger(Shot);

        animator.SetTrigger(trigger);
    }

    // 사망 시 애니메이션 정지
    public void StopAnimation()
    {
        if (animator == null) return;

        animator.enabled = false;
    }
}
