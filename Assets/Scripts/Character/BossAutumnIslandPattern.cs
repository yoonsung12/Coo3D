using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 가을 패턴(되튕기는 발판) 중 "탑다운 원형섬 아레나" 전용 버전이다.
// 기존 사이드뷰용 가을 패턴은 건드리지 않고, 아레나 씬에서는 이 컴포넌트만 켠다.
//
// 흐름(전투 중 딱 한 번만 진행한다): 섬 중앙 이동 -> (발구르기 -> 경로 예고 -> 뿌리가 보스에서 플레이어 쪽으로 차례로 솟음)을 반복
//   -> 마지막 뿌리가 솟은 자리에 발판이 있으면 그 발판이 보스에게 날아가 맞음 -> 정해진 횟수만큼 맞으면 쓰러져 탈진.
//  - 뿌리는 장애물이 아니라 곧바로 가라앉는 공격이다. 닿으면 피해를 받고 위로 튕겨 오른다.
//  - 플레이어는 마지막 뿌리가 발판 위에 솟도록 유도하되, 그 발판 위에 서 있으면 안 된다(피해를 입는다).
//  - 한 번 쓰인 발판은 비활성화되어 돌아오지 않는다.
//
// 보스 이동은 BossSummerIslandPattern과 같은 예외로 Rigidbody를 Kinematic으로 바꿔 MovePosition으로 옮긴다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossAutumnIslandPattern : BossSeasonPatternBase
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 중앙")]
    private Transform centerPoint;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발판 목록 (14개)")]
    private Transform[] platforms;
    // 날려 보낼 후보 발판이다. 봄 패턴(BossSpringIslandPattern)과 같은 발판들을 연결한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("원 예고 프리팹")]
    private GroundTelegraph circleTelegraphPrefab;
    // 뿌리가 솟을 자리마다 하나씩 띄운다. 뿌리가 솟을 때 파괴한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("뿌리 프리팹")]
    private AutumnRoot rootPrefab;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;
    // 발구르기/피격/쓰러짐 연출을 건다. 루트(Rigidbody)가 아니라 자식에 걸어 물리와 충돌하지 않게 한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("효과음 AudioSource (선택)")]
    private AudioSource audioSource;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발구르기 소리 (선택)")]
    private AudioClip stompClip;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("뿌리 솟는 소리 (선택)")]
    private AudioClip rootClip;
    // 뿌리 하나마다가 아니라 솟기 시작할 때 한 번만 재생한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발판 명중 소리 (선택)")]
    private AudioClip platformHitClip;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발구르기 볼륨"), Range(0f, 1f)]
    private float stompVolume = 0.6f;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("뿌리 볼륨"), Range(0f, 1f)]
    private float rootVolume = 0.6f;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발판 명중 볼륨"), Range(0f, 1f)]
    private float platformHitVolume = 0.6f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("서 있는 높이(중심 Y)")]
    private float standHeightY = 1f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("중앙 이동 시간")]
    private float entryDuration = 1f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("파훼에 필요한 명중 횟수")]
    private int requiredHits = 3;
    // 발판이 보스에게 이만큼 맞으면 보스가 쓰러진다.

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;
    // 발이 이보다 낮으면 물에 빠진 것으로 보고 뿌리 피해를 주지 않으며, 땅으로 돌아올 때까지 패턴을 멈춘다.

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("발구르기 전조 시간")]
    private float stompDuration = 0.8f;

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("뒤로 젖히는 각도")]
    private float windUpAngle = -20f;
    // 보스 시각 오브젝트를 X축으로 이만큼 기울여 내려찍기 전 자세를 만든다.

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("발구르기 카메라 흔들림 시간")]
    private float stompShakeDuration = 0.2f;

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("발구르기 카메라 흔들림 세기")]
    private float stompShakeStrength = 0.2f;

    [TabGroup("설정", "경로")]
    [SerializeField, LabelText("뿌리 간격")]
    private float rootSpacing = 1.5f;
    // 보스에서 플레이어 쪽으로 뿌리가 놓이는 간격(m)이다. 좁을수록 길이 촘촘해진다.

    [TabGroup("설정", "경로")]
    [SerializeField, LabelText("첫 뿌리 거리")]
    private float pathStartOffset = 1.8f;
    // 보스 중심에서 첫 뿌리까지의 거리(m)다. 보스 몸 밑에는 뿌리가 놓이지 않게 한다.

    [TabGroup("설정", "경로")]
    [SerializeField, LabelText("경로 예고 시간")]
    private float pathTelegraphDuration = 0.6f;

    [TabGroup("설정", "경로")]
    [SerializeField, LabelText("예고 반지름")]
    private float rootTelegraphRadius = 0.75f;

    [TabGroup("설정", "경로")]
    [SerializeField, LabelText("마지막 지점 예고 반지름")]
    private float lastTelegraphRadius = 1.2f;
    // 플레이어 위치였던 마지막 지점은 발판 판정의 기준이라 더 크게 보여 구분한다.

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("뿌리 솟는 간격")]
    private float rootStepDelay = 0.06f;
    // 뿌리가 하나씩 솟는 시간차(초)다. 클수록 보스에서 멀어지는 물결이 느리게 퍼진다.

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("뿌리 올라와 있는 시간")]
    private float rootUpDuration = 0.4f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("뿌리 판정 반경")]
    private float rootHitRadius = 0.9f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("뿌리 피해량")]
    private float rootDamage = 10f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("튕겨 오르는 속도")]
    private float launchUpSpeed = 9f;

    [TabGroup("설정", "뿌리")]
    [SerializeField, LabelText("바깥으로 밀리는 속도")]
    private float launchOutSpeed = 3f;
    // 보스 반대 방향으로 밀어내는 수평 속도다.

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("발판 판정 여유")]
    private float platformMargin = 0.2f;
    // 마지막 뿌리가 발판 가장자리에 걸쳐도 인정하기 위한 수평 여유(m)다.

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("날아가는 시간")]
    private float platformFlightDuration = 0.7f;

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("포물선 높이")]
    private float platformArcHeight = 4f;

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("회전량(도)")]
    private Vector3 platformSpin = new Vector3(540f, 360f, 180f);
    // 날아가는 동안 발판이 도는 총 각도다. 값이 클수록 빠르게 빙글 돈다.

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("사라지는 시간")]
    private float platformShrinkDuration = 0.15f;

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("보스 명중 카메라 흔들림 시간")]
    private float hitShakeDuration = 0.3f;

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("보스 명중 카메라 흔들림 세기")]
    private float hitShakeStrength = 0.4f;

    [TabGroup("설정", "발판")]
    [SerializeField, LabelText("회차 사이 회복 시간")]
    private float recoverDuration = 0.8f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러지는 회전값")]
    private Vector3 collapseEuler = new Vector3(0f, 0f, 90f);
    // 임시 메시를 옆으로 눕히는 값이다. 모델과 애니메이션이 들어오면 이 연출을 교체한다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러질 때 내려가는 높이")]
    private float collapseDropY = 0.6f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러지는 시간")]
    private float collapseDuration = 0.5f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("기절 시간")]
    private float stunDuration = 4f;
    // 이 시간 동안 보스는 무적이 풀려 공격받을 수 있다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러질 때 카메라 흔들림 시간")]
    private float collapseShakeDuration = 0.4f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러질 때 카메라 흔들림 세기")]
    private float collapseShakeStrength = 0.5f;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("명중 횟수")]
    private string HitCountText => _hitCount + " / " + requiredHits;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("남은 활성 발판 수")]
    private int RemainingPlatformCount
    {
        get
        {
            int n = 0;
            if (platforms == null) return 0;
            foreach (Transform platform in platforms)
            {
                if (platform != null && platform.gameObject.activeSelf) n++;
            }
            return n;
        }
    }

    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private Coroutine _recoverRoutine;
    private Sequence _poseSequence;
    private Sequence _flightSequence;
    private Transform _flyingPlatform;
    private Vector3 _flyingPlatformScale;
    private bool _flightFinished;
    private int _hitCount;
    private bool _playerHitThisCycle;
    private Vector3 _visualStartLocalPosition;
    private Quaternion _visualStartLocalRotation;
    private readonly List<GroundTelegraph> _telegraphs = new List<GroundTelegraph>();
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
        _flightSequence?.Kill();
    }

    protected override void StartCycle()
    {
        _cycleRoutine = StartCoroutine(AutumnCycleRoutine());
    }

    // 패턴이 밀려나거나 오브젝트가 꺼질 때 호출된다. 뿌리와 예고를 거두고 물리를 되돌린다.
    // 이미 쓰인(또는 날아가던) 발판은 비활성화된 채로 둔다.
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

        CleanupVisuals();
        FinishFlyingPlatformImmediately();
        ResetVisualPose();
        RestorePhysics();
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

        // 정해진 횟수만큼 발판이 맞을 때까지 시간 제한 없이 반복한다.
        while (_hitCount < requiredHits)
        {
            // 물에 빠져 있는 동안은 공격하지 않는다. 땅으로 돌아온 뒤에 이어서 진행한다.
            _currentPhase = "플레이어 대기";
            while (IsPlayerInWater()) yield return null;

            _currentPhase = "발구르기";
            yield return StompRoutine();

            _currentPhase = "경로 예고";
            List<Vector3> path = BuildPath();
            ShowPathTelegraphs(path);
            yield return new WaitForSeconds(pathTelegraphDuration);

            _currentPhase = "뿌리 솟음";
            yield return EruptionRoutine(path);

            // 발판이 날아가 보스에게 맞을 때까지 기다린다.
            _currentPhase = "발판 비행";
            while (!_flightFinished) yield return null;

            _currentPhase = "회복";
            ResetVisualPoseSmooth();
            yield return new WaitForSeconds(recoverDuration);
        }

        // 탈진 연출 중에는 코루틴이 끝나도 되도록 먼저 참조를 비운다(여름 패턴과 같은 방식).
        _currentPhase = "탈진";
        _cycleRoutine = null;
        _recoverRoutine = StartCoroutine(SolvedRoutine());
    }

    // 몸을 뒤로 젖혔다가 내려찍는다. 끝에서 카메라가 흔들린다.
    private IEnumerator StompRoutine()
    {
        float windUpTime = stompDuration * 0.7f;
        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation * Quaternion.Euler(windUpAngle, 0f, 0f), windUpTime)
                    .SetEase(Ease.OutQuad))
                .SetLink(gameObject);
        }

        yield return new WaitForSeconds(windUpTime);

        // 마지막 구간에 내려찍으며 작게 눌리는 연출을 준다.
        if (bossVisual != null)
        {
            float punchTime = stompDuration - windUpTime;
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation, punchTime).SetEase(Ease.InQuad))
                .Append(bossVisual.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.6f))
                .SetLink(gameObject);
            yield return new WaitForSeconds(punchTime);
        }

        if (audioSource != null && stompClip != null) audioSource.PlayOneShot(stompClip, stompVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(stompShakeDuration, stompShakeStrength);
    }

    // 보스에서 이 순간의 플레이어 위치까지의 뿌리 지점들을 만든다. 마지막 지점은 항상 플레이어 위치 그대로다.
    private List<Vector3> BuildPath()
    {
        List<Vector3> path = new List<Vector3>();
        Vector3 from = GetBossFlatPosition();
        Vector3 target = GetPlayerFlatPosition();
        Vector3 toTarget = target - from;
        float distance = toTarget.magnitude;
        Vector3 direction = distance > 0.01f ? toTarget / distance : Vector3.forward;

        // 마지막 지점(플레이어 위치)과 너무 가까운 일반 지점은 겹쳐 보이므로 건너뛴다.
        for (float d = pathStartOffset; d < distance - rootSpacing * 0.5f; d += rootSpacing)
            path.Add(from + direction * d);

        path.Add(target);
        return path;
    }

    private void ShowPathTelegraphs(List<Vector3> path)
    {
        ClearTelegraphs();
        if (circleTelegraphPrefab == null) return;

        for (int i = 0; i < path.Count; i++)
        {
            bool isLast = i == path.Count - 1;
            GroundTelegraph telegraph = Instantiate(circleTelegraphPrefab);
            telegraph.ShowCircle(path[i], isLast ? lastTelegraphRadius : rootTelegraphRadius);
            _telegraphs.Add(telegraph);
        }
    }

    // 뿌리를 보스 쪽부터 차례로 솟게 하면서 매 프레임 피해 판정을 한다.
    // 마지막 뿌리가 솟는 순간 그 자리의 발판을 날려 보낸다.
    private IEnumerator EruptionRoutine(List<Vector3> path)
    {
        _playerHitThisCycle = false;
        _flightFinished = true; // 발판이 날아가지 않는 회차는 기다릴 필요가 없다.

        if (audioSource != null && rootClip != null) audioSource.PlayOneShot(rootClip, rootVolume);

        int next = 0;
        float timer = 0f;
        float totalTime = (path.Count - 1) * rootStepDelay + rootUpDuration + 0.3f;
        _roots.Clear();

        while (timer < totalTime)
        {
            // 프레임이 길어도 건너뛰지 않도록 시간이 된 뿌리를 모두 솟게 한다.
            while (next < path.Count && timer >= next * rootStepDelay)
            {
                SpawnRoot(path[next], next);
                if (next == path.Count - 1) TryLaunchPlatformAt(path[next]);
                next++;
            }

            CheckRootHits();
            timer += Time.deltaTime; // 프레임 속도와 무관하게 같은 시간 간격으로 솟게 하기 위해 쓴다.
            yield return null;
        }

        _roots.Clear();
    }

    private void SpawnRoot(Vector3 position, int index)
    {
        // 해당 지점의 예고 원은 뿌리가 솟는 순간 거둔다.
        if (index < _telegraphs.Count && _telegraphs[index] != null)
        {
            Destroy(_telegraphs[index].gameObject);
            _telegraphs[index] = null;
        }

        if (rootPrefab == null) return;
        AutumnRoot root = Instantiate(rootPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        root.Play(rootUpDuration);
        _roots.Add(root);
    }

    // 솟아 있는 뿌리에 플레이어가 닿았는지 확인한다. 한 회차에 한 번만 맞는다.
    private void CheckRootHits()
    {
        if (_playerHitThisCycle || playerController == null || IsPlayerInWater()) return;

        Vector3 playerFlat = GetPlayerFlatPosition();
        foreach (AutumnRoot root in _roots)
        {
            if (root == null || !root.IsUp) continue;

            Vector3 offset = playerFlat - new Vector3(root.transform.position.x, 0f, root.transform.position.z);
            if (offset.magnitude > rootHitRadius) continue;

            _playerHitThisCycle = true;
            PlayerHealth.Instance?.TakeDamage(rootDamage);

            // 보스 반대쪽으로 비스듬히 튕겨 올린다.
            Vector3 away = playerFlat - GetBossFlatPosition();
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
            playerController.SetBlast(Vector3.up * launchUpSpeed + away * launchOutSpeed);
            return;
        }
    }

    // 마지막 뿌리 지점을 수평으로 포함하는 활성 발판이 있으면 날려 보낸다. 없으면 아무 일도 없다.
    private void TryLaunchPlatformAt(Vector3 point)
    {
        Transform platform = FindPlatformAt(point);
        if (platform == null) return;

        // 플레이어가 딸려 날아가지 않도록 충돌체를 먼저 끈다. 위에 서 있었다면 뿌리 판정이 이미 피해를 준다.
        BoxCollider box = platform.GetComponent<BoxCollider>();
        if (box != null) box.enabled = false;

        _flyingPlatform = platform;
        _flyingPlatformScale = platform.localScale;
        _flightFinished = false;

        Vector3 target = _rb.position + Vector3.up;
        Vector3 spin = new Vector3(
            platformSpin.x * (Random.value < 0.5f ? -1f : 1f),
            platformSpin.y * (Random.value < 0.5f ? -1f : 1f),
            platformSpin.z * (Random.value < 0.5f ? -1f : 1f));

        _flightSequence?.Kill();
        _flightSequence = DOTween.Sequence()
            .Append(platform.DOJump(target, platformArcHeight, 1, platformFlightDuration).SetEase(Ease.Linear))
            .Join(platform.DORotate(spin, platformFlightDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear))
            .OnComplete(OnPlatformArrived)
            .SetLink(gameObject);
    }

    private Transform FindPlatformAt(Vector3 point)
    {
        if (platforms == null) return null;

        foreach (Transform platform in platforms)
        {
            if (platform == null || !platform.gameObject.activeSelf) continue;

            BoxCollider box = platform.GetComponent<BoxCollider>();
            if (box == null || !box.enabled) continue;

            Bounds b = box.bounds;
            if (point.x >= b.min.x - platformMargin && point.x <= b.max.x + platformMargin &&
                point.z >= b.min.z - platformMargin && point.z <= b.max.z + platformMargin)
                return platform;
        }
        return null;
    }

    // 발판이 보스에게 닿은 순간: 명중 횟수를 올리고 피격 연출 뒤 발판을 영구히 치운다.
    private void OnPlatformArrived()
    {
        _hitCount++;

        if (audioSource != null && platformHitClip != null) audioSource.PlayOneShot(platformHitClip, platformHitVolume);
        if (sideViewCamera != null) sideViewCamera.Shake(hitShakeDuration, hitShakeStrength);

        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOPunchScale(Vector3.one * 0.3f, 0.35f, 6, 0.6f))
                .Join(bossVisual.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.35f, 8, 0.6f))
                .SetLink(gameObject);
        }

        Transform platform = _flyingPlatform;
        if (platform == null)
        {
            _flightFinished = true;
            return;
        }

        _flightSequence = DOTween.Sequence()
            .Append(platform.DOScale(Vector3.zero, platformShrinkDuration).SetEase(Ease.InQuad))
            .OnComplete(FinishFlyingPlatformImmediately)
            .SetLink(gameObject);
    }

    // 날아가던 발판의 Tween을 멈추고 비활성화한다. 한 번 쓰인 발판은 돌아오지 않는다.
    private void FinishFlyingPlatformImmediately()
    {
        _flightSequence?.Kill();
        _flightSequence = null;

        if (_flyingPlatform != null)
        {
            _flyingPlatform.gameObject.SetActive(false);
            _flyingPlatform.localScale = _flyingPlatformScale; // 줄어든 크기가 남지 않게 되돌린다(비활성 상태라 보이지 않는다).
            _flyingPlatform = null;
        }
        _flightFinished = true;
    }

    // 정해진 횟수만큼 맞은 뒤: 쓰러져 기절하고, 끝나면 일어선다.
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

    // 높이는 시각 오브젝트가 아니라 루트로 낮춘다(여름 패턴과 같은 이유: 피격 연출이 시각 오브젝트 위치를 되돌린다).
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
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(standHeightY, collapseDuration).SetEase(Ease.OutQuad))
            .SetLink(gameObject);

        if (bossVisual != null)
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation, collapseDuration).SetEase(Ease.OutQuad));

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

    private Vector3 GetStandPosition()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        return new Vector3(c.x, standHeightY, c.z);
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
        bossVisual.localScale = Vector3.one;
    }

    private void CleanupVisuals()
    {
        ClearTelegraphs();
        foreach (AutumnRoot root in _roots)
        {
            if (root != null) Destroy(root.gameObject);
        }
        _roots.Clear();
    }

    private void ClearTelegraphs()
    {
        foreach (GroundTelegraph telegraph in _telegraphs)
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
        }
        _telegraphs.Clear();
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
}
