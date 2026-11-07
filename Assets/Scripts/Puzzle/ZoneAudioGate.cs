using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 구역 안의 장치 소리가 구역 밖까지 새어 나가지 않게 하는 컴포넌트다.
// 플레이어가 구역 안에 들어오면 잠시 뒤 연결된 AudioSource의 음소거를 풀고, 밖으로 나가면 바로 음소거한다.
// 소리만 끄기 때문에 장치(물벼락, 전선 등)의 작동 주기는 그대로 유지된다.
[RequireComponent(typeof(Collider))]
public class ZoneAudioGate : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("플레이어")]
    private Transform player;
    // 구역 안에 있는지 확인할 플레이어를 연결한다.

    [SerializeField, LabelText("제어할 소리")]
    private AudioSource[] audioSources;
    // 플레이어가 구역 밖에 있을 때 들리면 안 되는 AudioSource를 연결한다.
    // 꺼져 있는 오브젝트의 AudioSource도 넣어 두면, 나중에 다시 켜도 같은 규칙이 적용된다.

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("음소거 해제 지연(초)")]
    private float unmuteDelay = 0.6f;
    // 구역에 들어온 뒤 소리가 들리기 시작할 때까지의 시간이다.
    // SideViewCamera의 전환 시간과 같게 두면 시점 전환이 끝나는 순간부터 소리가 들린다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 구역 안에 있음")]
    private bool _isPlayerInside;

    private Collider _zone;
    private Tween _unmuteTween;
    // 지연 중에 구역을 나가면 음소거 해제를 취소해야 하므로 Tween을 저장해 둔다.

    private void Awake()
    {
        _zone = GetComponent<Collider>();

        // 장치가 첫 소리를 내기 전에 상태를 맞춘다. 구역 안에서 시작했다면 지연 없이 바로 들리게 한다.
        _isPlayerInside = IsPlayerInZone();
        SetMuted(!_isPlayerInside);
    }

    private void OnDestroy()
    {
        _unmuteTween?.Kill();
    }

    // 트리거 이벤트 대신 매 프레임 위치로 판정한다.
    // 리스폰은 CharacterController를 끈 채 순간이동하므로 OnTriggerEnter/Exit가 오지 않을 수 있기 때문이다.
    private void Update()
    {
        bool isInside = IsPlayerInZone();
        if (isInside == _isPlayerInside) return;

        _isPlayerInside = isInside;
        _unmuteTween?.Kill();

        if (isInside)
            _unmuteTween = DOVirtual.DelayedCall(unmuteDelay, () => SetMuted(false));
        else
            SetMuted(true);
    }

    private bool IsPlayerInZone()
    {
        return player != null && _zone.bounds.Contains(player.position);
    }

    // mute는 volume과 별개라서, 장치가 volume을 직접 바꿔도(SparkWire의 지직 소리 등) 서로 충돌하지 않는다.
    private void SetMuted(bool muted)
    {
        foreach (AudioSource source in audioSources)
        {
            if (source != null)
                source.mute = muted;
        }
    }

    [Button("음소거 테스트")]
    private void TestMute() => SetMuted(true);

    [Button("음소거 해제 테스트")]
    private void TestUnmute() => SetMuted(false);
}
