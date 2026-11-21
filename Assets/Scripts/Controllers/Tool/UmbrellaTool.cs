using UnityEngine;
using Sirenix.OdinInspector;

// 우산 도구를 처리한다.
// 땅에서 사용하면 비를 막고, 공중에서 사용하면 낙하 속도를 줄인다.
// 사용 버튼을 누르는 동안만 우산이 펼쳐지며, 손을 떼면 즉시 접힌다.
// 공중 글라이드 중에는 PlayerStamina를 소비한다 (바람을 타는 중에는 소비하지 않는다).
// 스태미나가 바닥나면 강제로 접히고, 버튼을 뗐다가 다시 눌러야 재사용할 수 있다.
public class UmbrellaTool : BaseTool
{
    [Title("글라이드 설정")]
    [SerializeField, LabelText("글라이드 최대 낙하 속도")]
    private float glideMaxFallSpeed = -3f;
    // 우산이 펼쳐진 채 공중에 있을 때 수직 낙하 속도의 최솟값이다.
    // 음수 값이며 절댓값이 작을수록 더 천천히 떨어진다.

    [Title("스태미나")]
    [SerializeField, LabelText("플레이어 스태미나 (비우면 자동)")]
    private PlayerStamina stamina;
    // 비워두면 Start()에서 플레이어에게서 자동으로 찾는다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("우산 펼침 여부")]
    private bool _isOpen;

    // 비 차단 여부를 외부(날씨 시스템 등)에서 확인할 수 있다.
    public bool IsOpen => _isOpen;

    private PlayerController _player;
    private PlayerAnimatorController _playerAnimator;

    private int _lastWindFrame = -100;
    // WindZoneVolume이 마지막으로 바람을 적용한 프레임이다.
    // 업데이트 순서가 불확실하므로 1프레임 오차까지 '바람 타는 중'으로 본다.

    private bool _blockedUntilRelease;
    // 소진으로 강제 종료된 뒤, 버튼을 뗄 때까지 우산이 다시 펼쳐지지 않게 막는 플래그다.

    private bool IsRidingWind => Time.frameCount - _lastWindFrame <= 1;

    // WindZoneVolume이 바람을 실제로 적용할 때마다 호출한다 (보스 패턴 바람과 구분하기 위함).
    public void MarkRidingWind()
    {
        _lastWindFrame = Time.frameCount;
    }

    private void Start()
    {
        // Start()에서 탐색해야 모든 컴포넌트의 Awake()가 끝난 뒤 안전하게 참조할 수 있다.
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();
        if (_player != null)
            _playerAnimator = _player.GetComponent<PlayerAnimatorController>();
        if (stamina == null && _player != null)
            stamina = _player.GetComponent<PlayerStamina>();
    }

    // 사용 버튼을 누르는 동안 매 프레임 호출된다.
    public override void OnUseFrame()
    {
        // 소진 후 버튼을 계속 누르고 있는 동안은 다시 펼치지 않는다.
        if (_blockedUntilRelease) return;

        // 스태미나가 부족한 상태에서 누른 경우 펼치지 않고 버튼을 뗄 때까지 막는다.
        if (stamina != null && !stamina.CanUse)
        {
            _blockedUntilRelease = true;
            StopUsing();
            HideVisual();
            return;
        }

        _isOpen = true;

        bool isGliding = _player != null && !_player.IsGrounded;

        if (isGliding)
            _player.SetMaxFallSpeed(glideMaxFallSpeed);
        else
            _player?.ClearMaxFallSpeed();

        _playerAnimator?.SetUmbrella(true, isGliding);

        if (stamina != null)
        {
            // 우산을 쓰는 동안은 회복하지 않는다.
            stamina.PauseRegen();

            // 바람을 타는 중이거나 지상(비 막기)이면 소비하지 않는다.
            if (isGliding && !IsRidingWind)
                stamina.Drain(Time.deltaTime);

            if (stamina.IsExhausted)
            {
                StopUsing();
                HideVisual();
                _blockedUntilRelease = true;
            }
        }
    }

    // 사용 버튼을 뗐을 때 호출된다.
    public override void OnUseRelease()
    {
        _blockedUntilRelease = false;
        StopUsing();
    }

    // 도구 해제 또는 강제 중단 시 호출된다.
    public override void StopUsing()
    {
        _isOpen = false;
        _player?.ClearMaxFallSpeed();

        _playerAnimator?.SetUmbrella(false, false);
    }

    public override void OnUnequip()
    {
        _blockedUntilRelease = false;
        StopUsing();
        base.OnUnequip();
        // base.OnUnequip()에서 비주얼 사라짐 연출을 처리한다.
    }

    [Button("우산 펼침 테스트")]
    private void TestOpen()
    {
        _isOpen = true;
        Debug.Log("[UmbrellaTool] 우산 펼침 테스트");
    }

    [Button("우산 접힘 테스트")]
    private void TestClose()
    {
        StopUsing();
        Debug.Log("[UmbrellaTool] 우산 접힘 테스트");
    }
}
