using Sirenix.OdinInspector;
using UnityEngine;

// 특정 구역에 비가 내려 빗방울이 "현재 물 표면"에 닿으면 수위가 서서히 차오르는 물 웅덩이다.
// 일정 시간 동안 빗방울이 닿지 않으면 서서히 원래 수위로 빠진다.
// 콜라이더는 트리거이며, 윗면 위치를 현재 수위에 맞춰 매 프레임 갱신한다 —
// 그래야 빗방울이 바닥이 아니라 실제 물 표면에 닿았을 때만 반응한다.
[RequireComponent(typeof(BoxCollider))]
public class WaterArea : MonoBehaviour
{
    [Title("물 표면 시각화")]
    [SerializeField, LabelText("물 표면 오브젝트")]
    private Transform waterSurfaceVisual;
    // 실제로 눈에 보이는 물 표면 역할을 하는 Quad/Cube를 연결한다.
    // 콜라이더(트리거)는 판정용이라 렌더러가 없으므로, 이 오브젝트가 없으면 Play Mode에서 물이 안 보이고 상자만 떠오르는 것처럼 보인다.

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
    private Vector3 _visualBaseScale;
    // waterSurfaceVisual의 가로/깊이(X, Z) 원본 크기를 기억해둔다.
    // 세로(Y) 크기만 매 프레임 수위에 맞춰 바꿀 때, 가로/깊이는 처음 설정값을 그대로 유지하기 위해 필요하다.


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

        if (waterSurfaceVisual != null)
            _visualBaseScale = waterSurfaceVisual.localScale;
        // 물 표면 오브젝트의 처음 크기를 기억해둔다 — 이후 UpdateWaterVisual()에서 세로 크기만 바꿀 때 가로/깊이를 이 값으로 유지한다.
    }

    private void Update()
    {
        UpdateDrain();
        UpdateColliderHeight();
        UpdateWaterVisual();
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


    // 물 표면 오브젝트를 바닥은 고정한 채 위쪽만 자라나는 물덩이 형태로 만든다.
    // 옆에서 보는 사이드뷰 카메라 기준으로도 "물이 차오른다"는 느낌이 나도록,
    // 얇은 판이 위치만 움직이는 대신 실제로 부피가 있는 블록처럼 세로 크기 자체를 키운다.
    private void UpdateWaterVisual()
    {
        if (waterSurfaceVisual == null) return;

        // 콜라이더와 동일하게 바닥은 고정(_baseSurfaceY - _baseColliderHeight)이고, 높이만 현재 수위만큼 자란다.
        float poolBottomY = _baseSurfaceY - _baseColliderHeight;
        float currentHeight = Mathf.Max(_baseColliderHeight + _currentOffset, 0.01f);
        // 높이가 0이 되면 오브젝트가 납작하게 찌그러지는 것을 막기 위해 최소값을 둔다.

        waterSurfaceVisual.localScale = new Vector3(_visualBaseScale.x, currentHeight, _visualBaseScale.z);

        Vector3 pos = waterSurfaceVisual.position;
        pos.y = poolBottomY + (currentHeight * 0.5f);
        waterSurfaceVisual.position = pos;
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
