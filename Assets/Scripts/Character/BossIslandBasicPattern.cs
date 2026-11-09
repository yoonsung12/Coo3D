using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 탑다운 섬 아레나 전용 평범한 패턴(상시 공격) 스케줄러다.
// 사이드뷰용 BossBasicPattern은 X축 거리만 보므로 섬에서는 쓸 수 없어, 평면(XZ) 거리 기준으로 새로 만들었다.
// 하는 일: 플레이어에게 걷기 -> 플레이어를 바라보기 -> 거리에 따라 날개 쓸기/연속 쪼기/깃털 3갈래 중 하나 실행.
// 계절 패턴이 Rigidbody를 Kinematic으로 바꿔 쓰는 동안에는 절대 건드리지 않는다(물리를 두고 싸우지 않기 위해).
[RequireComponent(typeof(Boss))]
public class BossIslandBasicPattern : MonoBehaviour
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 중앙 (비우면 월드 원점)")]
    private Transform islandCenter;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("날개 쓸기")]
    private BossIslandWingSweep wingSweep;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("연속 쪼기")]
    private BossIslandDoublePeck doublePeck;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("깃털 3갈래")]
    private BossIslandFeatherShot featherShot;

    [TabGroup("설정", "스케줄")]
    [SerializeField, LabelText("공격 사이클 간격")]
    private float cycleInterval = 1.2f;
    // 한 공격이 끝난 뒤 다음 공격을 고르기까지 걷는 시간이다. 길수록 보스가 느긋해진다.

    [TabGroup("설정", "스케줄")]
    [SerializeField, LabelText("밀치기(날개 쓸기) 거리")]
    private float sweepRange = 3.5f;
    // 평면 거리가 이 값 이하로 바짝 붙었고 쿨타임이 끝났으면 날개 쓸기로 밀쳐낸다.
    // 보스가 멈추는 거리(멈춤 거리 3m)보다 조금 크게 둬야 붙어 있는 플레이어에게 발동한다.

    [TabGroup("설정", "스케줄")]
    [SerializeField, LabelText("밀치기 쿨타임")]
    private float sweepCooldown = 5f;
    // 날개 쓸기를 쓴 뒤 이 시간 동안은 붙어 있어도 쪼기를 쓴다. 클수록 쪼기 비중이 커진다.

    [TabGroup("설정", "스케줄")]
    [SerializeField, LabelText("연속 쪼기 거리")]
    private float peckRange = 4.5f;
    // 이 값 이하이면 기본 공격인 연속 쪼기를 쓰고, 이보다 멀면 깃털 3갈래를 쓴다.
    // 쪼기가 닿는 거리(두 번째 4m)와 비슷하게 둬야 헛손질이 줄어든다.

    [TabGroup("설정", "스케줄")]
    [SerializeField, LabelText("깃털 연속 최대 횟수")]
    private int maxFeatherStreak = 2;
    // 깃털만 계속 쏘면 지루하므로 이 횟수만큼 연속으로 쓰면 한 번은 쉬고 걷는다.

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("걷는 속도 (m/s)")]
    private float walkSpeed = 1.5f;

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("멈춤 거리")]
    private float stopDistance = 3f;
    // 플레이어와 평면 거리가 이 값 이하이면 더 다가가지 않는다(겹침 방지).

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("회전 속도 (도/초)")]
    private float turnSpeed = 360f;

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("섬 반지름 X")]
    private float islandRadiusX = 13.5f;

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("섬 반지름 Z")]
    private float islandRadiusZ = 9f;

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("걷기 허용 영역 비율"), Range(0.1f, 1f)]
    private float walkAreaScale = 0.85f;
    // 섬 가장자리(물가)로 걸어 나가 떨어지지 않도록 섬 크기에 곱해 안쪽만 쓴다.

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;
    // 플레이어 발이 이보다 낮으면 물에 빠진 것으로 보고 공격하지 않는다.

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("섬 복귀 점프 시간")]
    private float returnHopDuration = 0.6f;
    // 계절 패턴이 끝났을 때 보스가 물 위 발판처럼 걷기 영역 밖에 있으면, 걸어서 오다 물에 빠지지 않게
    // 이 시간 동안 섬 안쪽으로 폴짝 뛰어 돌아온다.

    [TabGroup("설정", "이동")]
    [SerializeField, LabelText("섬 복귀 점프 높이")]
    private float returnHopHeight = 2f;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 공격")]
    private string _currentAttackName = "없음";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("깃털 연속 횟수")]
    private int _featherStreak;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("플레이어와 평면 거리")]
    private float _flatDistance;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("밀치기 남은 쿨타임")]
    private float SweepCooldownRemaining => Mathf.Max(0f, _nextSweepTime - Time.time);

    private float _nextSweepTime;
    // 이 시각(Time.time) 이후부터 다시 날개 쓸기를 쓸 수 있다.

    private Boss _boss;
    private Rigidbody _rb;
    private float _timer;
    private bool _isAttacking;
    private bool _velocityZeroed;
    private bool _isReturning;
    private Sequence _returnSequence;
    // 섬 복귀 점프 중인지와 그 Tween이다. 계절 패턴이 시작되면 즉시 Kill해서 물리를 패턴에 넘긴다.

    // 외부에서 현재 공격 중인지 확인할 때 쓴다.
    public bool IsAttacking => _isAttacking;

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _rb = GetComponent<Rigidbody>();
        _timer = cycleInterval;
    }

    private void OnEnable()
    {
        _boss.OnPatternTriggered += OnSeasonPatternTriggered;
    }

    private void OnDisable()
    {
        _boss.OnPatternTriggered -= OnSeasonPatternTriggered;
        if (_isAttacking) AbortCurrentAttack();
        StopReturnHop(true);
    }

    private void OnDestroy()
    {
        _returnSequence?.Kill();
    }

    private void Update()
    {
        if (_boss.IsDead || playerController == null) return;

        _flatDistance = (GetPlayerFlat() - GetBossFlat()).magnitude;

        if (!_boss.CanBasicAttack || _isAttacking || _isReturning) return;

        // 공격 중에는 방향을 고정하므로, 회전은 공격이 아닐 때만 한다.
        // Rigidbody가 회전 고정(FreezeRotation)이라 MoveRotation 대신 루트 Y 회전만 직접 바꾼다.
        FacePlayer();

        if (IsPlayerInWater()) return;

        _timer -= Time.deltaTime; // 프레임 속도와 무관하게 같은 시간 간격을 주기 위해 쓴다.
        if (_timer > 0f) return;

        _timer = cycleInterval;
        ChooseAndStartAttack();
    }

    private void FixedUpdate()
    {
        bool canWalk = !_boss.IsDead && _boss.CanBasicAttack && !_isAttacking
                       && playerController != null && !_rb.isKinematic;

        if (!canWalk)
        {
            // 키네마틱일 때는 건드리지 않는다. 걸을 수 없게 된 순간 한 번만 속도를 0으로 만든다.
            if (!_rb.isKinematic && !_velocityZeroed) SetFlatVelocity(Vector3.zero);
            _velocityZeroed = true;
            return;
        }

        _velocityZeroed = false;

        // 계절 패턴이 보스를 물 위 발판 등 걷기 영역 밖에 두고 끝났으면, 걷지 않고 섬 안으로 점프해 돌아온다.
        if (_isReturning) return;
        if (!IsInsideWalkArea(GetBossFlat()))
        {
            StartReturnHop();
            return;
        }

        Vector3 toPlayer = GetPlayerFlat() - GetBossFlat();
        bool stop = toPlayer.magnitude <= stopDistance || IsPlayerInWater();
        if (!stop)
        {
            Vector3 dir = toPlayer.normalized;
            // 다음 물리 프레임 위치가 섬 밖이면 멈춘다.
            Vector3 next = GetBossFlat() + dir * walkSpeed * Time.fixedDeltaTime;
            if (!IsInsideWalkArea(next)) stop = true;
            else SetFlatVelocity(dir * walkSpeed);
        }
        if (stop) SetFlatVelocity(Vector3.zero);
    }

    // 섬 타원(걷기 허용 비율 적용) 안인지 확인한다. 연속 쪼기의 전진 판정에도 쓴다.
    public bool IsInsideWalkArea(Vector3 flatPos)
    {
        Vector3 c = islandCenter != null ? islandCenter.position : Vector3.zero;
        float nx = (flatPos.x - c.x) / (islandRadiusX * walkAreaScale);
        float nz = (flatPos.z - c.z) / (islandRadiusZ * walkAreaScale);
        return nx * nx + nz * nz <= 1f;
    }

    private void ChooseAndStartAttack()
    {
        float d = _flatDistance;
        Vector3 dir = GetDirectionToPlayer();

        // 바짝 붙었고 쿨타임이 끝났을 때만 밀쳐낸다. 나머지 근중거리는 기본 공격인 쪼기다.
        if (d <= sweepRange && Time.time >= _nextSweepTime)
        {
            _featherStreak = 0;
            _nextSweepTime = Time.time + sweepCooldown;
            StartAttack("날개 쓸기", dir, wingSweep != null ? (System.Action<Vector3, System.Action>)wingSweep.Execute : null);
        }
        else if (d <= peckRange || _featherStreak >= maxFeatherStreak)
        {
            // 깃털을 연속 최대 횟수만큼 썼으면 쉬지 않고 쪼기로 대신한다(멀어도 다가오며 쪼는 압박).
            _featherStreak = 0;
            StartAttack("연속 쪼기", dir, doublePeck != null ? (System.Action<Vector3, System.Action>)doublePeck.Execute : null);
        }
        else
        {
            _featherStreak++;
            StartAttack("깃털 3갈래", dir, featherShot != null ? (System.Action<Vector3, System.Action>)featherShot.Execute : null);
        }
    }

    private void StartAttack(string attackName, Vector3 dir, System.Action<Vector3, System.Action> execute)
    {
        if (execute == null) return;

        _isAttacking = true;
        _currentAttackName = attackName;
        _boss.SetBasicAttacking(true);
        SetFlatVelocity(Vector3.zero);
        execute(dir, OnAttackFinished);
    }

    private void OnAttackFinished()
    {
        _isAttacking = false;
        _currentAttackName = "없음";
        _boss.SetBasicAttacking(false);
    }

    // 걷기 영역 밖에 있는 보스를 섬 중앙 방향의 영역 안쪽(80% 지점)으로 점프시킨다.
    // Kinematic으로 바꿔 DOJump로 옮기는 이유: 물 위를 걸어 건너면 중력 때문에 물에 빠지기 때문이다.
    private void StartReturnHop()
    {
        Vector3 c = islandCenter != null ? islandCenter.position : Vector3.zero;
        Vector3 flat = GetBossFlat();
        float nx = (flat.x - c.x) / (islandRadiusX * walkAreaScale);
        float nz = (flat.z - c.z) / (islandRadiusZ * walkAreaScale);
        float scale = 0.8f / Mathf.Max(0.0001f, Mathf.Sqrt(nx * nx + nz * nz));
        Vector3 target = new Vector3(c.x + (flat.x - c.x) * scale, _rb.position.y, c.z + (flat.z - c.z) * scale);

        _isReturning = true;
        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true;

        _returnSequence?.Kill();
        _returnSequence = DOTween.Sequence()
            .Append(_rb.DOJump(target, returnHopHeight, 1, returnHopDuration).SetEase(Ease.Linear))
            .OnComplete(() => StopReturnHop(true))
            .SetLink(gameObject);
    }

    // restorePhysics가 false면 Rigidbody를 건드리지 않는다(계절 패턴이 이미 물리를 가져간 경우).
    private void StopReturnHop(bool restorePhysics)
    {
        if (!_isReturning) return;
        _returnSequence?.Kill();
        _returnSequence = null;
        _isReturning = false;
        if (restorePhysics && _rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
        }
    }

    // 계절 패턴이 시작되면 진행 중인 평범한 공격과 복귀 점프를 즉시 끊는다.
    private void OnSeasonPatternTriggered(Boss.SeasonPattern pattern)
    {
        StopReturnHop(false);
        if (_isAttacking) AbortCurrentAttack();
        _timer = cycleInterval;
    }

    private void AbortCurrentAttack()
    {
        if (wingSweep != null) wingSweep.Abort();
        if (doublePeck != null) doublePeck.Abort();
        if (featherShot != null) featherShot.Abort();

        _isAttacking = false;
        _currentAttackName = "없음";
        _boss.SetBasicAttacking(false);
        if (!_rb.isKinematic) SetFlatVelocity(Vector3.zero);
    }

    private void FacePlayer()
    {
        Vector3 dir = GetDirectionToPlayer();
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    // 중력(Y 속도)은 그대로 두고 XZ 속도만 바꾼다.
    private void SetFlatVelocity(Vector3 flatVelocity)
    {
        Vector3 v = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(flatVelocity.x, v.y, flatVelocity.z);
    }

    private Vector3 GetDirectionToPlayer()
    {
        Vector3 d = GetPlayerFlat() - GetBossFlat();
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    private Vector3 GetBossFlat()
    {
        Vector3 p = transform.position;
        return new Vector3(p.x, 0f, p.z);
    }

    private Vector3 GetPlayerFlat()
    {
        Vector3 p = playerController.transform.position;
        return new Vector3(p.x, 0f, p.z);
    }

    private bool IsPlayerInWater()
    {
        if (playerController == null) return false;

        CharacterController cc = playerController.GetComponent<CharacterController>();
        float feetY = cc != null ? cc.bounds.min.y : playerController.transform.position.y;
        return feetY < fallY;
    }

    private void ForceAttack(string attackName, System.Action<Vector3, System.Action> execute)
    {
        if (!Application.isPlaying || _isAttacking || execute == null || playerController == null) return;
        StartAttack(attackName, GetDirectionToPlayer(), execute);
    }

    [Button("날개 쓸기 테스트")]
    private void TestWingSweep() => ForceAttack("날개 쓸기", wingSweep != null ? (System.Action<Vector3, System.Action>)wingSweep.Execute : null);

    [Button("연속 쪼기 테스트")]
    private void TestDoublePeck() => ForceAttack("연속 쪼기", doublePeck != null ? (System.Action<Vector3, System.Action>)doublePeck.Execute : null);

    [Button("깃털 3갈래 테스트")]
    private void TestFeatherShot() => ForceAttack("깃털 3갈래", featherShot != null ? (System.Action<Vector3, System.Action>)featherShot.Execute : null);
}
