using Sirenix.OdinInspector;
using UnityEngine;

// 겨울 계절 낙하물인 눈송이다. 대각선으로 떨어지며 Player에 닿으면 겨울 게이지를 올린다.
// 이 프로젝트에는 아직 수위(WaterArea) 시스템이 없어서, 바닥 충돌 대신 BaseHazard의
// "Y 좌표 아래로 내려가면 자동 파괴" 로직으로 정리된다.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SnowFlake : BaseHazard
{
    [Title("낙하 설정")]
    [SerializeField, LabelText("낙하 속도")]
    private float fallSpeed = 4f;
    // 눈은 빗방울보다 가볍게 천천히 떨어지도록 기본값을 더 낮게 잡는다.

    [SerializeField, LabelText("낙하 각도 (도)"), Range(-45f, 45f)]
    private float fallAngle = 20f;
    // 수직(0°)에서 기울어진 각도다. 양수면 오른쪽 대각선, 음수면 왼쪽 대각선으로 떨어진다.
    // 눈보라 연출에서는 SnowController가 생성 시 이 값을 덮어써 바람 방향을 반영한다.

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        // 자체 fallSpeed로만 떨어지고, 물리 엔진의 중력은 받지 않는다.

        ApplyFallVelocity();

        GetComponent<Collider>().isTrigger = true;
    }

    // 현재 fallAngle/fallSpeed 값을 기준으로 낙하 방향과 기울기를 다시 계산해 적용한다.
    private void ApplyFallVelocity()
    {
        float rad = fallAngle * Mathf.Deg2Rad;
        // 사이드뷰(X-Y 평면)라 X(좌우)/Y(상하) 성분만 사용하고 Z는 고정한다.
        Vector3 fallDirection = new Vector3(Mathf.Sin(rad), -Mathf.Cos(rad), 0f);
        _rb.linearVelocity = fallDirection * fallSpeed;

        // 낙하 방향에 맞춰 오브젝트를 기울인다.
        transform.rotation = Quaternion.Euler(0f, 0f, -fallAngle);
    }

    // SnowController가 눈보라의 바람 세기에 맞춰 낙하 각도를 지정할 때 사용한다.
    public void SetFallAngle(float angle)
    {
        fallAngle = Mathf.Clamp(angle, -45f, 45f);
        if (_rb != null)
            ApplyFallVelocity();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (TryBlockByUmbrella(player)) return; // 우산에 막힘: 게이지 증가 없이 사라짐

        AddGauge(); // BaseHazard의 게이지 추가 메서드를 호출한다.
        Destroy(gameObject);
    }
}
