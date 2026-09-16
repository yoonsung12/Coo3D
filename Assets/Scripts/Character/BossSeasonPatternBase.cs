using UnityEngine;

// 최종보스 "비둘킹"의 계절 패턴(봄/여름/가을)들이 공통으로 가지는 "패턴 발동 감시" 뼈대를 담당하는
// 추상 베이스다. Boss.OnPatternTriggered 구독/해제, 내 계절인지 판정, 이미 진행 중이면 무시,
// 다른 계절 패턴에 밀려나면 즉시 중단하는 로직을 이 클래스 하나로 모아서 관리한다.
// 각 계절의 실제 연출/전투 로직(조준-돌진, 비-인력-레이저, 나무 성장 등)은 하위 클래스가 그대로
// 담당하며, 이 리팩토링은 그 로직을 전혀 바꾸지 않는다. 겨울 패턴(BossWinterPattern)은 감시 방식이
// 달라(온기존 점화를 계속 지켜보는 구조) 이 베이스를 쓰지 않는다.
[RequireComponent(typeof(Boss))]
public abstract class BossSeasonPatternBase : MonoBehaviour
{
    // 보스 본체 컴포넌트다. Awake에서 자동으로 채워지므로 하위 클래스가 다시 가져올 필요 없다.
    protected Boss _boss;

    // 이 패턴이 반응할 계절이다. 하위 클래스가 자신의 계절을 지정한다.
    protected abstract Boss.SeasonPattern Season { get; }

    // 현재 사이클(코루틴 등)이 진행 중인지 하위 클래스가 판단해서 알려준다.
    protected abstract bool IsRunning { get; }

    protected virtual void Awake()
    {
        _boss = GetComponent<Boss>();
    }

    protected virtual void OnEnable()
    {
        _boss.OnPatternTriggered += HandlePatternTriggered;
    }

    protected virtual void OnDisable()
    {
        _boss.OnPatternTriggered -= HandlePatternTriggered;
        if (IsRunning) Abort();
    }

    // Boss가 다음 계절 패턴을 발동시킬 때마다 모든 계절 패턴 컴포넌트에 브로드캐스트된다.
    // 내 계절이 아니면 무시하되, 진행 중이던 사이클이 있으면 즉시 중단한다(다른 계절에 밀려남).
    protected void HandlePatternTriggered(Boss.SeasonPattern pattern)
    {
        if (pattern != Season)
        {
            if (IsRunning) Abort();
            return;
        }

        if (IsRunning) return;

        StartCycle();
    }

    // 진행 중이던 사이클을 파훼 성공 없이 즉시 중단하고 물리/연출 상태를 원래대로 되돌린다.
    // 오브젝트 비활성화(OnDisable)와 다른 계절 패턴에 밀려날 때(HandlePatternTriggered) 둘 다 재사용한다.
    protected abstract void Abort();

    // 이 계절의 사이클을 시작한다(보통 StartCoroutine으로 감싸 코루틴 참조를 저장).
    protected abstract void StartCycle();
}
