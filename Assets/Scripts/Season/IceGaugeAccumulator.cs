using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 얼음 위에 서 있는 시간을 누적해, 일정 시간이 쌓일 때마다 겨울 게이지를 1칸씩 올린다.
// 밟자마자 오르지 않고, 얼음에서 내려와도 누적 시간은 초기화되지 않는다. (조금씩 여러 번 밟아도 결국 찬다)
// 얼음 판정은 PlayerController가 이미 계산한 발밑 얼음(IcePlatform)과 FootstepSurface의 "얼음" 바닥을 함께 본다.
// 플레이어 오브젝트(PlayerController가 붙은 곳)에 붙인다.
[RequireComponent(typeof(PlayerController))]
public class IceGaugeAccumulator : MonoBehaviour
{
    [Title("겨울 게이지 적립 설정")]
    [SerializeField, LabelText("1칸 오르는 데 필요한 시간(초)")]
    private float secondsPerSlot = 2f;
    // 얼음 위에 이 시간만큼 서 있으면 겨울 게이지가 1칸 오른다. 작을수록 빨리 찬다.

    [SerializeField, LabelText("한 번에 오르는 칸 수")]
    private int gaugeSlots = 1;

    [SerializeField, LabelText("바닥 검사 거리")]
    private float groundProbeDistance = 0.6f;
    // FootstepSurface(얼음) 바닥을 찾을 때 발 위치에서 아래로 검사하는 거리다. PlayerFootstep과 같은 값을 쓴다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("얼음 위에 있음")]
    private bool _isOnIce;

    [ReadOnly, ShowInInspector, LabelText("누적 시간")]
    private float _iceTimer;

    private PlayerController _player;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        _isOnIce = CanAccumulate() && IsStandingOnIce();
        if (!_isOnIce) return;
        // 얼음에서 내려와도 _iceTimer를 0으로 돌리지 않는다. 멈춘 지점부터 이어서 센다.

        // Time.deltaTime을 더해 프레임 수와 상관없이 실제로 서 있던 시간 기준으로 오르게 한다.
        _iceTimer += Time.deltaTime;
        if (_iceTimer >= secondsPerSlot)
        {
            _iceTimer -= secondsPerSlot;
            SeasonGaugeManager.AddGauge(SeasonType.Winter, gaugeSlots);
        }
    }

    // 시간을 세도 되는 상태인지 확인한다.
    private bool CanAccumulate()
    {
        // 컷신·포털 연출 중(PlayerController 꺼짐)이나 튜브 탑승 중에는 세지 않는다.
        if (!_player.enabled || _player.IsMounted) return false;

        // 디버프가 걸려 있는 동안은 세지 않는다.
        // 그러지 않으면 얼음 위에서 빙결로 못 움직이는 동안 게이지가 다시 차서 빙결이 끝없이 이어질 수 있다.
        if (SeasonGaugeManager.CurrentDebuff != DebuffType.None) return false;

        // 공중(얼음에서 점프한 상태 포함)에서는 세지 않는다.
        return _player.IsGrounded;
    }

    // 발밑이 얼음인지 확인한다.
    private bool IsStandingOnIce()
    {
        // 1) 미끄러운 IcePlatform 위: PlayerController가 착지 판정 중 이미 구해 둔 값을 그대로 쓴다.
        //    횃불로 녹아 미끄럽지 않은 얼음은 false라서, "녹이면 게이지도 안 찬다"는 연계가 된다.
        if (_player.IsSlipping) return true;

        // 2) 콜라이더 없는 장식 얼음(FootstepSurface 영역 판정)
        if (FootstepSurface.TryGetAreaSurface(transform.position, out FootstepSurfaceType areaType))
            return areaType == FootstepSurfaceType.Ice;

        // 3) FootstepSurface로 "얼음"을 직접 지정한 바닥 콜라이더
        //    발(피벗)보다 조금 위에서 쏴서 경사로에 발이 살짝 묻혀 있어도 바닥을 놓치지 않게 한다.
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.3f + groundProbeDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        FootstepSurface marker = hit.collider.GetComponentInParent<FootstepSurface>();
        return marker != null && marker.SurfaceType == FootstepSurfaceType.Ice;
    }

    [Title("테스트")]
    [Button("누적 시간 초기화")]
    private void TestResetTimer() => _iceTimer = 0f;
}
