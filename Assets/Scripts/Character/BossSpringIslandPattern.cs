using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 봄 패턴(꽃가루 돌진) 중 "탑다운 원형섬 아레나" 전용 버전이다.
// 기존 사이드뷰용 BossSpringPattern은 건드리지 않고, 아레나 씬에서는 이 컴포넌트만 켠다.
//
// 흐름: 섬 중앙으로 이동 -> (전조 -> 돌진 -> 발판 착지 + 파도) 반복.
//  - 돌진 목표는 플레이어 방향과 가장 가까운 "다른" 발판이고, 다음 돌진은 착지한 발판에서 시작한다.
//  - 돌진 횟수가 일정 이상이면 바위가 솟아오르고, 돌진 경로에 바위가 있으면 보스가 들이받아 부서지며 파훼된다.
//
// 돌진 이동은 Boss의 Rigidbody를 직접 MovePosition으로 움직인다 — BossSpringPattern과 같은 물리 이동 규칙의
// 예외다. 패턴이 도는 동안은 isKinematic을 켜서 지형에 막히거나 떨어지지 않게 하고, 끝나면 원래 상태로 복구한다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossSpringIslandPattern : BossSeasonPatternBase
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;
    // 플레이어 위치 추적, 파도/밀어내기(SetBlast) 적용에 사용한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 중앙")]
    private Transform centerPoint;
    // 패턴 시작 시 보스가 모이는 지점. 비워두면 (0,0,0)을 쓴다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발판 목록 (14개)")]
    private Transform[] platforms;
    // 섬 주변 Platform_L1~L4, R1~R4, T1~T3, B1~B3를 연결한다. 윗면 높이는 지면(y=0)과 같다고 가정한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바위 목록")]
    private IslandRock[] rocks;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("레인 예고")]
    private GroundTelegraph laneTelegraph;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("목표 원 예고")]
    private GroundTelegraph circleTelegraph;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바위 예고 프리팹")]
    private GroundTelegraph rockTelegraphPrefab;
    // 바위가 솟기 전 바위마다 임시로 생성하는 원 예고. 사용 후 파괴한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("파도 프리팹")]
    private BossSpringWave wavePrefab;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("돌진 접촉 히트박스")]
    private EnemyAttackHitbox dashHitbox;
    // Boss 자식 DashHitbox. 돌진 중에만 켠다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;
    // 착지/충돌 시 Shake()를 호출하기 위한 참조다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;
    // 회전 연출과 착지 펀치 스케일에 쓴다. 루트(Rigidbody)를 직접 줄이면 물리가 찌그러지므로 자식에 건다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("착지음 AudioSource (선택)")]
    private AudioSource audioSource;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("착지음 (선택)")]
    private AudioClip landingClip;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("보스 서 있는 높이(중심 Y)")]
    private float standHeightY = 1f;
    // 콜라이더 반높이 1.0 + 지면 Y 0. 모든 발판/섬 이동에서 이 높이를 유지한다.

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("중앙 이동 시간")]
    private float entryDuration = 1f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("전조 시간")]
    private float telegraphDuration = 1.6f;
    // 전조가 길수록 플레이어가 돌진을 읽고 피할 시간이 늘어난다.

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("고정(Lock) 시간")]
    private float lockDuration = 0.6f;
    // 전조의 마지막 이 시간 동안은 플레이어를 따라가지 않고 예고가 고정된다.

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("레인 폭")]
    private float laneWidth = 4f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("목표 원 반지름")]
    private float circleRadius = 2f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("돌진 속도(m/s)")]
    private float dashSpeed = 15f;
    // 거리와 상관없이 일정한 속도로 돌진한다(시간 = 거리 / 속도). 값이 작을수록 돌진이 느려 반응하기 쉽다.

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("돌진 최소 시간")]
    private float minDashDuration = 0.4f;
    // 가까운 거리도 너무 순식간에 끝나지 않게 하는 하한이다.

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("돌진 포물선 높이")]
    private float dashArcHeight = 0.8f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("보스 몸 반지름(바위 충돌용)")]
    private float bossBodyRadius = 1f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("착지 후 멈춤 시간")]
    private float landingPause = 2f;
    // 착지 후 다음 전조까지 쉬는 시간. 길수록 플레이어가 숨 돌릴 여유가 생긴다.

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("회전 연출 한 바퀴 시간")]
    private float spinDuration = 0.4f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("착지 펀치 스케일 세기")]
    private float landingPunchStrength = 0.3f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("착지 카메라 흔들림 (시간)")]
    private float landingShakeDuration = 0.3f;

    [TabGroup("설정", "돌진")]
    [SerializeField, LabelText("착지 카메라 흔들림 (세기)")]
    private float landingShakeStrength = 0.3f;

    [TabGroup("설정", "파도")]
    [SerializeField, LabelText("파도 시작 위치 (발판 중심에서 섬 안쪽으로)")]
    private float waveSpawnInset = 3f;
    // 발판 중심에서 섬 중앙 방향으로 이만큼 들어간 지점에서 파도가 시작된다.

    [TabGroup("설정", "파도")]
    [SerializeField, LabelText("섬 반축 X")]
    private float islandSemiAxisX = 13.5f;
    // 섬 타원의 X 반지름. 파도가 섬 반대편 가장자리까지만 가도록 이동 거리를 계산하는 데 쓴다.

    [TabGroup("설정", "파도")]
    [SerializeField, LabelText("섬 반축 Z")]
    private float islandSemiAxisZ = 9f;

    [TabGroup("설정", "파도")]
    [SerializeField, LabelText("섬 가장자리 여유 거리")]
    private float waveEdgeMargin = 2f;
    // 섬 가장자리를 지난 뒤 이만큼 더 가서 사라진다. 클수록 파도가 물 위로 더 나간다.

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위가 솟기까지 돌진 횟수")]
    private int dashesBeforeRocks = 3;

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 하나당 유인 기회(돌진 횟수)")]
    private int rockLureChances = 2;
    // 바위가 솟은 뒤 이 횟수만큼 돌진이 끝날 때까지 못 박으면 그 바위는 들어가고 다른 바위가 솟는다.
    // 클수록 한 바위로 유인할 여유가 길어진다.

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 교체 대기")]
    private float rockSwapPause = 0.5f;
    // 실패한 바위가 들어간 뒤 다음 바위 예고가 뜨기까지 잠깐 쉬는 시간이다.

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 솟은 뒤 추가 대기")]
    private float postRockRisePause = 1f;
    // 바위가 솟은 직후 바로 다음 전조가 시작되지 않도록 주는 여유 시간이다.

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("충돌 경고 색(레인/원)")]
    private Color crashLaneColor = new Color(1f, 0.9f, 0.1f, 0.6f);
    // 이번 돌진이 바위에 부딪힐 때 예고가 이 색으로 바뀌고 바위도 떨린다.

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 예고 시간")]
    private float rockTelegraphDuration = 1f;

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 예고 여유 반지름")]
    private float rockTelegraphMargin = 0.5f;

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("겹친 플레이어 밀어내는 속도")]
    private float rockPushSpeed = 10f;

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 충돌 카메라 흔들림 (시간)")]
    private float crashShakeDuration = 0.5f;

    [TabGroup("설정", "바위")]
    [SerializeField, LabelText("바위 충돌 카메라 흔들림 (세기)")]
    private float crashShakeStrength = 0.6f;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("돌진 횟수")]
    private int _dashCount;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("목표 발판")]
    private string _targetPlatformName = "-";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("지금 솟은 바위")]
    private IslandRock _activeRock;
    // 한 번에 하나의 바위만 솟는다. null이면 아직 바위가 안 나왔거나 교체 중이다.

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("남은 유인 기회")]
    private int _lureChancesLeft;

    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private Tween _spinTween;
    private Tween _punchTween;
    private Quaternion _visualStartLocalRotation;
    private readonly List<BossSpringWave> _waves = new List<BossSpringWave>();
    private readonly List<GroundTelegraph> _rockTelegraphs = new List<GroundTelegraph>();

    private int _currentPlatformIndex = -1; // -1이면 섬 중앙에 있다.
    private int _planPlatformIndex;
    private IslandRock _planRock;
    private IslandRock _warningRock; // 현재 경고 연출 중인 바위(없으면 null)
    private Vector3 _planEnd; // 돌진 도착 지점(지면 높이, 보스 중심 Y는 standHeightY)

    protected override Boss.SeasonPattern Season => Boss.SeasonPattern.Spring;
    protected override bool IsRunning => _cycleRoutine != null;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();
        if (bossVisual != null) _visualStartLocalRotation = bossVisual.localRotation;

        if (dashHitbox != null)
            dashHitbox.DisableHitbox();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    protected override void StartCycle()
    {
        _cycleRoutine = StartCoroutine(SpringCycleRoutine());
    }

    // 진행 중인 사이클을 파훼 성공 없이 즉시 중단하고 물리/연출 상태를 원래대로 되돌린다.
    protected override void Abort()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        KillTweens();
        if (bossVisual != null) bossVisual.localRotation = _visualStartLocalRotation;

        if (dashHitbox != null) dashHitbox.DisableHitbox();
        HideTelegraphsImmediate();
        ClearCrashWarning();
        ClearRockTelegraphs();
        RestorePhysics();

        // 다른 패턴에 밀려 중단되면 솟아 있던 바위도 다시 땅속으로 돌려보낸다.
        if (_activeRock != null) _activeRock.Sink();
        _activeRock = null;

        foreach (BossSpringWave wave in _waves)
        {
            if (wave != null) wave.Cancel();
        }
        _waves.Clear();

        _currentPhase = "대기";
    }

    private IEnumerator SpringCycleRoutine()
    {
        _dashCount = 0;
        _currentPlatformIndex = -1;
        _targetPlatformName = "-";
        _activeRock = null;
        _lureChancesLeft = 0;

        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true; // 사이클 동안 지형에 막히거나 떨어지지 않도록 고정한다.

        _currentPhase = "중앙 이동";
        Vector3 center = GetCenterPosition();
        yield return MoveArcRoutine(center, entryDuration, 1.5f);

        while (_boss.IsInvincible)
        {
            yield return TelegraphRoutine();
            if (!_boss.IsInvincible) break;

            yield return DashRoutine();
            if (!_boss.IsInvincible) break;

            if (_planRock != null)
            {
                CrashIntoRock();
                break; // 파훼 -> 사이클 종료
            }

            yield return LandRoutine();
            if (!_boss.IsInvincible) break;

            _dashCount++;
            if (_activeRock != null)
            {
                // 솟은 바위로 유인하지 못한 채 돌진이 끝났다. 기회를 다 쓰면 다른 바위로 바꾼다.
                _lureChancesLeft--;
                if (_lureChancesLeft <= 0)
                    yield return SwapRockRoutine();
            }
            else if (_dashCount >= dashesBeforeRocks)
            {
                yield return RiseRandomRockRoutine(null);
            }
        }

        ClearCrashWarning();
        _currentPhase = "대기";
        RestorePhysics();
        _cycleRoutine = null;
    }

    // 전조: 보스가 회전하며 레인/목표 원이 플레이어를 따라가다가 마지막 lockDuration 동안 고정된다.
    private IEnumerator TelegraphRoutine()
    {
        _currentPhase = "전조";

        _spinTween = bossVisual != null
            ? bossVisual.DORotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
                .SetRelative(true).SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental)
            : null;

        float followTime = Mathf.Max(telegraphDuration - lockDuration, 0f);
        float timer = 0f;
        while (_boss.IsInvincible && timer < followTime)
        {
            PlanDash();
            ShowPlan();
            timer += Time.deltaTime;
            yield return null;
        }

        PlanDash();
        ShowPlan();
        laneTelegraph.Lock();
        circleTelegraph.Lock();

        if (_spinTween != null) _spinTween.Kill();
        if (bossVisual != null)
        {
            Vector3 dir = GetFlatDirection(_rb.position, _planEnd);
            if (dir.sqrMagnitude > 0.0001f)
                bossVisual.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        yield return new WaitForSeconds(lockDuration);
    }

    // 지금 플레이어 위치 기준으로 목표 발판과(경로에 바위가 있다면) 도착 지점을 다시 계산한다.
    private void PlanDash()
    {
        Vector3 bossPos = _rb.position;
        Vector3 toPlayer = playerController != null
            ? GetFlatDirection(bossPos, playerController.transform.position)
            : Vector3.forward;
        if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.forward;

        // 유인 규칙: 보스와 플레이어 사이에 솟은 바위가 있으면 발판 대신 플레이어 쪽으로 곧장 돌진해 바위를 들이받는다.
        // 플레이어가 바위 뒤에 서서 보스를 유인하면 의도대로 파훼되게 하기 위해서다.
        Vector3 startFlat = new Vector3(bossPos.x, 0f, bossPos.z);
        if (playerController != null)
        {
            Vector3 playerPos = playerController.transform.position;
            float distToPlayer = Vector3.Distance(startFlat, new Vector3(playerPos.x, 0f, playerPos.z));
            float lureT;
            IslandRock lureRock = FindRockOnPath(startFlat, toPlayer, distToPlayer, out lureT);
            if (lureRock != null)
            {
                _planRock = lureRock;
                float lureStop = Mathf.Max(lureT - lureRock.FootprintRadius - bossBodyRadius, 0.5f);
                _planEnd = startFlat + toPlayer * lureStop;
                _targetPlatformName = lureRock.name + " (바위 유인)";
                UpdateCrashWarning();
                return;
            }
        }

        // 현재 서 있는 발판을 제외하고, 플레이어 방향과 각도가 가장 작은 발판을 고른다.
        int best = -1;
        float bestAngle = float.MaxValue;
        for (int i = 0; i < platforms.Length; i++)
        {
            if (i == _currentPlatformIndex || platforms[i] == null) continue;
            Vector3 dir = GetFlatDirection(bossPos, platforms[i].position);
            if (dir.sqrMagnitude < 0.0001f) continue;

            float angle = Vector3.Angle(toPlayer, dir);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = i;
            }
        }
        if (best < 0) best = (_currentPlatformIndex + 1) % platforms.Length;

        _planPlatformIndex = best;
        Vector3 platformPos = platforms[best].position;
        Vector3 end = new Vector3(platformPos.x, 0f, platformPos.z);
        _planRock = null;

        // 경로 위의 솟아오른 바위 중 가장 가까운 것을 찾는다. 있으면 바위 앞에서 멈추고 들이받는다.
        Vector3 start = new Vector3(bossPos.x, 0f, bossPos.z);
        Vector3 pathDir = GetFlatDirection(start, end);
        float pathLength = Vector3.Distance(start, end);
        float nearestT;
        _planRock = FindRockOnPath(start, pathDir, pathLength, out nearestT);

        if (_planRock != null)
        {
            float stopDistance = Mathf.Max(nearestT - _planRock.FootprintRadius - bossBodyRadius, 0.5f);
            end = start + pathDir * stopDistance;
        }

        _planEnd = end;
        _targetPlatformName = _planRock != null ? _planRock.name + " (바위)" : platforms[best].name;
        UpdateCrashWarning();
    }

    // start에서 dir 방향 length 거리 안의 레인(폭 laneWidth)에 걸린 솟은 바위 중 가장 가까운 것을 찾는다.
    // nearestT에는 그 바위까지의 진행 방향 거리를 돌려준다. 없으면 null.
    private IslandRock FindRockOnPath(Vector3 start, Vector3 dir, float length, out float nearestT)
    {
        nearestT = float.MaxValue;
        IslandRock found = null;

        foreach (IslandRock rock in rocks)
        {
            if (rock == null || !rock.IsRisen || rock.IsBroken) continue;

            Vector3 offset = rock.FootprintCenter - start;
            float t = Vector3.Dot(offset, dir);
            if (t <= 0f || t > length) continue;

            float side = Mathf.Abs(offset.x * dir.z - offset.z * dir.x);
            if (side > laneWidth * 0.5f + rock.FootprintRadius) continue;

            if (t < nearestT)
            {
                nearestT = t;
                found = rock;
            }
        }
        return found;
    }

    // 계획이 바위 충돌이면 레인/원을 경고색으로, 바위를 떨림으로 바꾼다. 상태가 바뀔 때만 적용한다.
    private void UpdateCrashWarning()
    {
        if (_warningRock == _planRock) return;

        if (_warningRock != null) _warningRock.SetWarning(false);
        _warningRock = _planRock;

        if (_warningRock != null)
        {
            _warningRock.SetWarning(true);
            laneTelegraph.SetWarningColor(crashLaneColor);
            circleTelegraph.SetWarningColor(crashLaneColor);
        }
        else
        {
            laneTelegraph.ClearWarningColor();
            circleTelegraph.ClearWarningColor();
        }
    }

    // 경고 연출을 모두 끈다(중단, 사이클 종료, 충돌 시).
    private void ClearCrashWarning()
    {
        if (_warningRock != null) _warningRock.SetWarning(false);
        _warningRock = null;
        if (laneTelegraph != null) laneTelegraph.ClearWarningColor();
        if (circleTelegraph != null) circleTelegraph.ClearWarningColor();
    }

    private void ShowPlan()
    {
        Vector3 start = new Vector3(_rb.position.x, 0f, _rb.position.z);
        laneTelegraph.ShowLane(start, _planEnd, laneWidth);
        circleTelegraph.ShowCircle(_planEnd, circleRadius);
    }

    // 돌진: 계산된 도착 지점까지 약간의 포물선을 그리며 이동한다. 이동 중에만 접촉 히트박스를 켠다.
    private IEnumerator DashRoutine()
    {
        _currentPhase = "돌진";
        laneTelegraph.Hide();
        circleTelegraph.Hide();

        Vector3 start = _rb.position;
        Vector3 end = new Vector3(_planEnd.x, standHeightY, _planEnd.z);

        if (dashHitbox != null) dashHitbox.EnableHitbox();

        // 속도 기준: 멀리 갈수록 오래 걸려 항상 같은 속도로 보인다. 너무 가까우면 최소 시간을 보장한다.
        float flatDistance = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(end.x, 0f, end.z));
        float dashDuration = Mathf.Max(flatDistance / Mathf.Max(dashSpeed, 0.1f), minDashDuration);

        float elapsed = 0f;
        while (_boss.IsInvincible && elapsed < dashDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / dashDuration);
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * dashArcHeight;
            _rb.MovePosition(pos);
            yield return new WaitForFixedUpdate();
        }

        if (dashHitbox != null) dashHitbox.DisableHitbox();
        _rb.MovePosition(end);
    }

    // 바위를 들이받아 파훼한다: 바위는 부서지고 나머지 바위는 가라앉으며 카메라가 흔들린다.
    private void CrashIntoRock()
    {
        _currentPhase = "바위 충돌";
        _planRock.Break();
        ClearCrashWarning();
        foreach (IslandRock rock in rocks)
        {
            if (rock != null && rock != _planRock) rock.Sink();
        }

        if (sideViewCamera != null) sideViewCamera.Shake(crashShakeDuration, crashShakeStrength);

        _activeRock = null; // 부서진 바위는 IsBroken이라 다음 추첨에서 자동으로 빠진다.
        _planRock = null;
        _boss.NotifyPatternSolved();
    }

    // 발판 착지: 쿵 연출 + 파도 생성 + 잠깐 멈춤. 다음 돌진은 이 발판에서 시작한다.
    private IEnumerator LandRoutine()
    {
        _currentPhase = "착지";
        _currentPlatformIndex = _planPlatformIndex;

        if (audioSource != null && landingClip != null) audioSource.PlayOneShot(landingClip);
        if (sideViewCamera != null) sideViewCamera.Shake(landingShakeDuration, landingShakeStrength);

        if (bossVisual != null)
        {
            if (_punchTween != null) _punchTween.Kill();
            _punchTween = bossVisual.DOPunchScale(Vector3.one * landingPunchStrength, 0.4f, 8, 0.8f);
        }

        SpawnWave(platforms[_currentPlatformIndex].position);

        yield return new WaitForSeconds(landingPause);
    }

    // 착지한 발판에서 섬 중앙 방향으로 진행하는 파도를 만든다.
    private void SpawnWave(Vector3 platformPosition)
    {
        if (wavePrefab == null) return;

        Vector3 centerFlat = GetCenterPosition();
        Vector3 dir = GetFlatDirection(platformPosition, centerFlat);
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector3 spawn = new Vector3(platformPosition.x, 0f, platformPosition.z) + dir * waveSpawnInset;
        BossSpringWave wave = Instantiate(wavePrefab);
        wave.Initialize(spawn, dir, playerController, rocks, GetDistanceToIslandEdge(spawn, dir) + waveEdgeMargin);

        _waves.RemoveAll(w => w == null);
        _waves.Add(wave);
    }

    // spawn에서 dir 방향으로 쏜 광선이 섬 타원과 만나는 "먼 쪽" 교점까지의 거리다.
    // 타원 (x/a)^2 + (z/b)^2 = 1 에 광선 p + t*d 를 넣으면 t에 대한 2차방정식이 된다.
    private float GetDistanceToIslandEdge(Vector3 spawn, Vector3 dir)
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        float px = (spawn.x - c.x) / islandSemiAxisX;
        float pz = (spawn.z - c.z) / islandSemiAxisZ;
        float dx = dir.x / islandSemiAxisX;
        float dz = dir.z / islandSemiAxisZ;

        float a = dx * dx + dz * dz;
        float b = 2f * (px * dx + pz * dz);
        float cc = px * px + pz * pz - 1f;
        float disc = b * b - 4f * a * cc;
        if (a < 0.0001f || disc < 0f) return 0f; // 섬을 지나지 않으면 여유 거리만큼만 간다.

        float far = (-b + Mathf.Sqrt(disc)) / (2f * a);
        return Mathf.Max(far, 0f);
    }

    // 바위 하나 솟아오름: 남은 바위 중 랜덤 1개 -> 예고 원 -> 겹친 플레이어 밀어내기 -> Rise().
    // exclude는 방금 실패해서 들어간 바위다. 같은 바위가 연속으로 나오지 않도록 후보에서 뺀다.
    private IEnumerator RiseRandomRockRoutine(IslandRock exclude)
    {
        IslandRock rock = PickRandomRock(exclude);
        if (rock == null) yield break; // 솟을 수 있는 바위가 없다(전부 부서짐 등).

        _currentPhase = "바위 솟음 예고";

        ClearRockTelegraphs();
        GroundTelegraph t = Instantiate(rockTelegraphPrefab);
        t.ShowCircle(rock.FootprintCenter, rock.FootprintRadius + rockTelegraphMargin);
        _rockTelegraphs.Add(t);

        yield return new WaitForSeconds(rockTelegraphDuration);

        PushPlayerOutOf(rock);
        rock.Rise();
        ClearRockTelegraphs();

        _activeRock = rock;
        _lureChancesLeft = rockLureChances;

        yield return new WaitForSeconds(postRockRisePause);
    }

    // 유인 실패: 지금 바위를 땅속으로 돌려보내고, 잠깐 쉰 뒤 다른 바위를 올린다.
    private IEnumerator SwapRockRoutine()
    {
        _currentPhase = "바위 교체";
        IslandRock previous = _activeRock;
        _activeRock = null;
        if (previous != null) previous.Sink();

        yield return new WaitForSeconds(rockSwapPause);
        yield return RiseRandomRockRoutine(previous);
    }

    // 부서지지 않고 숨어 있는 바위 중 하나를 고른다. exclude 말고 후보가 없으면 exclude라도 다시 쓴다.
    private IslandRock PickRandomRock(IslandRock exclude)
    {
        List<IslandRock> candidates = new List<IslandRock>();
        foreach (IslandRock rock in rocks)
        {
            if (rock == null || rock.IsBroken || rock.IsRisen || rock == exclude) continue;
            candidates.Add(rock);
        }

        if (candidates.Count == 0)
            return exclude != null && !exclude.IsBroken ? exclude : null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    // 바위 바닥 면적 안에 플레이어가 있으면 바깥쪽으로 한 번 밀어낸다.
    private void PushPlayerOutOf(IslandRock rock)
    {
        if (playerController == null) return;

        Vector3 offset = playerController.transform.position - rock.FootprintCenter;
        offset.y = 0f;
        float limit = rock.FootprintRadius + rockTelegraphMargin;
        if (offset.magnitude > limit) return;

        Vector3 dir = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.right;
        playerController.SetBlast(dir * rockPushSpeed);
    }

    // from -> to 의 XZ 평면 단위 방향이다(Y 무시). 거리가 0이면 zero를 돌려준다.
    private static Vector3 GetFlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 d = new Vector3(to.x - from.x, 0f, to.z - from.z);
        return d.sqrMagnitude < 0.0001f ? Vector3.zero : d.normalized;
    }

    private Vector3 GetCenterPosition()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        return new Vector3(c.x, standHeightY, c.z);
    }

    // 시작 위치에서 목표까지 포물선으로 이동한다(중앙 이동 연출용).
    private IEnumerator MoveArcRoutine(Vector3 target, float duration, float arcHeight)
    {
        Vector3 start = _rb.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            _rb.MovePosition(pos);
            yield return new WaitForFixedUpdate();
        }
        _rb.MovePosition(target);
    }

    private void RestorePhysics()
    {
        if (_rb == null) return;
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }

    private void HideTelegraphsImmediate()
    {
        if (laneTelegraph != null) laneTelegraph.HideImmediate();
        if (circleTelegraph != null) circleTelegraph.HideImmediate();
    }

    private void ClearRockTelegraphs()
    {
        foreach (GroundTelegraph t in _rockTelegraphs)
        {
            if (t != null) Destroy(t.gameObject);
        }
        _rockTelegraphs.Clear();
    }

    private void KillTweens()
    {
        if (_spinTween != null) _spinTween.Kill();
        if (_punchTween != null) _punchTween.Kill();
    }

    [Button("랜덤 바위 즉시 솟기 (테스트)")]
    private void TestRiseRocks()
    {
        if (_activeRock != null) _activeRock.Sink();
        _activeRock = PickRandomRock(_activeRock);
        if (_activeRock == null) return;
        _activeRock.Rise();
        _lureChancesLeft = rockLureChances;
    }

    [Button("바위 가라앉히기 (테스트)")]
    private void TestSinkRocks()
    {
        foreach (IslandRock rock in rocks)
        {
            if (rock != null) rock.Sink();
        }
        _activeRock = null;
    }

    [Button("현재 위치에서 파도 생성 (테스트)")]
    private void TestSpawnWave()
    {
        if (platforms == null || platforms.Length == 0) return;
        int index = Mathf.Clamp(_currentPlatformIndex, 0, platforms.Length - 1);
        SpawnWave(platforms[index].position);
    }
}
