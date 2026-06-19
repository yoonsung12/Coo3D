using UnityEngine;
using Sirenix.OdinInspector;

// 선풍기 바람에 밀리는 나무 상자다.
// Rigidbody(3D)로 물리 반응을 처리하며 IBlowable 인터페이스로 바람에 반응한다.
[RequireComponent(typeof(Rigidbody))]
public class WoodenBox : MonoBehaviour, IBlowable
{
    [Title("물리 설정")]
    [SerializeField, LabelText("최대 속도")]
    private float maxSpeed = 8f;
    // 바람에 의해 날려갈 때의 속도 상한이다. 너무 빠르게 날아가지 않도록 제한한다.

    [SerializeField, LabelText("복원력")]
    private float restoreForce = 3f;
    // 원래 위치로 돌아오려는 스프링 힘이다. 값이 클수록 빠르게 원위치로 돌아온다.
    // 0으로 설정하면 복원력 없이 밀린 상태를 유지한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 속도")]
    private float _currentSpeed;

    [ReadOnly, ShowInInspector, LabelText("원점으로부터 거리")]
    private float _distanceFromOrigin;

    private Rigidbody _rb;
    private Vector3 _originPosition;

    private void Start()
    {
        // Start()에서 초기화해야 [RequireComponent]로 추가된 Rigidbody가 확실히 준비된 뒤 참조할 수 있다.
        _rb = GetComponent<Rigidbody>();
        // 시작 위치를 원점으로 기록해 복원력 계산에 사용한다.
        _originPosition = transform.position;

        // Y축 회전만 허용해 상자가 쓰러지지 않게 한다.
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        Debug.Log($"[WoodenBox] Start() 호출됨. _rb={_rb}, _originPosition={_originPosition}");
    }

    private void FixedUpdate()
    {
        if (restoreForce > 0f)
            ApplyRestoreForce();

        ClampSpeed();

        _currentSpeed = _rb.linearVelocity.magnitude;
        _distanceFromOrigin = Vector3.Distance(transform.position, _originPosition);
    }

    // FanTool의 BoxCast에 감지될 때 호출된다.
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        // Y축 힘은 제거해 상자가 공중으로 뜨지 않고 지면을 따라 밀리게 한다.
        Vector3 blowForce = new Vector3(direction.x, 0f, direction.z).normalized * force;
        ForceMode mode = impulse ? ForceMode.Impulse : ForceMode.Force;
        _rb.AddForce(blowForce, mode);
    }

    private void ApplyRestoreForce()
    {
        // 원점까지의 수평 거리에 비례한 스프링 힘을 적용해 상자가 너무 멀리 가지 않게 한다.
        Vector3 toOrigin = _originPosition - transform.position;
        toOrigin.y = 0f;
        _rb.AddForce(toOrigin * restoreForce, ForceMode.Force);
    }

    private void ClampSpeed()
    {
        // 속도가 최대값을 초과하면 방향은 유지하고 크기만 제한한다.
        Vector3 vel = _rb.linearVelocity;
        if (vel.magnitude > maxSpeed)
            _rb.linearVelocity = vel.normalized * maxSpeed;
    }

    [Button("바람 테스트 (앞 방향)")]
    private void TestBlow()
    {
        OnBlown(transform.forward, 20f, true);
    }
}
