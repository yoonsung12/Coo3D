using Sirenix.OdinInspector;
using UnityEngine;

// 모든 계절 낙하물(꽃가루, 비, 은행, 눈)의 공통 기반 클래스다.
// 계절 타입, 게이지 칸 수, 스폰 범위를 공유한다.
// Spawner도 이 클래스를 상속해 스폰 범위 로직만 재사용한다.
public abstract class BaseHazard : MonoBehaviour
{
    [Title("시즌 게이지 설정")]
    [SerializeField, LabelText("계절 타입")]
    protected SeasonType seasonType;
    // Inspector에서 이 낙하물이 어느 계절에 속하는지 지정한다.

    [SerializeField, LabelText("게이지 칸 수")]
    protected int gaugeSlots = 1;
    // 낙하물이 플레이어에게 닿았을 때 증가시킬 게이지 칸 수다.

    [Title("스폰 범위 설정")]
    [SerializeField, LabelText("스폰 범위 절반 너비")]
    protected float spawnHalfWidth = 5f;
    // 스폰 범위의 절반 너비다. 이 오브젝트 위치를 중심으로 ±spawnHalfWidth 범위 안에서 생성된다.

    [SerializeField, LabelText("파괴 Y 좌표")]
    protected float destroyBelowY = -20f;
    // 이 Y 좌표 아래로 내려가면 자동으로 파괴된다.
    // 낭떠러지 아래로 떨어진 낙하물이 씬에 무한히 쌓이는 것을 방지한다.

    protected virtual void Update()
    {
        if (transform.position.y < destroyBelowY)
            Destroy(gameObject);
    }

    // 이 오브젝트의 위치를 중심으로 ±spawnHalfWidth 범위 내 랜덤 위치를 반환한다.
    // Y와 Z는 자신의 위치를 그대로 사용한다 — 사이드뷰라 Z는 고정 평면, Y는 원하는 스폰 높이가 된다.
    protected Vector3 GetSpawnPosition()
    {
        float randomX = Random.Range(
            transform.position.x - spawnHalfWidth,
            transform.position.x + spawnHalfWidth);
        return new Vector3(randomX, transform.position.y, transform.position.z);
    }

    // SeasonGaugeManager에 gaugeSlots 칸만큼 게이지를 추가한다.
    // 낙하물이 플레이어에게 닿았을 때 하위 클래스에서 호출한다.
    protected void AddGauge()
    {
        SeasonGaugeManager.AddGauge(seasonType, gaugeSlots);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 씬 뷰에서 스폰 범위를 선으로 시각화한다.
        Gizmos.color = Color.cyan;
        Vector3 left  = transform.position + Vector3.left  * spawnHalfWidth;
        Vector3 right = transform.position + Vector3.right * spawnHalfWidth;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left,  0.15f);
        Gizmos.DrawWireSphere(right, 0.15f);
    }
#endif
}
