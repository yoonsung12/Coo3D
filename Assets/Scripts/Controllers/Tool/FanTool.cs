using UnityEngine;
using Sirenix.OdinInspector;

// 선풍기 도구를 처리한다.
// 일반 바람(Blowing)과 충전 후 블라스트(Charging→Blast) 두 가지 모드로 작동한다.
public class FanTool : BaseTool
{
    private enum FanState { Idle, Blowing, Charging }

    [Title("일반 바람 설정")]
    [SerializeField, LabelText("바람 힘")]
    private float windForce = 8f;
    // 값이 클수록 오브젝트를 더 강하게 민다.

    [SerializeField, LabelText("바람 사거리")]
    private float windRange = 5f;
    // 바람이 닿는 최대 거리다.

    [SerializeField, LabelText("바람 박스 크기 (너비/높이)")]
    private Vector2 windBoxSize = new Vector2(1f, 1.2f);
    // BoxCast의 XY 크기다. 너비가 클수록 더 넓은 범위에 바람이 닿는다.

    [SerializeField, LabelText("플레이어 반동 속도")]
    private float recoilForce = 2f;
    // 선풍기를 쏠 때 플레이어가 반대 방향으로 밀리는 힘이다.

    [Title("충전 블라스트 설정")]
    [SerializeField, LabelText("최대 충전 시간")]
    private float maxChargeTime = 2f;
    // 이 시간까지 충전하면 3단계 블라스트가 발동된다.

    [SerializeField, LabelText("충전 중 이동속도 배율")]
    private float chargeSlowMultiplier = 0.3f;
    // 충전 중 플레이어 이동속도를 줄여 신중하게 조준하도록 유도한다.

    [BoxGroup("블라스트 오브젝트 힘")]
    [SerializeField, LabelText("1단계")] private float blastForceLevel1 = 15f;
    [BoxGroup("블라스트 오브젝트 힘")]
    [SerializeField, LabelText("2단계")] private float blastForceLevel2 = 25f;
    [BoxGroup("블라스트 오브젝트 힘")]
    [SerializeField, LabelText("3단계")] private float blastForceLevel3 = 40f;

    [BoxGroup("블라스트 플레이어 반동")]
    [SerializeField, LabelText("1단계")] private float chargeRecoilLevel1 = 1.5f;
    [BoxGroup("블라스트 플레이어 반동")]
    [SerializeField, LabelText("2단계")] private float chargeRecoilLevel2 = 3f;
    [BoxGroup("블라스트 플레이어 반동")]
    [SerializeField, LabelText("3단계")] private float chargeRecoilLevel3 = 5f;

    [Title("레이어 설정")]
    [SerializeField, LabelText("바람 감지 레이어")]
    private LayerMask blowableLayer;
    // Inspector에서 바람에 반응할 오브젝트들의 레이어를 설정한다.

    [Title("연결 컴포넌트")]
    [SerializeField, LabelText("바람 이펙트")]
    private FanWindEffect windEffect;
    // Inspector에서 FanWindEffect 컴포넌트를 연결한다. 없으면 이펙트 없이 동작한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    private FanState _state = FanState.Idle;

    [ReadOnly, ShowInInspector, LabelText("충전 진행도")]
    [ProgressBar(0, 1)]
    private float _chargeRatio;

    private float _chargeTime;
    private PlayerController _player;

    private void Start()
    {
        // Start()에서 탐색해야 모든 컴포넌트의 Awake()가 완료된 뒤 안전하게 참조할 수 있다.
        // FanTool은 플레이어의 자식이거나 같은 오브젝트에 붙어 있어야 한다.
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();
        Debug.Log($"[FanTool] Start() 호출됨. _player={_player}");
    }

    private void FixedUpdate()
    {
        // 일반 바람은 FixedUpdate에서 물리 힘을 적용한다.
        if (_state == FanState.Blowing)
            BlowWind();
    }

    // ToolManager에서 바람 버튼을 누르는 동안 Update마다 호출한다.
    public void OnBlowFrame()
    {
        if (_state == FanState.Charging) return;

        if (_state == FanState.Idle)
        {
            _state = FanState.Blowing;
            windEffect?.Play();
        }
    }

    // ToolManager에서 충전 버튼(Shift+Attack)을 누르는 동안 Update마다 호출한다.
    public void OnChargeFrame()
    {
        if (_state != FanState.Charging)
        {
            _state = FanState.Charging;
            windEffect?.Stop();
            _player?.SetSpeedMultiplier(chargeSlowMultiplier);
        }

        _chargeTime += Time.deltaTime;
        _chargeTime = Mathf.Min(_chargeTime, maxChargeTime);
        _chargeRatio = _chargeTime / maxChargeTime;
    }

    // ToolManager에서 바람 버튼을 뗄 때 호출한다.
    public void OnBlowRelease()
    {
        if (_state == FanState.Charging)
            Blast();

        StopUsing();
    }

    public override void StopUsing()
    {
        _state = FanState.Idle;
        _chargeTime = 0f;
        _chargeRatio = 0f;
        _player?.SetSpeedMultiplier(1f);
        windEffect?.Stop();
    }

    private void BlowWind()
    {
        if (_player == null) return;

        Vector3 windDir = _player.FacingDirection;
        // 플레이어 중심에서 약간 앞쪽을 BoxCast 시작점으로 사용한다.
        Vector3 origin = _player.transform.position + Vector3.up * 0.5f;

        // 플레이어가 바라보는 방향으로 BoxCast를 해 IBlowable 오브젝트를 감지한다.
        // 바람 방향에 수직인 축의 회전을 계산해 BoxCast가 이동 방향을 정면으로 향하게 한다.
        Quaternion rotation = windDir != Vector3.zero ? Quaternion.LookRotation(windDir) : Quaternion.identity;
        Vector3 halfExtents = new Vector3(windBoxSize.x * 0.5f, windBoxSize.y * 0.5f, 0.1f);

        RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, windDir, rotation, windRange, blowableLayer);

        foreach (RaycastHit hit in hits)
        {
            IBlowable blowable = hit.collider.GetComponent<IBlowable>();
            blowable?.OnBlown(windDir, windForce, false);
        }

        // 선풍기를 쏠 때 플레이어는 반대 방향으로 약하게 밀린다.
        _player.SetRecoil(-windDir * recoilForce);
    }

    private void Blast()
    {
        if (_player == null) return;

        int level = GetFanLevel();
        Vector3 windDir = _player.FacingDirection;
        Vector3 origin = _player.transform.position + Vector3.up * 0.5f;

        // 블라스트 단계에 따라 힘과 반동을 결정한다.
        float blastForce = level switch
        {
            1 => blastForceLevel1,
            2 => blastForceLevel2,
            _ => blastForceLevel3
        };
        float recoilAmount = level switch
        {
            1 => chargeRecoilLevel1,
            2 => chargeRecoilLevel2,
            _ => chargeRecoilLevel3
        };

        // OverlapBox로 전방의 오브젝트를 감지해 즉각적인 충격(Impulse)을 가한다.
        Quaternion rotation = windDir != Vector3.zero ? Quaternion.LookRotation(windDir) : Quaternion.identity;
        Vector3 blastCenter = origin + windDir * (windRange * 0.5f);
        Vector3 halfExtents = new Vector3(windBoxSize.x * 0.5f, windBoxSize.y * 0.5f, windRange * 0.5f);

        Collider[] cols = Physics.OverlapBox(blastCenter, halfExtents, rotation, blowableLayer);
        foreach (Collider col in cols)
        {
            IBlowable blowable = col.GetComponent<IBlowable>();
            blowable?.OnBlown(windDir, blastForce, true);
        }

        // 플레이어는 바람 반대 방향으로 강하게 튕겨난다.
        _player.SetBlast(-windDir * recoilAmount);

        windEffect?.PlayBlastBurst(windDir, level);
    }

    private int GetFanLevel()
    {
        // 충전 진행도를 0.33/0.66 기준으로 1/2/3단계로 나눈다.
        float ratio = _chargeTime / maxChargeTime;
        if (ratio < 0.33f) return 1;
        if (ratio < 0.66f) return 2;
        return 3;
    }

    [Button("블라스트 3단계 테스트")]
    private void TestBlast()
    {
        _chargeTime = maxChargeTime;
        Blast();
        StopUsing();
    }
}
