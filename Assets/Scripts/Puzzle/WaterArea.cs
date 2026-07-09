using Sirenix.OdinInspector;
using UnityEngine;

// 특정 구역에 비가 내려 빗방울이 "현재 물 표면"에 닿으면 수위가 서서히 차오르는 물 웅덩이다.
// 일정 시간 동안 빗방울이 닿지 않으면 서서히 원래 수위로 빠진다.
// 콜라이더는 트리거이며, 윗면 위치를 현재 수위에 맞춰 매 프레임 갱신한다 —
// 그래야 빗방울이 바닥이 아니라 실제 물 표면에 닿았을 때만 반응한다.
[RequireComponent(typeof(BoxCollider))]
public class WaterArea : MonoBehaviour
{
    [Title("수위 설정")]
    [SerializeField, LabelText("빗방울 1개당 상승량")]
    private float risePerDrop = 0.05f;
    // 값이 클수록 빗방울 하나에 물이 더 많이 차오른다.

    [SerializeField, LabelText("최대 상승 높이")]
    private float maxRiseHeight = 4f;
    // 기준 수위(콜라이더 원래 윗면)로부터 최대로 오를 수 있는 높이다.

    [Title("배수 설정")]
    [SerializeField, LabelText("배수 시작까지 대기 시간(초)")]
    private float drainDelay = 3f;
    // 마지막으로 물이 찬 뒤 이 시간이 지나면 서서히 빠지기 시작한다.

    [SerializeField, LabelText("배수 속도 (유닛/초)")]
    private float drainSpeed = 0.5f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 오른 높이")]
    private float _currentOffset;

    private BoxCollider _collider;
    private float _baseColliderHeight;
    private float _baseColliderCenterY;
    private float _baseSurfaceY;
    private float _lastFilledTime = -999f;

    // FloatingBox가 참조하는 현재 물 표면의 월드 Y 좌표다.
    public float CurrentWaterY => _baseSurfaceY + _currentOffset;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _collider.isTrigger = true;
        // 물은 물리 충돌이 필요 없는 감지 전용 트리거로 둔다 (RainController의 구름 콜라이더와 동일한 방식).

        _baseColliderHeight = _collider.size.y;
        _baseColliderCenterY = _collider.center.y;
        // 콜라이더 윗면의 원래 월드 Y 좌표를 기준 수위로 저장한다. 수위는 여기서부터 오른다.
        _baseSurfaceY = transform.position.y + _baseColliderCenterY + (_baseColliderHeight * 0.5f);
    }

    private void Update()
    {
        UpdateDrain();
        UpdateColliderHeight();
    }

    private void UpdateDrain()
    {
        bool canDrain = Time.time - _lastFilledTime > drainDelay;
        if (canDrain)
            _currentOffset = Mathf.MoveTowards(_currentOffset, 0f, drainSpeed * Time.deltaTime);
    }

    // 콜라이더 바닥면은 고정한 채 윗면만 _currentOffset만큼 올라가도록 크기와 중심을 함께 조정한다.
    private void UpdateColliderHeight()
    {
        float height = _baseColliderHeight + _currentOffset;
        Vector3 size = _collider.size;
        size.y = height;
        _collider.size = size;

        Vector3 center = _collider.center;
        center.y = _baseColliderCenterY + (_currentOffset * 0.5f);
        _collider.center = center;
    }

    // RainDrop이 이 물 표면에 닿았을 때 호출한다.
    public void OnRainDropHit()
    {
        _currentOffset = Mathf.Min(_currentOffset + risePerDrop, maxRiseHeight);
        _lastFilledTime = Time.time;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        // 최대로 차오를 수 있는 전체 범위를 옅은 파란색 와이어박스로 표시한다.
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Vector3 maxCenter = transform.position + Vector3.up * (maxRiseHeight * 0.5f);
        Vector3 maxSize = new Vector3(col.size.x, maxRiseHeight, col.size.z);
        Gizmos.DrawWireCube(maxCenter, maxSize);

        // 현재 수위선은 진한 파란색 평면으로 표시한다.
        Gizmos.color = new Color(0f, 0.3f, 1f);
        float currentY = Application.isPlaying
            ? CurrentWaterY
            : transform.position.y + col.center.y + (col.size.y * 0.5f);
        Vector3 lineCenter = new Vector3(transform.position.x, currentY, transform.position.z);
        Vector3 lineSize = new Vector3(col.size.x, 0.05f, col.size.z);
        Gizmos.DrawCube(lineCenter, lineSize);
    }
#endif
}
