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

    [Title("디버그")]
    [SerializeField, LabelText("씬에 범위 표시")]
    private bool showRangeInScene = true;
    // 켜면 Scene 뷰에 근접 공격 사거리를 발밑 빨간 원으로 그린다. Player가 이 원 안에 들어오면 공격을 시도한다.
    // 기즈모라 게임 화면과 빌드에는 나오지 않는다.

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

    // 피격 경직처럼 공격을 도중에 끊어야 할 때 호출한다.
    // 선딜 중이면 히트박스가 켜지기 전에 멈추고, 판정 중이면 즉시 끈다. 쿨다운은 그대로 유지된다.
    public void CancelAttack()
    {
        if (!IsAttacking) return;
        StopAllCoroutines();
        if (attackHitbox != null) attackHitbox.DisableHitbox();
        IsAttacking = false;
    }

    [Button("공격 테스트")]
    private void TestAttack() => StartAttack();

#if UNITY_EDITOR
    // 선택하지 않아도 항상 보이게 OnDrawGizmos를 쓴다.
    // NFBTEnemyAI가 높이를 뺀 XZ 거리로 사거리를 비교하므로, 바닥과 평행한 원(Y축 기준)으로 그린다.
    private void OnDrawGizmos()
    {
        if (!showRangeInScene) return;
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, attackRange);
    }
#endif
}
