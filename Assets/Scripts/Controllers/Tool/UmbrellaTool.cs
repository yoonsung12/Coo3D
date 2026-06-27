using UnityEngine;
using Sirenix.OdinInspector;

// 우산 도구를 처리한다.
// 땅에서 사용하면 비를 막고, 공중에서 사용하면 낙하 속도를 줄인다.
// 사용 버튼을 누르는 동안만 우산이 펼쳐지며, 손을 떼면 즉시 접힌다.
public class UmbrellaTool : BaseTool
{
    [Title("글라이드 설정")]
    [SerializeField, LabelText("글라이드 최대 낙하 속도")]
    private float glideMaxFallSpeed = -3f;
    // 우산이 펼쳐진 채 공중에 있을 때 수직 낙하 속도의 최솟값이다.
    // 음수 값이며 절댓값이 작을수록 더 천천히 떨어진다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("우산 펼침 여부")]
    private bool _isOpen;

    // 비 차단 여부를 외부(날씨 시스템 등)에서 확인할 수 있다.
    public bool IsOpen => _isOpen;

    private PlayerController _player;

    private void Start()
    {
        // Start()에서 탐색해야 모든 컴포넌트의 Awake()가 끝난 뒤 안전하게 참조할 수 있다.
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();
    }

    // 사용 버튼을 누르는 동안 매 프레임 호출된다.
    public override void OnUseFrame()
    {
        if (!_isOpen)
            _isOpen = true;

        // 공중에 있을 때만 낙하 속도를 제한한다.
        // 땅에서는 수직 속도를 건드리지 않아 점프/이동에 영향을 주지 않는다.
        if (_player != null && !_player.IsGrounded)
            _player.SetMaxFallSpeed(glideMaxFallSpeed);
        else
            _player?.ClearMaxFallSpeed();
    }

    // 사용 버튼을 뗐을 때 호출된다.
    public override void OnUseRelease()
    {
        StopUsing();
    }

    // 도구 해제 또는 강제 중단 시 호출된다.
    public override void StopUsing()
    {
        _isOpen = false;
        _player?.ClearMaxFallSpeed();
    }

    public override void OnUnequip()
    {
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
