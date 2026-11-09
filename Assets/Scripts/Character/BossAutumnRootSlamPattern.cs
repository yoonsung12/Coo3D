using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 가을 패턴 "뿌리 내려찍기" (탑다운 원형섬 아레나 전용)다.
// 기존 BossAutumnIslandPattern(발판 날리기)은 건드리지 않고, 아레나 씬에서는 둘 중 하나만 켠다.
//
// 흐름(전투 중 한 번만 진행한다): 섬 중앙 이동 -> (날아오르기 -> 빛나는 자리 여러 곳 표시 -> 그림자가 플레이어를 따라다니다 고정
//   -> 내려찍기 -> 모든 빛나는 자리에서 뿌리가 솟음)을 반복 -> 보스가 빛나는 자리 위에 착지하면 뿌리에 찔려 명중
//   -> 정해진 횟수만큼 찔리면 쓰러져 탈진.
//  - 빛나는 자리를 벗어나 착지하면 충격파가 퍼지고 보스는 찔리지 않는다(명중 아님).
//  - 플레이어는 그림자(착지점)를 빛나는 자리 위로 유도하되, 자신은 뿌리가 솟는 자리 위에 있으면 안 된다.
//
// 보스 이동은 BossAutumnIslandPattern과 같은 예외로 Rigidbody를 Kinematic으로 바꿔 MovePosition/DOMove로 옮긴다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossAutumnRootSlamPattern : BossSeasonPatternBase
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 중앙")]
    private Transform centerPoint;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("원 예고 프리팹")]
    private GroundTelegraph circleTelegraphPrefab;
    // 빛나는 자리, 그림자, 충격파 표시에 모두 같은 프리팹을 인스턴스로 만들어 쓴다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("뿌리 프리팹")]
    private AutumnRoot rootPrefab;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;
    // 피격 연출을 건다. 루트(Rigidbody)가 아니라 자식에 걸어 물리와 충돌하지 않게 한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("효과음 AudioSource (선택)")]
    private AudioSource audioSource;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("착지 소리 (선택)")]
    private AudioClip stompClip;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("뿌리 솟는 소리 (선택)")]
    private AudioClip rootClip;
    // 뿌리 하나마다가 아니라 솟기 시작할 때 한 번만 재생한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("찔림 소리 (선택)")]
    private AudioClip impaleClip;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("착지 볼륨"), Range(0f, 1f)]
    private float stompVolume = 0.6f;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("뿌리 볼륨"), Range(0f, 1f)]
    private float rootVolume = 0.6f;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("찔림 볼륨"), Range(0f, 1f)]
    private float impaleVolume = 0.6f;

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("서 있는 높이(중심 Y)")]
    private float standHeightY = 1f;

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("중앙 이동 시간")]
    private float entryDuration = 1f;

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("파훼에 필요한 명중 횟수")]
    private int requiredHits = 3;
    // 보스가 뿌리에 이만큼 찔리면 쓰러진다.

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;
    // 발이 이보다 낮으면 물에 빠진 것으로 보고 피해를 주지 않으며, 땅으로 돌아올 때까지 패턴을 멈춘다.

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("날아오르는 높이")]
    private float flyHeight = 6f;
    // 서 있는 높이에서 이만큼 더 올라간다. 클수록 내려찍기가 길어 보인다.

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("날아오르는 시간")]
    private float riseDuration = 0.6f;

    [TabGroup("설정", "비행")]
    [SerializeField, LabelText("공중 추적 속도")]
    private float bossAirFollowSpeed = 12f;
    // 보스가 그림자 위로 따라붙는 수평 속도(m/s)다. 낮을수록 보스가 그림자보다 느리게 따라온다.

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("빛나는 자리 개수")]
    private int patchCount = 5;

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("빛나는 자리 반지름")]
    private float patchRadius = 1.6f;

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("자리끼리 최소 간격")]
    private float patchMinSpacing = 4f;
    // 자리 중심끼리의 최소 거리다. 30번 시도해도 못 놓으면 그 자리는 포기하고 적게 만든다.

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("섬 반지름 X")]
    private float islandRadiusX = 13.5f;

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("섬 반지름 Z")]
    private float islandRadiusZ = 9f;

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("배치 영역 비율"), Range(0.1f, 1f)]
    private float patchAreaScale = 0.8f;
    // 섬 가장자리(물가)에 자리가 생기지 않도록 섬 크기에 곱해 안쪽만 쓴다.

    [TabGroup("설정", "패치")]
    [SerializeField, LabelText("빛나는 자리 색")]
    private Color patchColor = new Color(1f, 0.8f, 0.2f, 0.6f);

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("그림자 반지름")]
    private float shadowRadius = 1.4f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("그림자 색")]
    private Color shadowColor = new Color(0.1f, 0.05f, 0f, 0.7f);

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("추적 시간")]
    private float followDuration = 3f;
    // 이 시간 동안 그림자가 플레이어를 따라다닌다.

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("고정 후 대기 시간")]
    private float lockDuration = 1f;
    // 그림자가 고정된 뒤 실제로 내려찍기까지의 시간이다. 플레이어가 피할 마지막 기회다.

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("내려찍는 시간")]
    private float slamDuration = 0.35f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("찍을 때 카메라 흔들림 시간")]
    private float slamShakeDuration = 0.3f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("찍을 때 카메라 흔들림 세기")]
    private float slamShakeStrength = 0.5f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("명중 판정 여유")]
    private float impaleMargin = 0.4f;
    // 착지점이 빛나는 자리 반지름 + 이 값 안이면 뿌리에 찔린 것으로 본다.

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("충격파 반지름")]
    private float shockwaveRadius = 2.5f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("충격파 피해량")]
    private float shockwaveDamage = 10f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("충격파 표시 색")]
    private Color shockwaveColor = new Color(1f, 0.25f, 0.1f, 0.6f);

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("명중 카메라 흔들림 시간")]
    private float impaleShakeDuration = 0.4f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("명중 카메라 흔들림 세기")]
    private float impaleShakeStrength = 0.8f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("명중 후 경직 시간")]
    private float impaleStunDuration = 1.5f;

    [TabGroup("설정", "내려찍기")]
    [SerializeField, LabelText("회차 사이 회복 시간")]
    private float recoverDuration = 1f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("자리당 뿌리 개수")]
    private int rootsPerPatch = 5;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("뿌리 올라와 있는 시간")]
    private float rootUpDuration = 1.2f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("뿌리 피해량")]
    private float rootDamage = 10f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("튕겨 오르는 속도")]
    private float launchUpSpeed = 9f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("바깥으로 밀리는 속도")]
    private float launchOutSpeed = 6f;
    // 충격파/뿌리 중심 반대 방향으로 밀어내는 수평 속도다.

    [TabGroup("설정", "쓰러짐")]
    [SerializeField, LabelText("쓰러지는 회전값")]
    private Vector3 collapseEuler = new Vector3(0f, 0f, 90f);
    // 임시 메시를 옆으로 눕히는 값이다. 모델과 애니메이션이 들어오면 이 연출을 교체한다.

    [TabGroup("설정", "쓰러짐")]
    [SerializeField, LabelText("쓰러질 때 내려가는 높이")]
    private float collapseDropY = 0.6f;

    [TabGroup("설정", "쓰러짐")]
    [SerializeField, LabelText("쓰러지는 시간")]
    private float collapseDuration = 0.5f;

    [TabGroup("설정", "쓰러짐")]
    [SerializeField, LabelText("기절 시간")]
    private float stunDuration = 4f;
    // 이 시간 동안 보스는 무적이 풀려 공격받을 수 있다.

    [TabGroup("설정", "쓰러짐")]
    [SerializeField, LabelText("쓰러질 때 카메라 흔들림 시간")]
    private float collapseShakeDuration = 0.4f;

    [TabGroup("설정", "쓰러짐")]
    [SerializeField, LabelText("쓰러질 때 카메라 흔들림 세기")]
    private float collapseShakeStrength = 0.5f;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("명중 횟수")]
    private string HitCountText => _hitCount + " / " + requiredHits;

    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private Coroutine _recoverRoutine;
    private Sequence _poseSequence;
    private Sequence _moveSequence;
    private bool _moveFinished; // WaitForCompletion이 코루틴에서 불안정해서 OnComplete 플래그로 대신 기다린다.
    private int _hitCount;
    private bool _singleRound; // 테스트 버튼용: 한 회차만 돌고 탈진 없이 끝낸다.
    private bool _playerHitThisRound;
    private Vector3 _visualStartLocalPosition;
    private Quaternion _visualStartLocalRotation;
    private Vector3 _visualStartLocalScale = Vector3.one;
    // 비주얼 원래 크기다(씬에서 (2,2,2)). Vector3.one으로 되돌리면 보스가 작아지므로 Awake에서 기억해 둔다.
    private readonly List<GroundTelegraph> _patches = new List<GroundTelegraph>();
    private readonly List<Vector3> _patchCenters = new List<Vector3>();
    private GroundTelegraph _shadow;
    private readonly List<AutumnRoot> _roots = new List<AutumnRoot>();

    protected override Boss.SeasonPattern Season => Boss.SeasonPattern.Autumn;
    protected override bool IsRunning => _cycleRoutine != null;

    protected override void Awake()
    {
        base.Awake();
        _rb = GetComponent<Rigidbody>();

        if (bossVisual != null)
        {
            _visualStartLocalPosition = bossVisual.localPosition;
            _visualStartLocalRotation = bossVisual.localRotation;
            _visualStartLocalScale = bossVisual.localScale;
        }
    }

    // 탈진 대기 중(IsRunning이 false인 동안)에 꺼져도 물리를 정리하기 위해 덧붙인다.
    protected override void OnDisable()
    {
        base.OnDisable();
        if (_recoverRoutine != null) Abort();
    }

    private void OnDestroy()
    {
        _poseSequence?.Kill();
        _moveSequence?.Kill();
    }

    protected override void StartCycle()
    {
        _cycleRoutine = StartCoroutine(AutumnCycleRoutine());
    }

    // 패턴이 밀려나거나 오브젝트가 꺼질 때 호출된다. 예고와 뿌리를 거두고 물리를 되돌린다.
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

        _moveSequence?.Kill();
        _moveSequence = null;
        CleanupVisuals();
        ResetVisualPose();
        RestorePhysics();
        _singleRound = false;
        _currentPhase = "대기";
        _hitCount = 0;
    }

    private IEnumerator AutumnCycleRoutine()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true; // 패턴 동안 지형에 막히거나 떨어지지 않도록 고정한다.

        _hitCount = 0;

        _currentPhase = "중앙 이동";
        yield return MoveBossRoutine(GetStandPosition(), entryDuration);

        // 정해진 횟수만큼 찔릴 때까지 시간 제한 없이 반복한다.
        while (_hitCount < requiredHits)
        {
            // 물에 빠져 있는 동안은 공격하지 않는다. 땅으로 돌아온 뒤에 이어서 진행한다.
            _currentPhase = "플레이어 대기";
            while (IsPlayerInWater()) yield return null;

            yield return RoundRoutine();

            if (_singleRound) break;

            _currentPhase = "회복";
            ResetVisualPoseSmooth();
            yield return new WaitForSeconds(recoverDuration);
            // 중앙으로 돌아가지 않고 내려찍은 자리에서 바로 다음 회차를 이어간다(쉬는 틈 없이 몰아붙이는 느낌).
        }

        if (_singleRound)
        {
            // 테스트용 한 회차: 탈진 없이 물리를 돌려놓고 대기로 돌아간다.
            _singleRound = false;
            ResetVisualPose();
            RestorePhysics();
            _currentPhase = "대기";
            _hitCount = 0;
            _cycleRoutine = null;
            yield break;
        }

        // 탈진 연출 중에는 코루틴이 끝나도 되도록 먼저 참조를 비운다(기존 가을 패턴과 같은 방식).
        _currentPhase = "탈진";
        _cycleRoutine = null;
        _recoverRoutine = StartCoroutine(SolvedRoutine());
    }

    // 한 회차: 날아오르기 -> 자리 표시 -> 그림자 추적/고정 -> 내려찍기 -> 뿌리 솟음.
    private IEnumerator RoundRoutine()
    {
        _playerHitThisRound = false;

        _currentPhase = "날아오르기";
        float airY = standHeightY + flyHeight;
        yield return MoveBossTweenRoutine(new Vector3(_rb.position.x, airY, _rb.position.z), riseDuration, Ease.OutQuad);

        _currentPhase = "빛나는 자리 표시";
        SpawnPatches();

        _currentPhase = "그림자 추적";
        yield return ShadowFollowRoutine(airY);

        _currentPhase = "내려찍기";
        Vector3 landing = _shadow != null ? GetFlat(_shadow.transform.position) : GetBossFlatPosition();
        yield return MoveBossTweenRoutine(new Vector3(landing.x, standHeightY, landing.z), slamDuration, Ease.InQuad);

        if (audioSource != null && stompClip != null) audioSource.PlayOneShot(stompClip, stompVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(slamShakeDuration, slamShakeStrength);

        bool impaled = IsOnAnyPatch(landing);

        _currentPhase = "뿌리 솟음";
        EruptRoots();
        // 패치 예고는 뿌리가 솟는 순간 거둔다.
        ClearPatches();

        float stunTime = 0f;
        if (impaled)
        {
            // 그림자는 더 필요 없다.
            if (_shadow != null) _shadow.Hide();
            OnImpaled();
            stunTime = impaleStunDuration;
        }
        else
        {
            ShowShockwave(landing);
        }

        // 뿌리가 올라와 있는 동안(+가라앉는 여유) 매 프레임 플레이어 피해를 판정한다.
        float timer = 0f;
        float totalTime = Mathf.Max(rootUpDuration + 0.3f, stunTime);
        while (timer < totalTime)
        {
            CheckRootHits();
            timer += Time.deltaTime; // 프레임 속도와 무관하게 같은 시간 동안 판정하기 위해 쓴다.
            yield return null;
        }

        CleanupVisuals();
    }

    // 섬 타원 안쪽에서 서로 간격을 둔 무작위 지점에 빛나는 자리를 만든다.
    private void SpawnPatches()
    {
        ClearPatches();
        _patchCenters.Clear(); // 이전 회차의 자리 좌표가 남아 간격 계산과 판정을 오염시키지 않게 비운다.
        if (circleTelegraphPrefab == null) return;

        Vector3 c = GetStandPosition();
        for (int i = 0; i < patchCount; i++)
        {
            bool placed = false;
            Vector3 point = Vector3.zero;
            for (int attempt = 0; attempt < 30 && !placed; attempt++)
            {
                // sqrt를 써야 타원 안에서 점이 고르게 퍼진다(그냥 쓰면 중앙에 몰린다).
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Random.value);
                point = new Vector3(
                    c.x + Mathf.Cos(angle) * r * islandRadiusX * patchAreaScale,
                    0f,
                    c.z + Mathf.Sin(angle) * r * islandRadiusZ * patchAreaScale);

                placed = true;
                foreach (Vector3 other in _patchCenters)
                {
                    if ((other - point).magnitude < patchMinSpacing) { placed = false; break; }
                }
            }
            if (!placed) continue; // 자리를 못 찾으면 그 자리는 포기하고 적게 만든다.

            GroundTelegraph patch = Instantiate(circleTelegraphPrefab);
            patch.ShowCircle(point, patchRadius);
            patch.SetWarningColor(patchColor);
            _patches.Add(patch);
            _patchCenters.Add(point);
        }
    }

    // 그림자가 followDuration 동안 플레이어를 따라다니고 보스는 그 위 공중에서 따라붙는다. 그 뒤 고정하고 기다린다.
    private IEnumerator ShadowFollowRoutine(float airY)
    {
        if (circleTelegraphPrefab != null)
        {
            _shadow = Instantiate(circleTelegraphPrefab);
            _shadow.SetWarningColor(shadowColor);
            _shadow.ShowCircle(GetPlayerFlatPosition(), shadowRadius);
        }

        float elapsed = 0f;
        while (elapsed < followDuration)
        {
            // 물에 빠진 동안은 그림자가 마지막 위치에 머문다(물 위에 내려찍지 않게 하기 위함은 아니고 단순히 위치 유지).
            Vector3 target = IsPlayerInWater() && _shadow != null ? GetFlat(_shadow.transform.position) : GetPlayerFlatPosition();
            if (_shadow != null) _shadow.ShowCircle(target, shadowRadius);

            // 키네마틱 Rigidbody는 물리 주기에 맞춰 MovePosition으로 움직여야 떨림이 없다.
            Vector3 goal = new Vector3(target.x, airY, target.z);
            Vector3 next = Vector3.MoveTowards(_rb.position, goal, bossAirFollowSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (_shadow != null) _shadow.Lock();
        yield return new WaitForSeconds(lockDuration);
    }

    // 모든 빛나는 자리에서 뿌리를 한꺼번에 솟게 한다.
    private void EruptRoots()
    {
        if (audioSource != null && rootClip != null) audioSource.PlayOneShot(rootClip, rootVolume);
        if (rootPrefab == null) return;

        foreach (Vector3 center in _patchCenters)
        {
            for (int i = 0; i < rootsPerPatch; i++)
            {
                // 자리 가장자리까지 가득 차 보이지 않도록 반지름의 70% 안쪽에 뿌린다.
                Vector2 offset = Random.insideUnitCircle * (patchRadius * 0.7f);
                Vector3 pos = new Vector3(center.x + offset.x, 0f, center.z + offset.y);
                AutumnRoot root = Instantiate(rootPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                root.Play(rootUpDuration);
                _roots.Add(root);
            }
        }
    }

    private bool IsOnAnyPatch(Vector3 landing)
    {
        foreach (Vector3 center in _patchCenters)
        {
            if ((landing - center).magnitude <= patchRadius + impaleMargin) return true;
        }
        return false;
    }

    // 보스가 뿌리에 찔린 순간: 명중 횟수를 올리고 피격 연출을 건다.
    private void OnImpaled()
    {
        _hitCount++;

        if (audioSource != null && impaleClip != null) audioSource.PlayOneShot(impaleClip, impaleVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(impaleShakeDuration, impaleShakeStrength);

        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOPunchScale(Vector3.one * 0.3f, 0.35f, 6, 0.6f))
                .Join(bossVisual.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.35f, 8, 0.6f))
                .SetLink(gameObject);
        }
    }

    // 빗나갔을 때: 착지점에 충격파 범위를 잠깐 보여주고, 안에 있던 플레이어에게 피해를 준다.
    private void ShowShockwave(Vector3 landing)
    {
        if (_shadow != null)
        {
            _shadow.ShowCircle(landing, shockwaveRadius);
            _shadow.SetWarningColor(shockwaveColor);
            _shadow.Hide();
        }

        if (playerController == null || IsPlayerInWater()) return;

        Vector3 playerFlat = GetPlayerFlatPosition();
        if ((playerFlat - landing).magnitude > shockwaveRadius) return;

        _playerHitThisRound = true; // 충격파에 맞았으면 같은 회차 뿌리 피해는 중복해서 받지 않는다.
        PlayerHealth.Instance?.TakeDamage(shockwaveDamage);
        LaunchPlayerFrom(landing);
    }

    // 솟아 있는 뿌리 근처에 플레이어가 있는지 확인한다. 한 회차에 한 번만 맞는다.
    private void CheckRootHits()
    {
        if (_playerHitThisRound || playerController == null || IsPlayerInWater()) return;

        Vector3 playerFlat = GetPlayerFlatPosition();
        foreach (Vector3 center in _patchCenters)
        {
            if ((playerFlat - center).magnitude > patchRadius) continue;
            if (!HasRootUpNear(center)) continue;

            _playerHitThisRound = true;
            PlayerHealth.Instance?.TakeDamage(rootDamage);
            LaunchPlayerFrom(center);
            return;
        }
    }

    // 해당 자리 반지름 안에 지금 솟아 있는 뿌리가 하나라도 있는지 본다.
    private bool HasRootUpNear(Vector3 center)
    {
        foreach (AutumnRoot root in _roots)
        {
            if (root == null || !root.IsUp) continue;
            if ((GetFlat(root.transform.position) - center).magnitude <= patchRadius) return true;
        }
        return false;
    }

    // origin 반대쪽으로 비스듬히 튕겨 올린다.
    private void LaunchPlayerFrom(Vector3 origin)
    {
        Vector3 away = GetPlayerFlatPosition() - origin;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
        playerController.SetBlast(Vector3.up * launchUpSpeed + away * launchOutSpeed);
    }

    // 정해진 횟수만큼 찔린 뒤: 쓰러져 기절하고, 끝나면 일어선다.
    private IEnumerator SolvedRoutine()
    {
        CleanupVisuals();
        CollapseBoss();

        yield return new WaitForSeconds(stunDuration);

        _currentPhase = "복구";
        yield return StandUpRoutine();

        _currentPhase = "대기";
        _recoverRoutine = null;
    }

    // 높이는 시각 오브젝트가 아니라 루트로 낮춘다(피격 연출이 시각 오브젝트 위치를 되돌리기 때문).
    private void CollapseBoss()
    {
        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(standHeightY - collapseDropY, collapseDuration).SetEase(Ease.InQuad))
            .SetLink(gameObject);

        if (bossVisual != null)
        {
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation * Quaternion.Euler(collapseEuler), collapseDuration)
                .SetEase(Ease.InQuad));
        }

        if (sideViewCamera != null) sideViewCamera.Shake(collapseShakeDuration, collapseShakeStrength);

        _boss.NotifyPatternSolved(stunDuration);
    }

    private IEnumerator StandUpRoutine()
    {
        _poseSequence?.Kill();
        bool finished = false;
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(standHeightY, collapseDuration).SetEase(Ease.OutQuad))
            .OnComplete(() => finished = true)
            .SetLink(gameObject);

        if (bossVisual != null)
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation, collapseDuration).SetEase(Ease.OutQuad));

        // OnComplete 플래그로 기다린다(WaitForCompletion은 이 프로젝트에서 불안정했다).
        while (!finished && _poseSequence != null && _poseSequence.IsActive()) yield return null;
        RestorePhysics();
    }

    // 보스 루트를 DOTween으로 목표까지 옮기고 끝날 때까지 기다린다. Kinematic이라 지형과 충돌하지 않는다.
    private IEnumerator MoveBossTweenRoutine(Vector3 target, float duration, Ease ease)
    {
        _moveFinished = false;
        _moveSequence?.Kill();
        _moveSequence = DOTween.Sequence()
            .Append(_rb.DOMove(target, duration).SetEase(ease))
            .OnComplete(() => _moveFinished = true)
            .SetLink(gameObject);

        yield return new WaitUntil(() => _moveFinished);
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

    private Vector3 GetStandPosition()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        return new Vector3(c.x, standHeightY, c.z);
    }

    private static Vector3 GetFlat(Vector3 p)
    {
        return new Vector3(p.x, 0f, p.z);
    }

    private Vector3 GetBossFlatPosition()
    {
        return new Vector3(_rb.position.x, 0f, _rb.position.z);
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

    // 회복 구간에서 시각 오브젝트를 부드럽게 원래 자세로 돌려놓는다.
    private void ResetVisualPoseSmooth()
    {
        if (bossVisual == null) return;
        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation, recoverDuration * 0.5f).SetEase(Ease.OutQuad))
            .SetLink(gameObject);
    }

    private void ResetVisualPose()
    {
        _poseSequence?.Kill();
        if (bossVisual == null) return;
        bossVisual.localPosition = _visualStartLocalPosition;
        bossVisual.localRotation = _visualStartLocalRotation;
        bossVisual.localScale = _visualStartLocalScale;
    }

    private void CleanupVisuals()
    {
        ClearPatches();
        _patchCenters.Clear();
        if (_shadow != null)
        {
            Destroy(_shadow.gameObject);
            _shadow = null;
        }
        foreach (AutumnRoot root in _roots)
        {
            if (root != null) Destroy(root.gameObject);
        }
        _roots.Clear();
    }

    // 빛나는 자리 예고만 거둔다. 자리 좌표는 뿌리 판정에 쓰이므로 EruptRoots 이후에도 남겨 둔다.
    private void ClearPatches()
    {
        foreach (GroundTelegraph patch in _patches)
        {
            if (patch != null) Destroy(patch.gameObject);
        }
        _patches.Clear();
    }

    private void RestorePhysics()
    {
        if (_rb == null) return;
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }

    [Button("가을 패턴만 바로 시작 (Play Mode 테스트)")]
    private void TestStartWithoutBoss()
    {
        if (Application.isPlaying && !IsRunning) StartCycle();
    }

    [Button("1회만 실행 (Play Mode 테스트)")]
    private void TestSingleRound()
    {
        if (!Application.isPlaying || IsRunning) return;
        _singleRound = true;
        StartCycle();
    }
}
