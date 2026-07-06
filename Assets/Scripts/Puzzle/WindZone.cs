using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 우산을 펼친 플레이어를 밀거나 띄우는 바람 구역이다.
// 수직 성분이 우세한 방향이면 접지 여부와 무관하게, 수평 성분이 우세하면
// 공중에서만 작동한다 (Update()의 힘 적용 판정 참고).
[RequireComponent(typeof(Collider))]
public class WindZone : MonoBehaviour
{
    public enum WindMode { Constant, Intermittent }

    [Title("바람 설정")]
    [SerializeField, LabelText("바람 방향")]
    private Vector3 windDirection = Vector3.right;
    // Inspector에서 바람이 부는 방향을 지정한다. 정규화해서 사용하므로 크기는 상관없다.
    // 예: (1,0,0)은 오른쪽, (0,1,0)은 위쪽 — 위쪽 방향이면 접지 중에도 작동하는 수직풍이 된다.

    [SerializeField, LabelText("바람 세기")]
    private float windStrength = 6f;
    // 값이 클수록 플레이어가 더 강하게 밀리거나 빠르게 떠오른다.

    [Title("간헐풍 설정")]
    [SerializeField, LabelText("바람 모드")]
    private WindMode windMode = WindMode.Constant;

    [SerializeField, LabelText("바람 부는 시간"), ShowIf("windMode", WindMode.Intermittent)]
    private float onDuration = 2f;
    // 간헐풍이 켜져 있는 지속 시간(초)이다.

    [SerializeField, LabelText("바람 멈추는 시간"), ShowIf("windMode", WindMode.Intermittent)]
    private float offDuration = 2f;
    // 간헐풍이 꺼져 있는 지속 시간(초)이다.

    [SerializeField, LabelText("전환 페이드 시간")]
    private float fadeDuration = 0.4f;
    // On/Off 전환 시 바람 세기가 부드럽게 바뀌는 데 걸리는 시간이다.

    [Title("연출 설정")]
    [SerializeField, LabelText("바람 소리")]
    private AudioClip windSound;
    // Inspector에서 바람 소리 클립을 연결한다. 비워두면 소리 없이 동작한다.

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;
    // Inspector에서 이 WindZone 오브젝트의 AudioSource를 연결한다. 3D 스페이셜로 설정해야
    // 존에 가까울수록 크게, 멀수록 작게 들린다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("존 안의 플레이어")]
    private PlayerController _playerInZone;
    private UmbrellaTool _umbrellaInZone;

    [ReadOnly, ShowInInspector, LabelText("현재 활성 여부")]
    private bool _isActive = true;

    private Collider _collider;
    private float _strengthMultiplier = 1f;
    private Tween _fadeTween;
    private Tween _audioFadeTween;
    private Coroutine _cycleRoutine;
    private bool _wasApplying;

    // 수직 성분이 수평 성분보다 크면 수직풍으로 취급한다.
    // 수직풍은 지상에서 우산을 펼치기만 해도 작동해야 "위로 슈웅" 이동이 가능하기 때문이다.
    private bool IsVerticalDominant => Mathf.Abs(windDirection.y) > Mathf.Abs(windDirection.x);

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        // 플레이어가 그냥 통과해야 하므로 트리거로 강제한다.
        _collider.isTrigger = true;
    }

    private void OnEnable()
    {
        if (windMode == WindMode.Intermittent)
            _cycleRoutine = StartCoroutine(IntermittentCycle());
        else
            SetActive(true);
    }

    private void OnDisable()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        _playerInZone?.ClearWindZone();
        _playerInZone = null;
        _umbrellaInZone = null;
        StopEffects();
        // Update()의 edge 판정용 플래그도 같이 초기화해야, 다시 활성화된 뒤 canApply가
        // 곧바로 true가 되어도 PlayEffects()가 정상적으로 다시 호출된다.
        _wasApplying = false;
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _audioFadeTween?.Kill();
    }

    private IEnumerator IntermittentCycle()
    {
        while (true)
        {
            SetActive(true);
            yield return new WaitForSeconds(onDuration);

            SetActive(false);
            yield return new WaitForSeconds(offDuration);
        }
    }

    // 바람의 On/Off 상태를 바꾸고, 세기를 fadeDuration 동안 부드럽게 전환한다.
    private void SetActive(bool active)
    {
        _isActive = active;

        _fadeTween?.Kill();
        float targetMultiplier = active ? 1f : 0f;
        _fadeTween = DOTween.To(
            () => _strengthMultiplier,
            x => _strengthMultiplier = x,
            targetMultiplier,
            fadeDuration
        );
    }

    [Button("강제 On/Off 토글 테스트")]
    private void TestToggle() => SetActive(!_isActive);

    private void Update()
    {
        if (_playerInZone == null) return;

        bool umbrellaOpen = _umbrellaInZone != null && _umbrellaInZone.IsOpen;
        bool canApply = _isActive && umbrellaOpen
            && (IsVerticalDominant || !_playerInZone.IsGrounded);

        if (canApply)
        {
            Vector3 velocity = windDirection.normalized * (windStrength * _strengthMultiplier);
            _playerInZone.SetWindZone(velocity);
        }
        else
        {
            _playerInZone.ClearWindZone();
        }

        // 매 프레임이 아니라 canApply가 바뀌는 경계에서만 재생/정지를 트리거해야
        // StopEffects()의 페이드아웃 트윈이 매 프레임 다시 시작되며 끊기지 않는다.
        if (canApply && !_wasApplying)
            PlayEffects();
        else if (!canApply && _wasApplying)
            StopEffects();

        _wasApplying = canApply;
    }

    private void PlayEffects()
    {
        if (audioSource == null || windSound == null) return;

        _audioFadeTween?.Kill();

        if (!audioSource.isPlaying)
        {
            audioSource.clip = windSound;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.Play();
        }

        _audioFadeTween = audioSource.DOFade(1f, fadeDuration);
    }

    private void StopEffects()
    {
        if (audioSource == null) return;

        _audioFadeTween?.Kill();
        _audioFadeTween = audioSource.DOFade(0f, fadeDuration)
            .OnComplete(() =>
            {
                if (audioSource != null)
                    audioSource.Stop();
            });
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerInZone = player;
        // FanTool/UmbrellaTool의 기존 참조 탐색 방식과 동일하게, 자식에서 못 찾으면
        // 씬 전체에서 하나 찾는다 (도구가 플레이어와 분리된 오브젝트에 있을 수도 있어서).
        _umbrellaInZone = player.GetComponentInChildren<UmbrellaTool>();
        if (_umbrellaInZone == null)
            _umbrellaInZone = FindFirstObjectByType<UmbrellaTool>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != _playerInZone) return;

        _playerInZone.ClearWindZone();
        _playerInZone = null;
        _umbrellaInZone = null;
        StopEffects();
        // 존을 벗어난 뒤 다시 들어왔을 때도 PlayEffects()가 정상적으로 재생되도록
        // edge 판정용 플래그를 초기화한다.
        _wasApplying = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();

        // 지속풍은 파란색, 간헐풍은 하늘색으로 구분해 씬 뷰에서 바로 모드를 알 수 있게 한다.
        Gizmos.color = windMode == WindMode.Constant
            ? new Color(0.2f, 0.4f, 1f)
            : new Color(0.4f, 0.8f, 1f);

        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        // 존 중심에서 바람 방향으로 화살표(선 + 화살촉)를 그려 방향을 미리 확인할 수 있게 한다.
        Vector3 dir = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector3.right;
        Vector3 start = transform.position;
        Vector3 end = start + dir * 2f;
        Gizmos.DrawLine(start, end);

        Vector3 back = -dir * 0.4f;
        Vector3 side = Vector3.Cross(dir, Vector3.forward).normalized * 0.3f;
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(dir, Vector3.up).normalized * 0.3f;

        Gizmos.DrawLine(end, end + back + side);
        Gizmos.DrawLine(end, end + back - side);
    }
#endif
}
