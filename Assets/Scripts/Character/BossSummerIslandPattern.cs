using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 여름 패턴(피뢰침 발판) 중 "탑다운 원형섬 아레나" 전용 버전이다.
// 기존 사이드뷰용 여름 패턴은 건드리지 않고, 아레나 씬에서는 이 컴포넌트만 켠다.
//
// 흐름(전투 중 딱 한 번만 진행한다): 섬 중앙 상공 이동 -> 피뢰침 솟음 -> (번개 기둥 견제 + 낙뢰 N번)을 피뢰침이 전부 충전될 때까지 반복
//   -> 전류가 보스에게 모여 감전, 추락해 탈진.
//  - 플레이어는 번개 예고 원을 피하면서, 번개가 피뢰침 근처에 떨어지도록 유도해야 한다.
//  - 낙뢰와 함께 번개 기둥이 훑고 지나가거나(홀수 회차: 흐르는 기둥) 플레이어를 둘러쌌다 퍼진다(짝수 회차: 기둥 포위).
//    기둥은 닿으면 피해만 주고 통과할 수 있는 견제용이다.
//
// 보스 이동은 BossWinterIslandPattern과 같은 예외로 Rigidbody를 Kinematic으로 바꿔 MovePosition으로 옮긴다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossSummerIslandPattern : BossSeasonPatternBase
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 중앙")]
    private Transform centerPoint;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("피뢰침 목록")]
    private LightningRod[] rods;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발판 목록 (14개)")]
    private Transform[] platforms;
    // 피뢰침이 놓일 후보 발판이다. 봄 패턴(BossSpringIslandPattern)과 같은 발판들을 연결한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("원 예고 프리팹")]
    private GroundTelegraph circleTelegraphPrefab;
    // 피뢰침 솟음 예고와 낙뢰 예고에 쓴다. 사용 후 파괴한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("번개 선 프리팹")]
    private LightningBoltVisual boltPrefab;
    // 낙뢰와 마지막 전류 연출에 쓴다. 끝나면 스스로 파괴된다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("번개 기둥 프리팹")]
    private LightningPillar pillarPrefab;
    // 견제 패턴에서 기둥 하나마다 생성한다. 사라질 때 스스로 파괴된다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;
    // 쓰러짐 연출을 건다. 루트(Rigidbody)가 아니라 자식에 걸어 물리와 충돌하지 않게 한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("천둥 AudioSource (선택)")]
    private AudioSource audioSource;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("천둥 소리 (선택)")]
    private AudioClip thunderClip;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("천둥 볼륨"), Range(0f, 1f)]
    private float thunderVolume = 0.3f;
    // 천둥 원본 소리가 다른 효과음보다 훨씬 커서 낙뢰마다 줄여서 재생한다. 클수록 크게 들린다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("기둥 지지직 소리 (선택)")]
    private AudioClip pillarSoundClip;
    // 기둥 무리(흐르는 줄 묶음 하나, 포위 원 하나)마다 하나씩 반복 재생한다.
    // 기둥마다 붙이면 수십 개가 겹쳐 시끄러워지므로 무리 단위로 하나만 쓴다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("기둥 소리 볼륨"), Range(0f, 1f)]
    private float pillarSoundVolume = 0.25f;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("기둥 소리 들리는 최대 거리")]
    private float pillarSoundMaxDistance = 25f;
    // 3D 소리라 기둥 무리에서 이 거리보다 멀면 들리지 않는다. 가까울수록 크게 들린다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("기둥 소리 페이드 시간")]
    private float pillarSoundFadeDuration = 0.3f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("보스 떠 있는 높이(중심 Y)")]
    private float hoverHeightY = 3.5f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("중앙 이동 시간")]
    private float entryDuration = 1f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("피뢰침 예고 시간")]
    private float rodTelegraphDuration = 0.8f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("피뢰침 예고 반지름")]
    private float rodTelegraphRadius = 1.2f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("피뢰침 떨어지는 간격")]
    private float rodDropInterval = 0.6f;
    // 피뢰침이 한 개씩 차례로 떨어지는 시간 간격(초)이다. 꽂히는 소리가 겹치지 않고 하나씩 들리게 한다.
    // 0이면 예전처럼 한꺼번에 떨어진다.

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("피뢰침 사이 최소 거리")]
    private float rodMinSpacing = 8f;
    // 피뢰침끼리 수평으로 이만큼은 떨어지게 발판을 고른다. 클수록 섬 곳곳에 흩어진다.

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("피뢰침 바깥 오프셋")]
    private float rodOutwardOffset = 0.7f;
    // 발판 중심에서 섬 중앙 반대쪽(바깥)으로 이만큼 비켜 세운다.

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;
    // 발이 이보다 낮으면 물에 빠진 것으로 보고 낙뢰 피해를 주지 않는다.

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("회차당 낙뢰 횟수")]
    private int strikesPerRound = 3;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("낙뢰 반지름")]
    private float strikeRadius = 1.6f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("플레이어 추적 시간")]
    private float followDuration = 1.2f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("고정 후 대기 시간")]
    private float lockDuration = 0.6f;
    // 예고가 고정된 뒤 번개가 떨어지기까지의 시간이다. 짧을수록 피하기 어렵다.

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("낙뢰 간 간격")]
    private float strikeInterval = 0.4f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("낙뢰 피해량")]
    private float strikeDamage = 10f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("피뢰침 충전 반경")]
    private float rodChargeRadius = 2.5f;
    // 낙뢰 지점과 피뢰침의 수평 거리가 이 값 이하이면 충전된다.

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("번개 시작 높이")]
    private float boltHeight = 15f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("낙뢰 카메라 흔들림 시간")]
    private float strikeShakeDuration = 0.25f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("낙뢰 카메라 흔들림 세기")]
    private float strikeShakeStrength = 0.25f;

    [TabGroup("설정", "낙뢰")]
    [SerializeField, LabelText("회차 사이 쉬는 시간")]
    private float restBetweenRounds = 1.5f;

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("기둥 피해량")]
    private float pillarDamage = 10f;
    // 기둥에 닿았을 때 받는 피해다. 같은 기둥에는 한 번만 맞는다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("기둥 판정 반경")]
    private float pillarHitRadius = 0.75f;
    // 클수록 기둥 옆을 스치기만 해도 맞는다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("기둥 예고 반지름")]
    private float pillarTelegraphRadius = 0.9f;
    // 기둥이 솟을 자리에 보이는 원 예고의 크기다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("기둥 예고 시간")]
    private float pillarTelegraphDuration = 0.8f;
    // 짧을수록 기둥 위치를 확인하고 피하기 어렵다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 줄 수")]
    private int flowRowCount = 2;
    // 한 번에 흐르는 기둥 줄 개수다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 줄 간격")]
    private float flowRowSpacing = 11f;
    // 줄과 줄 사이 거리(m)다. 흐름 속도 4m/s 기준 약 2.7초 간격으로 줄이 지나간다. 좁을수록 통과 타이밍이 빡빡하다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 섬 반지름 (X, Z)")]
    private Vector2 islandHalfSize = new Vector2(13.5f, 9f);
    // 원형 섬(타원)의 가로/세로 반지름(m)이다. 틈이 섬 밖(물 위)에만 생기지 않도록 틈 위치를 이 안으로 제한할 때 쓴다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 틈이 생기는 섬 안쪽 비율"), Range(0.1f, 1f)]
    private float flowGapInsideRatio = 0.6f;
    // 틈 전체가 섬 반지름의 이 비율 안에 들어오게 고른다. 작을수록 틈이 섬 가운데 쪽에 생긴다.
    // 줄이 섬 가장자리를 지날 때도 섬 위에서 빠져나갈 수 있도록 1보다 작게 둔다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 줄 반폭")]
    private float flowHalfWidth = 16f;
    // 중앙에서 좌우로 기둥이 깔리는 범위(m)다. 섬 지름보다 크게 잡아야 옆으로 우회할 수 없다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 기둥 간격")]
    private float flowPillarSpacing = 1.4f;
    // 한 줄 안에서 기둥 사이 거리(m)다. '판정 반경 x 2'보다 작아야 기둥 사이로 못 빠져나가는 벽이 된다.
    // 이보다 넓히면 아무 기둥 사이로나 지나갈 수 있어 틈의 의미가 사라진다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 틈 개수")]
    private int flowGapCount = 2;
    // 줄마다 이어서 비워 두는 칸 수다(맨 바깥 칸 제외). 2칸이면 틈 폭이 약 2.7m다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[흐름] 이동 속도")]
    private float flowSpeed = 4f;
    // 기둥이 흐르는 속도(m/s)다. 빠를수록 피하기 어렵다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 기둥 개수")]
    private int ringPillarCount = 24;
    // 원을 이루는 기둥 수다. 시작 반지름에서 기둥 간격이 '판정 반경 x 2'보다 좁아야 벽이 된다(반지름 5m면 24개 정도).

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 틈 개수")]
    private int ringGapCount = 3;
    // 원에서 이어서 비워 두는 칸 수다. 틈 각도는 회차 내내 같다. 모일수록 틈이 좁아지니 일찍 빠져나가야 한다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 시작 반지름")]
    private float ringStartRadius = 5f;
    // 예고와 등장 시 원의 반지름(m)이다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 최소 반지름")]
    private float ringMinRadius = 0.3f;
    // 모였을 때 반지름이다. 작을수록 중앙(플레이어 자리)까지 좁혀진다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 최대 반지름")]
    private float ringEndRadius = 14f;
    // 퍼져 나간 뒤 사라지는 반지름(m)이다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 모이는 시간")]
    private float ringGatherDuration = 2f;
    // 시작 반지름에서 최소 반지름까지 걸리는 시간이다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 유지 시간")]
    private float ringHoldDuration = 0.2f;
    // 모인 뒤 퍼지기 전 멈춰 있는 시간이다.

    [TabGroup("설정", "견제")]
    [SerializeField, LabelText("[포위] 퍼지는 시간")]
    private float ringSpreadDuration = 2f;
    // 최소 반지름에서 최대 반지름까지 걸리는 시간이다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("전류 모이는 시간")]
    private float currentDuration = 0.5f;
    // 피뢰침에서 보스로 전류가 흐르는 모습을 보여 주고 추락하기까지의 대기 시간이다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("추락 후 서는 높이(중심 Y)")]
    private float groundStandY = 1f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("추락 시간")]
    private float fallDuration = 0.5f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러지는 회전값")]
    private Vector3 collapseEuler = new Vector3(0f, 0f, 90f);
    // 임시 메시를 옆으로 눕히는 값이다. 모델과 애니메이션이 들어오면 이 연출을 교체한다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("기절 시간")]
    private float stunDuration = 4f;
    // 이 시간 동안 보스는 무적이 풀려 공격받을 수 있다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("추락 카메라 흔들림 시간")]
    private float collapseShakeDuration = 0.4f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("추락 카메라 흔들림 세기")]
    private float collapseShakeStrength = 0.5f;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("피뢰침 발판")]
    private string _rodPlatformNames = "-";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("충전된 피뢰침 수")]
    private int _chargedCount;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 회차")]
    private int _roundNumber;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 견제 종류")]
    private string _harassmentType = "-";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("살아 있는 기둥 수")]
    private int LivePillarCount
    {
        get
        {
            int n = 0;
            foreach (LightningPillar pillar in _pillars)
            {
                if (pillar != null) n++;
            }
            return n;
        }
    }

    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private Coroutine _recoverRoutine;
    private Sequence _poseSequence;
    private Vector3 _visualStartLocalPosition;
    private Quaternion _visualStartLocalRotation;
    private Vector3 _previousFlowDirection;
    private Coroutine _harassRoutine;
    private bool _roundActive;
    private GroundTelegraph _strikeTelegraph;
    private readonly List<GroundTelegraph> _rodTelegraphs = new List<GroundTelegraph>();
    private readonly List<GroundTelegraph> _pillarTelegraphs = new List<GroundTelegraph>();
    private readonly List<LightningPillar> _pillars = new List<LightningPillar>();
    private readonly List<AudioSource> _pillarSounds = new List<AudioSource>();
    // 기둥 무리마다 만든 지지직 소리다. 중단/파훼 때 남지 않도록 모아서 정리한다.

    // 수평 4방향이다. 흐르는 기둥 회차마다 이 중 하나를 고른다.
    private static readonly Vector3[] FlowDirections = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

    protected override Boss.SeasonPattern Season => Boss.SeasonPattern.Summer;
    protected override bool IsRunning => _cycleRoutine != null;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();

        if (bossVisual != null)
        {
            _visualStartLocalPosition = bossVisual.localPosition;
            _visualStartLocalRotation = bossVisual.localRotation;
        }
    }

    // 탈진 대기 중(IsRunning이 false인 동안)에 꺼져도 피뢰침과 물리를 정리하기 위해 덧붙인다.
    protected override void OnDisable()
    {
        base.OnDisable();
        if (_recoverRoutine != null) Abort();
    }

    private void OnDestroy()
    {
        _poseSequence?.Kill();
    }

    protected override void StartCycle()
    {
        _cycleRoutine = StartCoroutine(SummerCycleRoutine());
    }

    // 패턴이 밀려나거나 오브젝트가 꺼질 때 호출된다. 견제 기둥과 표시를 모두 거두고 물리를 되돌린다.
    protected override void Abort()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }
        if (_recoverRoutine != null)
        {
            StopCoroutine(_recoverRoutine);
            _recoverRoutine = null;
        }

        _roundActive = false;
        StopHarassment(true);
        CleanupVisuals();
        ResetRods(true);
        ResetVisualPose();
        RestorePhysics();
        _currentPhase = "대기";
        _chargedCount = 0;
        _roundNumber = 0;
        _harassmentType = "-";
    }

    private IEnumerator SummerCycleRoutine()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true; // 패턴 동안 지형에 막히거나 떨어지지 않도록 고정한다.

        _chargedCount = 0;
        _roundNumber = 0;
        _previousFlowDirection = Vector3.zero;

        _currentPhase = "중앙 이동";
        yield return MoveBossRoutine(GetHoverPosition(), entryDuration);

        _currentPhase = "피뢰침 솟음";
        PlaceRodsOnRandomPlatforms(); // 예고 원이 새 위치에 뜨도록 예고 전에 옮긴다.
        yield return RiseRodsRoutine();

        // 피뢰침이 전부 충전될 때까지 회차를 반복한다.
        while (CountCharged() < CountRisen())
        {
            _roundNumber++;
            _currentPhase = "낙뢰 회차";
            yield return RoundRoutine();

            if (CountCharged() >= CountRisen()) break;

            _currentPhase = "숨 고르기";
            yield return new WaitForSeconds(restBetweenRounds);
        }

        // 탈진 연출 중에는 코루틴이 끝나도 되도록 먼저 참조를 비운다(겨울 패턴과 같은 방식).
        _currentPhase = "감전";
        _cycleRoutine = null;
        _recoverRoutine = StartCoroutine(SolvedRoutine());
    }

    // 매 사이클 피뢰침을 서로 멀리 떨어진 무작위 발판 위로 옮긴다. 같은 자리만 외워서 풀지 못하게 하기 위해서다.
    // 이 시점에는 피뢰침이 땅속에 숨어 있으므로 루트를 옮겨도 눈에 띄지 않는다.
    private void PlaceRodsOnRandomPlatforms()
    {
        if (rods == null || platforms == null || platforms.Length < rods.Length) return;

        int rodCount = rods.Length;
        int[] best = null;
        float bestMinDist = -1f;

        // 조건(모든 쌍이 최소 거리 이상)을 만족하는 조합을 무작위로 찾는다. 못 찾으면 최소 쌍 거리가 가장 큰 시도를 쓴다.
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int[] pick = PickDistinctIndices(platforms.Length, rodCount);
            float minDist = MinPairDistance(pick);
            if (minDist > bestMinDist)
            {
                bestMinDist = minDist;
                best = pick;
            }
            if (minDist >= rodMinSpacing) break;
        }
        if (best == null) return;

        Vector3 center = GetCenterFlat();
        System.Text.StringBuilder names = new System.Text.StringBuilder();
        for (int i = 0; i < rodCount; i++)
        {
            Transform platform = platforms[best[i]];
            names.Append(i > 0 ? ", " : "").Append(platform.name);
            if (rods[i] == null || rods[i].IsRisen) continue; // 솟아 있는 피뢰침은 옮기지 않는다.

            Vector3 outward = platform.position - center;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.zero;
            Vector3 flat = new Vector3(platform.position.x, 0f, platform.position.z);
            rods[i].transform.position = flat + outward * rodOutwardOffset;
        }
        _rodPlatformNames = names.ToString();
    }

    private static int[] PickDistinctIndices(int total, int count)
    {
        List<int> pool = new List<int>(total);
        for (int i = 0; i < total; i++) pool.Add(i);
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int k = Random.Range(0, pool.Count);
            result[i] = pool[k];
            pool.RemoveAt(k);
        }
        return result;
    }

    // 고른 발판들 사이 수평 거리 중 가장 짧은 값이다.
    private float MinPairDistance(int[] indices)
    {
        float min = float.MaxValue;
        for (int a = 0; a < indices.Length; a++)
        {
            for (int b = a + 1; b < indices.Length; b++)
            {
                Vector3 d = platforms[indices[a]].position - platforms[indices[b]].position;
                d.y = 0f;
                min = Mathf.Min(min, d.magnitude);
            }
        }
        return min;
    }

    // 피뢰침 위치에 원 예고를 띄운 뒤 한꺼번에 솟게 한다.
    private IEnumerator RiseRodsRoutine()
    {
        ClearRodTelegraphs();
        foreach (LightningRod rod in rods)
        {
            if (rod == null || rod.IsRisen || circleTelegraphPrefab == null) continue;

            GroundTelegraph telegraph = Instantiate(circleTelegraphPrefab);
            telegraph.ShowCircle(rod.FootprintCenter, rodTelegraphRadius);
            _rodTelegraphs.Add(telegraph);
        }

        yield return new WaitForSeconds(rodTelegraphDuration);

        // 한 개씩 차례로 떨어뜨린다. 떨어진 피뢰침의 예고 원은 바로 거둬, 남은 원이 "다음에 떨어질 자리"로 보이게 한다.
        for (int i = 0; i < rods.Length; i++)
        {
            LightningRod rod = rods[i];
            if (rod == null || rod.IsRisen) continue;

            rod.Rise();
            RemoveRodTelegraphAt(rod.FootprintCenter);

            bool isLast = i == rods.Length - 1;
            if (!isLast && rodDropInterval > 0f) yield return new WaitForSeconds(rodDropInterval);
        }
        ClearRodTelegraphs();
    }

    // 해당 위치에 띄운 피뢰침 예고 원 하나를 지운다.
    private void RemoveRodTelegraphAt(Vector3 center)
    {
        for (int i = _rodTelegraphs.Count - 1; i >= 0; i--)
        {
            GroundTelegraph telegraph = _rodTelegraphs[i];
            if (telegraph == null) { _rodTelegraphs.RemoveAt(i); continue; }

            Vector3 offset = telegraph.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.01f) continue;

            Destroy(telegraph.gameObject);
            _rodTelegraphs.RemoveAt(i);
            return;
        }
    }

    // 한 회차: 견제 기둥(코루틴)을 병렬로 돌리면서 낙뢰를 정해진 횟수만큼 떨어뜨린다.
    // 낙뢰와 견제가 모두 끝나야 회차가 끝난다. 피뢰침이 다 충전되면 견제를 즉시 멈추고 끝낸다.
    private IEnumerator RoundRoutine()
    {
        _roundActive = true;

        bool isFlowRound = _roundNumber % 2 == 1; // 홀수 회차 = 흐르는 기둥, 짝수 회차 = 기둥 포위
        _harassmentType = isFlowRound ? "흐르는 기둥" : "기둥 포위";
        _harassRoutine = StartCoroutine(isFlowRound ? FlowPillarsRoutine() : RingPillarsRoutine());

        for (int i = 0; i < strikesPerRound; i++)
        {
            yield return StrikeRoutine();

            if (CountCharged() >= CountRisen()) break;
            if (i < strikesPerRound - 1) yield return new WaitForSeconds(strikeInterval);
        }

        // 낙뢰가 먼저 끝났다면 견제가 끝날 때까지 기다린다(전부 충전되면 기다릴 필요 없다).
        while (_harassRoutine != null && CountCharged() < CountRisen())
            yield return null;

        _roundActive = false;
        StopHarassment(false);
    }

    // 견제 코루틴을 멈추고 기둥/예고를 정리한다. immediate면 페이드 없이 즉시 파괴한다(Abort용).
    private void StopHarassment(bool immediate)
    {
        if (_harassRoutine != null)
        {
            StopCoroutine(_harassRoutine);
            _harassRoutine = null;
        }

        ClearPillarTelegraphs();
        foreach (LightningPillar pillar in _pillars)
        {
            if (pillar == null) continue;
            if (immediate) Destroy(pillar.gameObject);
            else pillar.Vanish();
        }
        _pillars.Clear();

        foreach (AudioSource sound in _pillarSounds)
        {
            if (sound == null) continue;
            if (immediate) Destroy(sound.gameObject);
            else FadeOutPillarSound(sound);
        }
        _pillarSounds.Clear();
        _harassmentType = "-";
    }

    // 기둥 무리를 따라다닐 지지직 반복 소리를 만든다. 3D 소리라 무리가 가까이 올수록 크게 들린다.
    private AudioSource CreatePillarSound(Vector3 position)
    {
        if (pillarSoundClip == null) return null;

        GameObject go = new GameObject("PillarCrackleSound");
        go.transform.position = position + Vector3.up;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = pillarSoundClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = pillarSoundMaxDistance;
        source.dopplerLevel = 0f; // 기둥이 움직여도 음 높이가 변하지 않게 한다.
        if (audioSource != null) source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup; // 효과음 볼륨 설정을 따르게 한다.
        source.volume = 0f;
        source.Play();

        // 기둥이 나타날 때 소리도 서서히 커진다.
        source.DOFade(pillarSoundVolume, pillarSoundFadeDuration).SetLink(go);
        _pillarSounds.Add(source);
        return source;
    }

    private void MovePillarSound(AudioSource source, Vector3 position)
    {
        if (source != null) source.transform.position = position + Vector3.up;
    }

    // 기둥이 사라질 때 소리도 서서히 줄인 뒤 지운다.
    private void FadeOutPillarSound(AudioSource source)
    {
        if (source == null) return;
        _pillarSounds.Remove(source);
        DOTween.Kill(source);
        source.DOFade(0f, pillarSoundFadeDuration)
            .SetLink(source.gameObject)
            .OnComplete(() => { if (source != null) Destroy(source.gameObject); });
    }

    // 흐르는 기둥: 줄 지은 기둥이 섬 한쪽 테두리에서 솟아 반대쪽 테두리로 쓸고 지나간다. 줄마다 틈이 있어 그 사이로 피한다.
    // 기둥은 원형 섬(타원) 위에서만 보이고 피해를 준다. 섬 테두리에 들어오는 순간 솟고(Appear), 벗어나면 사라진다(Vanish).
    private IEnumerator FlowPillarsRoutine()
    {
        Vector3 direction = PickFlowDirection();
        _previousFlowDirection = direction;
        Vector3 side = Vector3.Cross(Vector3.up, direction); // 진행 방향에 수직인 줄 방향이다.
        Vector3 center = GetCenterFlat();

        int slotCount = Mathf.Max(3, Mathf.FloorToInt(flowHalfWidth * 2f / flowPillarSpacing) + 1);
        int rows = Mathf.Max(1, flowRowCount);

        // 줄 방향(side)으로 본 섬의 반지름이다. 가로로 늘어선 줄이면 X 반지름, 세로면 Z 반지름을 쓴다.
        float islandLateralHalf = Mathf.Abs(side.x) * islandHalfSize.x + Mathf.Abs(side.z) * islandHalfSize.y;
        // 진행 방향(direction)으로 본 섬의 반지름이다. 줄 가운데 기둥은 중앙에서 이만큼 떨어진 테두리에서 섬에 들어온다.
        float islandForwardHalf = Mathf.Abs(direction.x) * islandHalfSize.x + Mathf.Abs(direction.z) * islandHalfSize.y;
        float gapLimit = islandLateralHalf * flowGapInsideRatio;

        // 기둥별 옆 오프셋, 출발 지연 거리, 섬 안에 머무는 반길이(entryDepth)를 저장해 두고 매 프레임 위치를 다시 계산한다.
        List<Vector3> startPositions = new List<Vector3>();
        List<Vector3> entryPoints = new List<Vector3>(); // 기둥마다 섬에 들어오는 테두리 지점이다. 원 예고를 여기에 띄운다.
        List<float> laterals = new List<float>();
        List<float> rowBehind = new List<float>();
        List<float> entryDepths = new List<float>();
        for (int r = 0; r < rows; r++)
        {
            HashSet<int> gaps = PickFlowGapSlots(slotCount, flowGapCount, gapLimit);
            for (int i = 0; i < slotCount; i++)
            {
                if (gaps.Contains(i)) continue;
                float lateral = (i - (slotCount - 1) * 0.5f) * flowPillarSpacing;
                // 섬 옆 폭 밖의 기둥은 섬 위를 한 번도 지나지 않으므로 만들지 않는다.
                if (Mathf.Abs(lateral) >= islandLateralHalf) continue;

                // 타원 식 (옆/옆반지름)² + (앞/앞반지름)² = 1 에서, 이 옆 위치에서 섬이 앞뒤로 얼마나 이어지는지 구한다.
                // 가운데 기둥은 섬을 길게 지나고, 가장자리 기둥일수록 짧게 스치고 지나간다.
                float t = lateral / islandLateralHalf;
                float entryDepth = islandForwardHalf * Mathf.Sqrt(1f - t * t);
                float behind = r * flowRowSpacing; // 첫 줄이 맨 앞, 뒤 줄일수록 더 뒤에서 출발한다.

                laterals.Add(lateral);
                rowBehind.Add(behind);
                entryDepths.Add(entryDepth);
                startPositions.Add(center + side * lateral - direction * (islandForwardHalf + behind));
                entryPoints.Add(center + side * lateral - direction * entryDepth);
            }
        }

        // 원 예고는 기둥마다 하나씩, 그 기둥이 섬에 들어오기 '기둥 예고 시간'만큼 앞서 띄우고 기둥이 솟으면 지운다.
        // 예고 시간 동안 기둥이 움직이는 거리만큼 미리 띄우면 된다(속도 x 시간).
        float telegraphLeadDistance = flowSpeed * pillarTelegraphDuration;
        GroundTelegraph[] telegraphs = new GroundTelegraph[startPositions.Count];

        // 첫 줄은 출발 직후 바로 섬에 들어오므로, 출발 전에 예고를 띄우고 예고 시간만큼 기다린다.
        for (int i = 0; i < startPositions.Count; i++)
        {
            if (rowBehind[i] <= 0f) telegraphs[i] = ShowFlowTelegraph(entryPoints[i]);
        }
        yield return new WaitForSeconds(pillarTelegraphDuration);

        // 처음엔 보이지 않고 피해도 없는 상태로 만들어 두고, 섬에 들어올 때 솟게 한다.
        List<LightningPillar> group = SpawnPillars(startPositions, false);
        bool[] appeared = new bool[group.Count];
        bool[] gone = new bool[group.Count];

        // 소리는 줄 묶음의 가운데(앞줄과 뒷줄 사이)를 따라간다.
        float soundBehind = (rows - 1) * flowRowSpacing * 0.5f;
        AudioSource sound = CreatePillarSound(center - direction * (islandForwardHalf + soundBehind));

        // 맨 뒤 줄이 반대편 테두리를 벗어날 때까지 이동한다.
        float totalDistance = islandForwardHalf * 2f + (rows - 1) * flowRowSpacing;
        float travelled = 0f;
        while (travelled < totalDistance)
        {
            travelled += flowSpeed * Time.deltaTime; // 프레임 속도와 무관하게 같은 속도로 흐르게 한다.
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] == null || gone[i]) continue;
                // along: 중앙 기준 진행 방향 위치다. -entryDepth ~ +entryDepth 사이일 때만 섬 위에 있다.
                float along = -(islandForwardHalf + rowBehind[i]) + travelled;
                group[i].SetPosition(center + side * laterals[i] + direction * along);

                // 뒤 줄 기둥: 섬 테두리에 닿기 예고 거리만큼 앞에서 원 예고를 띄운다.
                if (!appeared[i] && telegraphs[i] == null && along >= -entryDepths[i] - telegraphLeadDistance)
                    telegraphs[i] = ShowFlowTelegraph(entryPoints[i]);

                if (!appeared[i] && along >= -entryDepths[i])
                {
                    appeared[i] = true;
                    RemoveFlowTelegraph(telegraphs[i]); // 기둥이 솟으면 그 자리 예고는 더 필요 없다.
                    telegraphs[i] = null;
                    group[i].Appear(); // 섬 테두리에 닿는 순간 솟아올라 위험 상태가 된다.
                }
                if (appeared[i] && along > entryDepths[i])
                {
                    gone[i] = true;
                    group[i].Vanish(); // 반대편 테두리를 벗어나면 사라진다(물 위로는 넘어가지 않는다).
                }
            }
            MovePillarSound(sound, center + direction * (-(islandForwardHalf + soundBehind) + travelled));
            yield return null;
        }

        // 기둥 프리팹이 비어 있는 등으로 남은 예고가 있으면 함께 지운다.
        foreach (GroundTelegraph telegraph in telegraphs) RemoveFlowTelegraph(telegraph);
        VanishGroup(group);
        FadeOutPillarSound(sound);
        _harassRoutine = null;
    }

    // 흐르는 기둥 하나의 섬 진입 지점에 원 예고를 띄운다.
    // _pillarTelegraphs에도 넣어 두어 패턴이 중간에 끊겨도 ClearPillarTelegraphs()가 함께 지우게 한다.
    private GroundTelegraph ShowFlowTelegraph(Vector3 pos)
    {
        if (circleTelegraphPrefab == null) return null;
        GroundTelegraph telegraph = Instantiate(circleTelegraphPrefab);
        telegraph.ShowCircle(pos, pillarTelegraphRadius);
        _pillarTelegraphs.Add(telegraph);
        return telegraph;
    }

    private void RemoveFlowTelegraph(GroundTelegraph telegraph)
    {
        if (telegraph == null) return;
        _pillarTelegraphs.Remove(telegraph);
        Destroy(telegraph.gameObject);
    }

    // 기둥 포위: 플레이어 주변에 원으로 솟아 한 점으로 모였다가 바깥으로 퍼진다. 틈 각도는 그대로다.
    private IEnumerator RingPillarsRoutine()
    {
        Vector3 center = GetPlayerFlatPosition(); // 시작 시점의 플레이어 위치를 중심으로 고정한다.
        int count = Mathf.Max(3, ringPillarCount);
        HashSet<int> gaps = PickGapSlots(count, ringGapCount, false);

        List<float> angles = new List<float>(); // 기둥별 각도(라디안)
        List<Vector3> startPositions = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            if (gaps.Contains(i)) continue;
            float angle = i * Mathf.PI * 2f / count;
            angles.Add(angle);
            startPositions.Add(RingPoint(center, angle, ringStartRadius));
        }

        yield return TelegraphRoutine(startPositions);
        List<LightningPillar> group = SpawnPillars(startPositions);
        AudioSource sound = CreatePillarSound(center); // 원은 중심이 움직이지 않으므로 소리도 중심에 둔다.

        // 모이기: t를 제곱해 처음엔 느리고 끝에서 빨라지는(InQuad) 느낌을 낸다.
        yield return MoveRing(group, angles, center, ringStartRadius, ringMinRadius, ringGatherDuration, true);
        yield return new WaitForSeconds(ringHoldDuration);
        // 퍼지기: 일정한 속도로 바깥으로 보낸다.
        yield return MoveRing(group, angles, center, ringMinRadius, ringEndRadius, ringSpreadDuration, false);

        VanishGroup(group);
        FadeOutPillarSound(sound);
        _harassRoutine = null;
    }

    private IEnumerator MoveRing(List<LightningPillar> group, List<float> angles, Vector3 center,
        float fromRadius, float toRadius, float duration, bool easeIn)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeIn ? t * t : t;
            float radius = Mathf.Lerp(fromRadius, toRadius, eased);
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] != null) group[i].SetPosition(RingPoint(center, angles[i], radius));
            }
            yield return null;
        }
    }

    private static Vector3 RingPoint(Vector3 center, float angle, float radius)
    {
        return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    // 기둥이 솟을 자리마다 원 예고를 띄우고 시간이 지나면 거둔다.
    private IEnumerator TelegraphRoutine(List<Vector3> positions)
    {
        if (circleTelegraphPrefab != null)
        {
            foreach (Vector3 pos in positions)
            {
                GroundTelegraph telegraph = Instantiate(circleTelegraphPrefab);
                telegraph.ShowCircle(pos, pillarTelegraphRadius);
                _pillarTelegraphs.Add(telegraph);
            }
        }

        yield return new WaitForSeconds(pillarTelegraphDuration);
        ClearPillarTelegraphs();
    }

    // positions와 같은 순서/개수의 리스트를 돌려준다(프리팹이 없으면 null 칸). 인덱스로 위치를 갱신하기 위해서다.
    // appearNow가 false면 보이지 않고 피해도 없는 상태로만 만들어 두고, 호출한 쪽이 원하는 순간에 Appear()를 부른다.
    private List<LightningPillar> SpawnPillars(List<Vector3> positions, bool appearNow = true)
    {
        List<LightningPillar> group = new List<LightningPillar>();
        foreach (Vector3 pos in positions)
        {
            if (pillarPrefab == null)
            {
                group.Add(null);
                continue;
            }
            LightningPillar pillar = Instantiate(pillarPrefab);
            pillar.Init(playerController, pillarDamage, pillarHitRadius);
            pillar.SetPosition(pos);
            if (appearNow) pillar.Appear();
            _pillars.Add(pillar);
            group.Add(pillar);
        }
        return group;
    }

    private void VanishGroup(List<LightningPillar> group)
    {
        foreach (LightningPillar pillar in group)
        {
            if (pillar != null) pillar.Vanish();
        }
        _pillars.RemoveAll(p => p == null || group.Contains(p));
    }

    // 틈 칸을 "이어 붙은" gapCount칸으로 고른다. 기둥이 촘촘한 벽이라, 칸을 흩어 빼면 사람이 지나갈 폭이 안 나오기 때문이다.
    // excludeEdges가 true면 맨 바깥 칸(0, 마지막)은 제외한다(옆으로 돌아 나가는 길 방지).
    // 원형(excludeEdges false)은 끝 칸과 첫 칸이 이어져 있으므로 인덱스를 한 바퀴 돌려 감는다.
    private static HashSet<int> PickGapSlots(int slotCount, int gapCount, bool excludeEdges)
    {
        HashSet<int> gaps = new HashSet<int>();
        int min = excludeEdges ? 1 : 0;
        int max = excludeEdges ? slotCount - 1 : slotCount; // 배타적 상한
        gapCount = Mathf.Clamp(gapCount, 0, max - min);
        if (gapCount == 0) return gaps;

        int start = excludeEdges ? Random.Range(min, max - gapCount + 1) : Random.Range(0, slotCount);
        for (int i = 0; i < gapCount; i++)
            gaps.Add(excludeEdges ? start + i : (start + i) % slotCount);
        return gaps;
    }

    // 흐르는 줄의 틈을 고른다. 틈 칸 전체가 섬 안쪽(중앙에서 limit 이내)에 들어오는 시작 칸 중에서 무작위로 고른다.
    // 그래야 섬 위에 서 있는 플레이어가 물로 뛰어들지 않고도 반드시 빠져나갈 길이 생긴다.
    private HashSet<int> PickFlowGapSlots(int slotCount, int gapCount, float limit)
    {
        gapCount = Mathf.Clamp(gapCount, 0, slotCount - 2);
        HashSet<int> gaps = new HashSet<int>();
        if (gapCount == 0) return gaps;

        List<int> validStarts = new List<int>();
        for (int start = 1; start + gapCount - 1 <= slotCount - 2; start++)
        {
            float first = SlotLateral(start, slotCount);
            float last = SlotLateral(start + gapCount - 1, slotCount);
            if (Mathf.Abs(first) <= limit && Mathf.Abs(last) <= limit) validStarts.Add(start);
        }

        // 섬이 너무 작아 조건에 맞는 칸이 없으면 가운데에 둔다.
        int chosen = validStarts.Count > 0
            ? validStarts[Random.Range(0, validStarts.Count)]
            : Mathf.Clamp((slotCount - gapCount) / 2, 1, slotCount - 1 - gapCount);

        for (int i = 0; i < gapCount; i++) gaps.Add(chosen + i);
        return gaps;
    }

    private float SlotLateral(int index, int slotCount)
    {
        return (index - (slotCount - 1) * 0.5f) * flowPillarSpacing;
    }

    // 이전 흐르는 기둥과 다른 방향을 무작위로 고른다. 같은 방향만 반복되는 정답 자리가 생기지 않게 한다.
    private Vector3 PickFlowDirection()
    {
        Vector3 pick;
        do
        {
            pick = FlowDirections[Random.Range(0, FlowDirections.Length)];
        }
        while (pick == _previousFlowDirection);
        return pick;
    }

    // 낙뢰 한 번: 예고가 플레이어를 따라가다 고정되고, 잠시 뒤 번개가 떨어진다.
    private IEnumerator StrikeRoutine()
    {
        if (circleTelegraphPrefab != null)
            _strikeTelegraph = Instantiate(circleTelegraphPrefab);

        Vector3 strikePoint = GetPlayerFlatPosition();

        float timer = 0f;
        while (timer < followDuration)
        {
            strikePoint = GetPlayerFlatPosition();
            if (_strikeTelegraph != null) _strikeTelegraph.ShowCircle(strikePoint, strikeRadius);
            timer += Time.deltaTime;
            yield return null;
        }

        strikePoint = GetPlayerFlatPosition();
        if (_strikeTelegraph != null)
        {
            _strikeTelegraph.ShowCircle(strikePoint, strikeRadius);
            _strikeTelegraph.Lock();
        }

        yield return new WaitForSeconds(lockDuration);

        ApplyStrike(strikePoint);

        if (_strikeTelegraph != null)
        {
            Destroy(_strikeTelegraph.gameObject);
            _strikeTelegraph = null;
        }
    }

    // 번개가 실제로 떨어지는 순간이다: 연출, 플레이어 피해, 피뢰침 충전을 처리한다.
    private void ApplyStrike(Vector3 strikePoint)
    {
        SpawnBolt(strikePoint + Vector3.up * boltHeight, strikePoint);
        if (audioSource != null && thunderClip != null) audioSource.PlayOneShot(thunderClip, thunderVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(strikeShakeDuration, strikeShakeStrength);

        if (playerController != null && !IsPlayerInWater())
        {
            Vector3 toPlayer = GetPlayerFlatPosition() - strikePoint;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= strikeRadius)
                PlayerHealth.Instance?.TakeDamage(strikeDamage);
        }

        foreach (LightningRod rod in rods)
        {
            if (rod == null || !rod.IsRisen || rod.IsCharged) continue;

            Vector3 toRod = rod.FootprintCenter - strikePoint;
            toRod.y = 0f;
            if (toRod.magnitude > rodChargeRadius) continue;

            rod.Charge();
            _chargedCount = CountCharged();
            SpawnBolt(strikePoint + Vector3.up * boltHeight, rod.TopPosition); // 피뢰침에 번개가 꽂히는 모습이다.
        }
    }

    // 피뢰침이 다 충전된 뒤: 전류가 보스에게 모이고, 보스가 추락해 기절한다. 끝나면 원래대로 복구한다.
    private IEnumerator SolvedRoutine()
    {
        _roundActive = false;
        StopHarassment(false);
        CleanupVisuals();

        foreach (LightningRod rod in rods)
        {
            if (rod != null && rod.IsCharged) SpawnBolt(rod.TopPosition, _rb.position);
        }
        if (audioSource != null && thunderClip != null) audioSource.PlayOneShot(thunderClip, thunderVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(currentDuration, strikeShakeStrength);

        yield return new WaitForSeconds(currentDuration);

        _currentPhase = "탈진";
        CollapseBoss();

        yield return new WaitForSeconds(stunDuration);

        _currentPhase = "복구";
        yield return StandUpRoutine();

        ResetRods(true);
        _currentPhase = "대기";
        _chargedCount = 0;
        _roundNumber = 0;
        _recoverRoutine = null;
    }

    // 높이는 시각 오브젝트가 아니라 루트로 낮춘다(겨울 패턴과 같은 이유: 피격 연출이 시각 오브젝트 위치를 되돌린다).
    private void CollapseBoss()
    {
        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(groundStandY, fallDuration).SetEase(Ease.InQuad))
            .SetLink(gameObject);

        if (bossVisual != null)
        {
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation * Quaternion.Euler(collapseEuler), fallDuration)
                .SetEase(Ease.InQuad));
        }

        if (sideViewCamera != null) sideViewCamera.Shake(collapseShakeDuration, collapseShakeStrength);

        _boss.NotifyPatternSolved(stunDuration);
    }

    // 기절이 끝나면 자세와 높이를 되돌리고 물리를 복구한다.
    private IEnumerator StandUpRoutine()
    {
        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(groundStandY, fallDuration).SetEase(Ease.OutQuad))
            .SetLink(gameObject);

        if (bossVisual != null)
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation, fallDuration).SetEase(Ease.OutQuad));

        yield return _poseSequence.WaitForCompletion();
        RestorePhysics();
    }

    // 보스를 목표 위치까지 옮긴다. Kinematic Rigidbody라 물리 주기에 맞춰 MovePosition을 쓴다.
    private IEnumerator MoveBossRoutine(Vector3 target, float duration)
    {
        Vector3 start = _rb.position;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _rb.MovePosition(Vector3.Lerp(start, target, t));
            yield return new WaitForFixedUpdate();
        }
        _rb.MovePosition(target);
        yield return new WaitForFixedUpdate();
    }

    private Vector3 GetHoverPosition()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        return new Vector3(c.x, hoverHeightY, c.z);
    }

    private Vector3 GetPlayerFlatPosition()
    {
        if (playerController == null) return Vector3.zero;
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

    private Vector3 GetCenterFlat()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        return new Vector3(c.x, 0f, c.z);
    }

    private void SpawnBolt(Vector3 from, Vector3 to)
    {
        if (boltPrefab == null) return;
        LightningBoltVisual bolt = Instantiate(boltPrefab);
        bolt.Play(from, to);
    }

    private int CountRisen()
    {
        int n = 0;
        foreach (LightningRod rod in rods)
        {
            if (rod != null && rod.IsRisen) n++;
        }
        return n;
    }

    private int CountCharged()
    {
        int n = 0;
        foreach (LightningRod rod in rods)
        {
            if (rod != null && rod.IsCharged) n++;
        }
        return n;
    }

    private void ResetRods(bool sink)
    {
        if (rods == null) return;
        foreach (LightningRod rod in rods)
        {
            if (rod == null) continue;
            rod.ResetRod();
            if (sink) rod.Sink();
        }
    }

    private void ResetVisualPose()
    {
        _poseSequence?.Kill();
        if (bossVisual == null) return;
        bossVisual.localPosition = _visualStartLocalPosition;
        bossVisual.localRotation = _visualStartLocalRotation;
    }

    private void CleanupVisuals()
    {
        if (_strikeTelegraph != null)
        {
            Destroy(_strikeTelegraph.gameObject);
            _strikeTelegraph = null;
        }
        ClearRodTelegraphs();
        ClearPillarTelegraphs();
    }

    private void ClearPillarTelegraphs()
    {
        foreach (GroundTelegraph telegraph in _pillarTelegraphs)
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
        }
        _pillarTelegraphs.Clear();
    }

    private void ClearRodTelegraphs()
    {
        foreach (GroundTelegraph telegraph in _rodTelegraphs)
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
        }
        _rodTelegraphs.Clear();
    }

    private void RestorePhysics()
    {
        if (_rb == null) return;
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }

    [Button("여름 패턴만 바로 시작 (Play Mode 테스트)")]
    private void TestStartWithoutBoss()
    {
        if (Application.isPlaying && !IsRunning) StartCycle();
    }
}
