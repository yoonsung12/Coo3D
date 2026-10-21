using DG.Tweening;
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

    [SerializeField, LabelText("스폰 범위 절반 깊이")]
    protected float spawnHalfDepth = 0f;
    // 스폰 범위의 절반 깊이(Z, 앞뒤)다. 이 오브젝트 위치를 중심으로 ±spawnHalfDepth 범위 안에서 생성된다.
    // 사이드뷰에서도 ↑/↓로 앞뒤 이동을 하므로, 바닥 폭에 맞춰 넣으면 한 줄이 아니라 네모 범위에 떨어진다.
    // 0이면 예전처럼 스포너의 Z 한 줄에서만 생성된다.

    [SerializeField, LabelText("파괴 Y 좌표")]
    protected float destroyBelowY = -20f;
    // 이 Y 좌표 아래로 내려가면 자동으로 파괴된다.
    // 낭떠러지 아래로 떨어진 낙하물이 씬에 무한히 쌓이는 것을 방지한다.

    [Title("우산 차단 설정")]
    [SerializeField, LabelText("차단 시 축소 시간")]
    private float blockedShrinkDuration = 0.2f;
    // 우산에 막혔을 때 낙하물이 줄어들며 사라지는 데 걸리는 시간이다.

    private Tween _blockTween;

    protected virtual void Update()
    {
        if (transform.position.y < destroyBelowY)
            Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        _blockTween?.Kill();
    }

    // 이 오브젝트의 위치를 중심으로 X ±spawnHalfWidth, Z ±spawnHalfDepth 네모 범위 내 랜덤 위치를 반환한다.
    // Y는 자신의 위치를 그대로 사용해 원하는 스폰 높이가 된다.
    protected Vector3 GetSpawnPosition()
    {
        float randomX = Random.Range(
            transform.position.x - spawnHalfWidth,
            transform.position.x + spawnHalfWidth);
        float randomZ = Random.Range(
            transform.position.z - spawnHalfDepth,
            transform.position.z + spawnHalfDepth);
        return new Vector3(randomX, transform.position.y, randomZ);
    }

    // SeasonGaugeManager에 gaugeSlots 칸만큼 게이지를 추가한다.
    // 낙하물이 플레이어에게 닿았을 때 하위 클래스에서 호출한다.
    protected void AddGauge()
    {
        SeasonGaugeManager.AddGauge(seasonType, gaugeSlots);
    }

    // 우산이 펼쳐진 플레이어와 부딪히면 게이지 증가 없이 축소되며 사라진다.
    // 반환값이 true이면 호출부에서 게이지 증가 로직을 건너뛰어야 한다.
    protected bool TryBlockByUmbrella(PlayerController player)
    {
        // WindZoneVolume이 우산을 찾는 방식과 동일하게, 자식에서 못 찾으면 씬 전체에서 하나 찾는다.
        UmbrellaTool umbrella = player.GetComponentInChildren<UmbrellaTool>();
        if (umbrella == null)
            umbrella = FindFirstObjectByType<UmbrellaTool>();

        if (umbrella == null || !umbrella.IsOpen)
            return false;

        _blockTween = transform.DOScale(Vector3.zero, blockedShrinkDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 씬 뷰에서 스폰 범위를 시각화한다. 깊이가 있으면 네모, 없으면 선으로 그린다.
        Gizmos.color = Color.cyan;
        if (spawnHalfDepth > 0f)
        {
            Gizmos.DrawWireCube(transform.position, new Vector3(spawnHalfWidth * 2f, 0f, spawnHalfDepth * 2f));
            return;
        }

        Vector3 left  = transform.position + Vector3.left  * spawnHalfWidth;
        Vector3 right = transform.position + Vector3.right * spawnHalfWidth;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left,  0.15f);
        Gizmos.DrawWireSphere(right, 0.15f);
    }
#endif
}
