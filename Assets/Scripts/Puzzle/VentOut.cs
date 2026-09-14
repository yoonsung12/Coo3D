using Sirenix.OdinInspector;
using UnityEngine;

// 환풍기 출구. VentIn에서 활성화 신호를 받으면 일정 방향으로 바람을 발생시킨다.
// Trigger Collider 안에 머무는 IBlowable/Rigidbody 오브젝트에 바람 힘을 가한다.
// 바람 방향은 Inspector의 WindDirectionType으로 설정한다(오브젝트 회전 불필요).
public enum WindDirectionType
{
    Right,  // 오른쪽 (+X)
    Left,   // 왼쪽 (-X)
    Up,     // 위 (+Y)
    Down,   // 아래 (-Y)
    Custom  // 직접 입력
}

[RequireComponent(typeof(Collider))]
public class VentOut : MonoBehaviour
{
    [Title("바람 방향 설정")]
    [SerializeField, LabelText("바람 방향")]
    private WindDirectionType directionType = WindDirectionType.Right;
    // 바람이 나오는 방향을 선택한다. Custom을 고르면 아래 벡터를 직접 입력할 수 있다.
    // 사이드뷰(X: 좌우, Y: 상하)라 Z축 방향은 제공하지 않는다.

    [SerializeField, LabelText("커스텀 방향 벡터")]
    [ShowIf("directionType", WindDirectionType.Custom)]
    private Vector3 customDirection = Vector3.right;
    // directionType이 Custom일 때만 표시된다. 정규화하지 않아도 자동으로 처리된다.

    [Title("바람 설정")]
    [SerializeField, LabelText("바람 세기")]
    private float windForce = 8f;
    // 값이 클수록 범위 안 오브젝트가 더 강하게 밀린다.

    [Title("감지 설정")]
    [InfoBox("바람 영향을 받을 오브젝트의 레이어를 선택하세요.\nVentIn 레이어는 제외해야 피드백 루프를 방지할 수 있습니다.")]
    [SerializeField, LabelText("바람 영향 LayerMask")]
    private LayerMask blowTargetMask;
    // Inspector에서 바람에 반응할 레이어를 선택한다 (예: WoodenBox, 촛불, 꽃가루).

    [Title("Latch 모드")]
    [SerializeField, LabelText("Latch 모드 사용")]
    private bool latchMode = false;
    // 체크하면 한 번 활성화된 후 VentIn 바람이 끊겨도 계속 켜진 상태를 유지한다.

    [Title("바람 이펙트 설정")]
    [SerializeField, LabelText("바람 파티클")]
    private ParticleSystem windEffect;
    // Inspector에서 바람 방향을 향하는 파티클을 연결한다. 비워두면 이펙트 없이 힘만 작동한다.
    // WindZoneVolume과 동일하게 Play()/Stop()만 호출하는 방식이라, 파티클 모양/색은 에디터에서 직접 제작/튜닝한다.

    [Title("런타임 상태")]
    [ShowInInspector, ReadOnly, LabelText("현재 활성 여부")]
    private bool isActive = false;

    [ShowInInspector, ReadOnly, LabelText("Latch 잠금 여부")]
    private bool isLatched = false;

    private void Awake()
    {
        if (windEffect != null)
        {
            // 파티클이 바람 방향을 바라보도록 회전시켜 흩날리는 모양이 방향과 일치하게 한다.
            Vector3 dir = GetWindDirection();
            windEffect.transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    // VentIn에서 호출된다. 바람을 활성화하고 이펙트를 재생한다.
    public void Activate()
    {
        if (isActive) return;

        isActive = true;
        // Unity 오브젝트에는 ?. 대신 명시적 null 체크를 쓴다(비어있는 참조에서 ?.가 실제로
        // 메서드 호출을 건너뛰지 않고 UnassignedReferenceException을 던지는 경우가 있었다).
        if (windEffect != null)
            windEffect.Play();

        if (latchMode)
            isLatched = true;
    }

    // VentIn에서 바람이 끊겼을 때 호출된다. Latch 모드로 잠금이 걸려 있으면 비활성화하지 않는다.
    public void Deactivate()
    {
        if (isLatched) return;
        if (!isActive) return;

        isActive = false;
        if (windEffect != null)
            windEffect.Stop();
    }

    // Trigger 안에 오브젝트가 머무는 동안 FixedUpdate마다 호출된다.
    // 활성 상태일 때 IBlowable 오브젝트에 바람 힘을 가한다.
    private void OnTriggerStay(Collider other)
    {
        if (!isActive) return;

        // VentIn은 IBlowable을 구현하므로 아래 감지에 걸린다.
        // VentOut이 VentIn.OnBlown()을 호출하면 VentIn의 비활성화 타이머가 계속 초기화되어
        // 플레이어가 선풍기를 멈춰도 VentOut이 영원히 꺼지지 않는 피드백 루프가 발생한다.
        // blowTargetMask 설정과 무관하게 VentIn은 반드시 건너뛴다.
        if (other.TryGetComponent<VentIn>(out _)) return;

        if ((blowTargetMask.value & (1 << other.gameObject.layer)) == 0) return;

        Vector3 windDir = GetWindDirection();

        if (other.TryGetComponent<IBlowable>(out var blowable))
        {
            // IBlowable이 있으면 OnBlown()에 힘 처리를 맡긴다.
            blowable.OnBlown(windDir, windForce, false);
        }
        else if (other.attachedRigidbody != null)
        {
            // IBlowable이 없지만 Rigidbody가 있는 오브젝트는 직접 밀어낸다.
            other.attachedRigidbody.AddForce(windDir * windForce, ForceMode.Force);
        }
    }

    // directionType 설정에 따라 실제 바람 방향 벡터를 반환한다.
    // Custom일 때는 customDirection을 정규화해서 반환한다.
    private Vector3 GetWindDirection()
    {
        return directionType switch
        {
            WindDirectionType.Right  => Vector3.right,
            WindDirectionType.Left   => Vector3.left,
            WindDirectionType.Up     => Vector3.up,
            WindDirectionType.Down   => Vector3.down,
            WindDirectionType.Custom => customDirection.normalized,
            _                        => Vector3.right,
        };
    }

    [Button("테스트: 활성화 (Play Mode)")]
    private void TestActivate()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[VentOut] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        isActive = false;
        Activate();
    }

    [Button("테스트: 비활성화 (Play Mode)")]
    private void TestDeactivate()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[VentOut] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        isLatched = false;
        isActive  = true;
        Deactivate();
    }

    [Button("테스트: Latch 초기화 (Play Mode)")]
    private void TestResetLatch()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[VentOut] Play Mode에서만 테스트 가능합니다.");
            return;
        }
        isLatched = false;
        isActive  = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? Color.cyan : Color.gray;

        // 바람 방향 화살표
        Vector3 windDir = GetWindDirection();
        Gizmos.DrawRay(transform.position, windDir * 3f);
        Gizmos.DrawWireSphere(transform.position + windDir * 3f, 0.15f);

        // Trigger Collider 범위 표시
        if (TryGetComponent<BoxCollider>(out var box))
            Gizmos.DrawWireCube(transform.position + box.center, box.size);
    }
}
