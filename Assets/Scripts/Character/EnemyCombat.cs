using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 적의 공격 타이밍을 담당한다. 선딜 이후 히트박스를 잠깐 켰다 끄고, 쿨다운이 지나야 다시 공격할 수 있다.
public class EnemyCombat : MonoBehaviour
{
    [Title("공격 타이밍 설정")]
    [SerializeField, LabelText("공격 사거리")]
    private float attackRange = 1.5f;
    // NFBTEnemyAI가 이 사거리 안에 Player가 있을 때만 공격을 시도한다.

    [SerializeField, LabelText("공격 쿨다운")]
    private float attackCooldown = 1f;

    [SerializeField, LabelText("선딜레이 (히트박스 켜지기 전)")]
    private float attackDelay = 0.2f;

    [SerializeField, LabelText("히트박스 유지 시간")]
    private float attackDuration = 0.2f;

    [Title("연결")]
    [SerializeField, LabelText("공격 히트박스")]
    private EnemyAttackHitbox attackHitbox;

    public float AttackRange => attackRange;

    [ReadOnly, ShowInInspector, LabelText("공격 중")]
    public bool IsAttacking { get; private set; }

    private float _lastAttackTime = -999f;

    public bool CanAttack => !IsAttacking && Time.time - _lastAttackTime >= attackCooldown;

    public void StartAttack()
    {
        if (!CanAttack) return;

        _lastAttackTime = Time.time;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;

        yield return new WaitForSeconds(attackDelay);

        attackHitbox.EnableHitbox();
        yield return new WaitForSeconds(attackDuration);
        attackHitbox.DisableHitbox();

        IsAttacking = false;
    }

    [Button("공격 테스트")]
    private void TestAttack() => StartAttack();
}
