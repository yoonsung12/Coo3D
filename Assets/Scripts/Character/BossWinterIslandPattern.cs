using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 겨울 패턴(칼바람 날갯짓) 중 "탑다운 원형섬 아레나" 전용 버전이다.
// 기존 사이드뷰용 BossWinterPattern은 건드리지 않고, 아레나 씬에서는 이 컴포넌트만 켠다.
//
// 흐름(전투 중 딱 한 번만 진행한다): 섬 중앙 이동 -> 바위 솟음 -> (플레이어 지정 + 전조 -> 칼바람)을 회차 수만큼 -> 탈진해 쓰러짐.
//  - 바람은 플레이어 쪽을 향하므로, 서는 자리에 따라 다른 바위도 경로에 들어가 함께 깎인다. 바위는 두 번 맞으면 부서진다.
//  - 바위 뒤에 숨었든 강물에 빠졌든 마지막 바람이 끝나면 쓰러진다. 실패의 대가는 피해와 복귀 지점 이동이다.
//  - 플레이어를 미는 힘은 PlayerController.SetWindZone을 재사용한다(CharacterController.Move 흐름 유지).
//
// 보스 이동은 BossSpringIslandPattern과 같은 예외로 Rigidbody를 Kinematic으로 바꿔 MovePosition으로 옮긴다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossWinterIslandPattern : BossSeasonPatternBase
{
    // 칼바람 한 회차의 설정이다. 회차마다 다른 느낌을 내려고 목록으로 둔다.
    [System.Serializable]
    private class WindSwing
    {
        [LabelText("이름")]
        public string name;

        [LabelText("전조 시간")]
        public float telegraphDuration = 1.5f;
        // 플레이어를 지정한 뒤 휘두르기까지의 시간이다. 길수록 바위까지 달려갈 여유가 생긴다.

        [LabelText("방향 고정 시간")]
        public float lockDuration = 0.5f;
        // 전조의 마지막 이 시간 동안은 플레이어를 따라가지 않는다.

        [LabelText("바람 지속 시간")]
        public float windDuration = 2f;

        [LabelText("부채꼴 각도")]
        public float fanAngle = 90f;

        [LabelText("보스 이동 각도")]
        public float sweepAngle;
        // 바람이 부는 동안 보스가 섬 중앙을 도는 각도다. 0이면 제자리에서 분다.
        // 보스가 움직여야 바위 뒤 안전 구역도 돌아간다. 클수록 빨리 돌아 따라가기 어렵다.

        [LabelText("카메라 흔들림 세기")]
        public float shakeStrength = 0.35f;

        [LabelText("끝난 뒤 쉬는 시간")]
        public float restAfter = 1f;
        // 다음 회차 전조까지의 틈이다. 마지막 회차는 쓰지 않는다.
    }

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 중앙")]
    private Transform centerPoint;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바위 목록")]
    private IslandRock[] rocks;
    // 봄 패턴에서 부서진 바위는 IsBroken이라 자동으로 솟지 않는다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("부채꼴 예고")]
    private WindFanTelegraph fanTelegraph;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("원 예고 프리팹")]
    private GroundTelegraph circleTelegraphPrefab;
    // 바위 솟음 예고와 플레이어 지정 표시에 쓴다. 사용 후 파괴한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바람 파티클")]
    private ParticleSystem windParticles;
    // 보스 앞쪽으로 퍼지는 칼바람 입자다. 방향은 런타임에 맞춘다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;
    // 젖히기, 휘두르기, 쓰러짐 연출을 건다. 루트(Rigidbody)가 아니라 자식에 걸어 물리와 충돌하지 않게 한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바람 소리 AudioSource (선택)")]
    private AudioSource audioSource;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("바람 소리 (선택)")]
    private AudioClip windClip;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("보스 서 있는 높이(중심 Y)")]
    private float standHeightY = 1f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("중앙 이동 시간")]
    private float entryDuration = 1f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("바위 예고 시간")]
    private float rockTelegraphDuration = 1f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("바위 예고 여유 반지름")]
    private float rockTelegraphMargin = 0.5f;

    [TabGroup("설정", "준비")]
    [SerializeField, LabelText("겹친 플레이어 밀어내는 속도")]
    private float rockPushSpeed = 10f;

    [TabGroup("설정", "회차")]
    [SerializeField, LabelText("칼바람 회차")]
    private WindSwing[] swings =
    {
        new WindSwing { name = "1타: 고정", telegraphDuration = 1.5f, windDuration = 2f, fanAngle = 90f, sweepAngle = 0f, shakeStrength = 0.3f, restAfter = 1f },
        new WindSwing { name = "2타: 이동", telegraphDuration = 1.5f, windDuration = 3f, fanAngle = 90f, sweepAngle = 120f, shakeStrength = 0.35f, restAfter = 2.5f },
        new WindSwing { name = "3타: 대폭풍", telegraphDuration = 2f, windDuration = 3f, fanAngle = 120f, sweepAngle = 0f, shakeStrength = 0.6f, restAfter = 0f },
    };

    [TabGroup("설정", "회차")]
    [SerializeField, LabelText("보스 이동 반지름")]
    private float orbitRadius = 4f;
    // 이동 회차에서 보스가 섬 중앙을 도는 거리다. 위쪽 바위 앞면(중앙에서 약 4.2)에 끼지 않게 그보다 작게 둔다.

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("부채꼴 길이")]
    private float fanRadius = 30f;
    // 맵 끝까지 닿도록 돌발판보다 멀리 잡는다.

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("지정 표시 반지름")]
    private float targetMarkerRadius = 1f;

    [TabGroup("설정", "전조")]
    [SerializeField, LabelText("젖히는 각도")]
    private float windUpAngle = -25f;

    [TabGroup("설정", "칼바람")]
    [SerializeField, LabelText("밀리는 속도(m/s)")]
    private float windPushSpeed = 10f;
    // PDF의 5칸/초(1칸 약 2m)다. 걷기 속도(5)보다 빨라 경로 안에서는 거슬러 갈 수 없다.

    [TabGroup("설정", "칼바람")]
    [SerializeField, LabelText("피해량")]
    private float windDamage = 10f;
    // 회차마다 바람에 처음 닿을 때 한 번만 준다. 체력 100 기준 하트 반 칸이다.

    [TabGroup("설정", "칼바람")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;
    // 발이 이보다 낮으면 물에 빠진 것으로 보고 이번 바람을 더 적용하지 않는다.
    // 그러지 않으면 복귀 지점으로 돌아온 플레이어가 같은 바람에 다시 밀려 떨어진다.

    [TabGroup("설정", "칼바람")]
    [SerializeField, LabelText("휘두르기 각도")]
    private float swingAngle = 20f;

    [TabGroup("설정", "칼바람")]
    [SerializeField, LabelText("카메라 흔들림 시간")]
    private float windShakeDuration = 0.5f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("일어나지 않음")]
    private bool stayDown = true;
    // 켜면 쓰러진 채로 마지막 일격을 기다린다. 끄면 아래 시간 뒤에 일어난다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러져 있는 시간"), HideIf("stayDown")]
    private float collapseDuration = 6f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러지는 회전값")]
    private Vector3 collapseEuler = new Vector3(0f, 0f, 90f);
    // 임시 메시를 옆으로 눕히는 값이다. 모델과 애니메이션이 들어오면 이 연출을 교체한다.

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러질 때 내려가는 높이")]
    private float collapseDropY = 0.6f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러지는 시간")]
    private float collapseMoveDuration = 0.6f;

    [TabGroup("설정", "탈진")]
    [SerializeField, LabelText("쓰러지는 Ease")]
    private Ease collapseEase = Ease.OutBounce;

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    [FoldoutGroup("런타임 상태")]
    [ReadOnly, ShowInInspector, LabelText("현재 회차")]
    private int _currentSwingNumber;

    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private Sequence _poseSequence;
    private Vector3 _visualStartLocalPosition;
    private Quaternion _visualStartLocalRotation;
    private Vector3 _windOrigin;
    private Vector3 _windDirection;
    private Vector3 _lockedTargetPoint;
    // 전조가 끝날 때 플레이어가 있던 자리다. 이동 회차에서는 보스가 움직이면서도 이 자리를 향해 계속 분다.
    private GroundTelegraph _targetMarker;
    private readonly List<GroundTelegraph> _rockTelegraphs = new List<GroundTelegraph>();
    private readonly HashSet<IslandRock> _rocksHitThisSwing = new HashSet<IslandRock>();
    // 이번 회차 바람이 스친 바위다. 숨어 있는 도중 부서지지 않도록 회차가 끝날 때 한꺼번에 깎는다.

    protected override Boss.SeasonPattern Season => Boss.SeasonPattern.Winter;
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

        if (fanTelegraph != null)
            fanTelegraph.SetRocks(rocks);
    }

    private void OnDestroy()
    {
        _poseSequence?.Kill();
    }

    protected override void StartCycle()
    {
        _cycleRoutine = StartCoroutine(WinterCycleRoutine());
    }

    // 패턴이 밀려나거나 오브젝트가 꺼질 때 호출된다. 바람과 표시를 모두 거두고 물리를 되돌린다.
    protected override void Abort()
    {
        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        ReleasePlayer();
        CleanupVisuals();
        SinkRocks();
        ResetVisualPose();
        RestorePhysics();
        _currentPhase = "대기";
    }

    private IEnumerator WinterCycleRoutine()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true; // 패턴 동안 지형에 막히거나 떨어지지 않도록 고정한다.

        _currentPhase = "중앙 이동";
        yield return MoveToCenterRoutine();

        _currentPhase = "바위 솟음";
        yield return RiseRocksRoutine();

        for (int i = 0; i < swings.Length; i++)
        {
            WindSwing swing = swings[i];
            _currentSwingNumber = i + 1;

            // 이동 방향은 회차마다 무작위로 정해 같은 자리에 서는 정답이 생기지 않게 한다.
            float signedSweep = Random.value < 0.5f ? swing.sweepAngle : -swing.sweepAngle;

            // 앞 회차와 이번 회차 사이의 쉬는 시간에 이번 회차 시작 자리로 옮겨 간다.
            if (i > 0)
            {
                _currentPhase = "숨 고르기";
                yield return MoveBossRoutine(GetSwingStartPosition(signedSweep), swings[i - 1].restAfter, 0.5f);
            }

            _currentPhase = "전조";
            yield return TelegraphRoutine(swing, signedSweep);

            _currentPhase = "칼바람";
            yield return WindRoutine(swing, signedSweep);

            WearRocksHitThisSwing();
        }

        _currentPhase = "탈진";
        CollapseBoss();
        _cycleRoutine = null;

        if (stayDown) yield break;

        yield return new WaitForSeconds(collapseDuration);
        StandUp();
    }

    private IEnumerator MoveToCenterRoutine()
    {
        yield return MoveBossRoutine(GetCenterStandPosition(), entryDuration, 1.5f);
    }

    // 이번 회차를 시작할 자리다. 제자리 회차는 중앙이고, 이동 회차는 플레이어 반대편에서 이동 각도의 절반만큼 비낀 곳이다.
    private Vector3 GetSwingStartPosition(float signedSweep)
    {
        Vector3 center = GetCenterStandPosition();
        if (Mathf.Approximately(signedSweep, 0f) || playerController == null) return center;

        Vector3 awayFromPlayer = center - playerController.transform.position;
        awayFromPlayer.y = 0f;
        awayFromPlayer = awayFromPlayer.sqrMagnitude > 0.0001f ? awayFromPlayer.normalized : Vector3.forward;

        return center + Quaternion.AngleAxis(-signedSweep * 0.5f, Vector3.up) * awayFromPlayer * orbitRadius;
    }

    private Vector3 GetCenterStandPosition()
    {
        Vector3 c = GetCenter();
        return new Vector3(c.x, standHeightY, c.z);
    }

    // 보스를 목표 위치까지 살짝 포물선으로 옮긴다. Kinematic Rigidbody라 물리 주기에 맞춰 MovePosition을 쓴다.
    private IEnumerator MoveBossRoutine(Vector3 target, float duration, float arcHeight)
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
        yield return new WaitForFixedUpdate();
    }

    // 부서지지 않은 바위 위치에 원 예고를 띄운 뒤 한꺼번에 올린다. 그 자리에 선 플레이어는 밀어낸다.
    private IEnumerator RiseRocksRoutine()
    {
        ClearRockTelegraphs();
        foreach (IslandRock rock in rocks)
        {
            if (rock == null || rock.IsBroken || rock.IsRisen || circleTelegraphPrefab == null) continue;

            GroundTelegraph telegraph = Instantiate(circleTelegraphPrefab);
            telegraph.ShowCircle(rock.FootprintCenter, rock.FootprintRadius + rockTelegraphMargin);
            _rockTelegraphs.Add(telegraph);
        }

        yield return new WaitForSeconds(rockTelegraphDuration);

        foreach (IslandRock rock in rocks)
        {
            if (rock == null || rock.IsBroken || rock.IsRisen) continue;
            PushPlayerOutOf(rock);
            rock.Rise();
        }
        ClearRockTelegraphs();
    }

    // 플레이어를 지정해 전조를 보여 준다. 대부분은 따라가다가 마지막에 방향을 고정한다.
    private IEnumerator TelegraphRoutine(WindSwing swing, float signedSweep)
    {
        _windOrigin = _rb.position;

        if (circleTelegraphPrefab != null)
            _targetMarker = Instantiate(circleTelegraphPrefab);

        PlayWindUpPose(swing);
        if (fanTelegraph != null) fanTelegraph.SetOrbitArrow(_windOrigin, GetCenterStandPosition(), signedSweep);
        UpdateAim(swing);
        if (fanTelegraph != null) fanTelegraph.Show();

        float followTime = Mathf.Max(swing.telegraphDuration - swing.lockDuration, 0f);
        float timer = 0f;
        while (timer < followTime)
        {
            UpdateAim(swing);
            timer += Time.deltaTime;
            yield return null;
        }

        UpdateAim(swing);
        if (_targetMarker != null) _targetMarker.Lock();
        if (fanTelegraph != null) fanTelegraph.PlayLockFlash();

        yield return new WaitForSeconds(swing.lockDuration);
    }

    // 바람 방향을 지금 플레이어 쪽으로 맞추고 예고와 보스의 몸을 함께 돌린다.
    private void UpdateAim(WindSwing swing)
    {
        Vector3 toPlayer = playerController != null
            ? playerController.transform.position - _windOrigin
            : Vector3.back;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
            _windDirection = toPlayer.normalized;
        else if (_windDirection.sqrMagnitude < 0.0001f)
            _windDirection = Vector3.back;

        ApplyWindDirection(swing);

        if (playerController != null)
        {
            _lockedTargetPoint = playerController.transform.position;
            if (_targetMarker != null)
                _targetMarker.ShowCircle(_lockedTargetPoint, targetMarkerRadius);
        }
    }

    // 현재 바람 방향을 예고와 보스의 몸에 반영한다.
    private void ApplyWindDirection(WindSwing swing)
    {
        if (fanTelegraph != null)
            fanTelegraph.UpdateFan(_windOrigin, _windDirection, swing.fanAngle, fanRadius, GetCenter());

        // 루트는 Kinematic Rigidbody라 회전도 MoveRotation으로 맞춘다. 시각 연출은 자식에서 따로 돈다.
        _rb.MoveRotation(Quaternion.LookRotation(_windDirection, Vector3.up));
    }

    // 칼바람: 일정 시간 동안 매 프레임 플레이어가 경로 안인지 확인해서 밀어낸다.
    private IEnumerator WindRoutine(WindSwing swing, float signedSweep)
    {
        PlaySwingPose();
        _rocksHitThisSwing.Clear();

        if (fanTelegraph != null)
        {
            fanTelegraph.ClearOrbitArrow();
            fanTelegraph.DimForWind();
        }
        if (_targetMarker != null)
        {
            Destroy(_targetMarker.gameObject);
            _targetMarker = null;
        }

        PlayWindEffects(swing);

        Vector3 orbitStart = _windOrigin;
        Vector3 center = GetCenterStandPosition();
        bool moves = !Mathf.Approximately(signedSweep, 0f);
        bool damaged = false;
        bool fellInWater = false;
        float elapsed = 0f;

        while (elapsed < swing.windDuration)
        {
            elapsed += Time.deltaTime;

            // 이동 회차: 보스가 섬 중앙을 돌면서 지정한 자리를 향해 계속 분다.
            // 바람 시작점이 움직이므로 바위 뒤 안전 구역이 돌아가고, 플레이어는 바위를 돌며 따라가야 한다.
            if (moves)
            {
                float t = Mathf.Clamp01(elapsed / swing.windDuration);
                _windOrigin = center + Quaternion.AngleAxis(signedSweep * t, Vector3.up) * (orbitStart - center);
                _rb.MovePosition(_windOrigin);

                Vector3 toLocked = _lockedTargetPoint - _windOrigin;
                toLocked.y = 0f;
                if (toLocked.sqrMagnitude > 0.0001f)
                    _windDirection = toLocked.normalized;
            }

            ApplyWindDirection(swing);
            AimWindParticles();
            CollectRocksInWind(swing);

            if (!fellInWater)
            {
                if (IsPlayerInWater())
                {
                    fellInWater = true;
                    ReleasePlayer();
                }
                else if (IsPlayerInWind(swing))
                {
                    Vector3 away = playerController.transform.position - _windOrigin;
                    away.y = 0f;
                    playerController.SetWindZone(away.normalized * windPushSpeed);

                    if (!damaged)
                    {
                        damaged = true;
                        PlayerHealth.Instance?.TakeDamage(windDamage);
                    }
                }
                else
                {
                    ReleasePlayer();
                }
            }

            yield return null;
        }

        ReleasePlayer();
        StopWindEffects();
        if (fanTelegraph != null) fanTelegraph.Hide();
    }

    // 지금 부채꼴 안에 들어온 솟은 바위를 기록한다. 플레이어가 어디 서느냐에 따라 다른 바위도 함께 깎인다.
    private void CollectRocksInWind(WindSwing swing)
    {
        foreach (IslandRock rock in rocks)
        {
            if (rock == null || rock.IsBroken || !rock.IsRisen) continue;

            Vector3 toRock = rock.FootprintCenter - GetFlat(_windOrigin);
            if (Vector3.Angle(_windDirection, toRock) <= swing.fanAngle * 0.5f)
                _rocksHitThisSwing.Add(rock);
        }
    }

    private void WearRocksHitThisSwing()
    {
        foreach (IslandRock rock in _rocksHitThisSwing)
            rock.TakeWindHit();
        _rocksHitThisSwing.Clear();
    }

    // 플레이어가 부채꼴 안에 있고 바위에 가려지지 않았는지 확인한다. 점프 중에도 밀린다.
    private bool IsPlayerInWind(WindSwing swing)
    {
        if (playerController == null) return false;

        Vector3 toPlayer = playerController.transform.position - _windOrigin;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > fanRadius) return false;
        if (Vector3.Angle(_windDirection, toPlayer) > swing.fanAngle * 0.5f) return false;

        return fanTelegraph == null || !fanTelegraph.IsBlockedByRock(_windOrigin, playerController.transform.position);
    }

    private bool IsPlayerInWater()
    {
        if (playerController == null) return false;

        CharacterController cc = playerController.GetComponent<CharacterController>();
        float feetY = cc != null ? cc.bounds.min.y : playerController.transform.position.y;
        return feetY < fallY;
    }

    private void ReleasePlayer()
    {
        if (playerController != null) playerController.ClearWindZone();
    }

    private void PlayWindEffects(WindSwing swing)
    {
        if (sideViewCamera != null) sideViewCamera.Shake(windShakeDuration, swing.shakeStrength);
        if (audioSource != null && windClip != null) audioSource.PlayOneShot(windClip);

        if (windParticles != null)
        {
            ParticleSystem.ShapeModule shape = windParticles.shape;
            shape.angle = swing.fanAngle * 0.5f;
            AimWindParticles();
            windParticles.Play();
        }
    }

    private void AimWindParticles()
    {
        if (windParticles == null) return;

        windParticles.transform.SetPositionAndRotation(
            _windOrigin + Vector3.up * 0.2f,
            Quaternion.LookRotation(_windDirection, Vector3.up));
    }

    private void StopWindEffects()
    {
        if (windParticles != null) windParticles.Stop();
    }

    // 몸을 뒤로 젖히는 전조 자세다. 임시 메시라 몸 전체를 기울여 표현한다.
    private void PlayWindUpPose(WindSwing swing)
    {
        if (bossVisual == null) return;

        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation * Quaternion.Euler(windUpAngle, 0f, 0f), swing.telegraphDuration * 0.6f)
                .SetEase(Ease.OutQuad))
            .SetLink(gameObject);
    }

    // 앞으로 크게 휘두른 뒤 바람이 부는 동안 그 자세를 유지한다.
    private void PlaySwingPose()
    {
        if (bossVisual == null) return;

        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation * Quaternion.Euler(swingAngle, 0f, 0f), 0.12f)
                .SetEase(Ease.OutBack))
            .Join(bossVisual.DOPunchScale(Vector3.one * 0.3f, 0.35f, 6, 0.6f))
            .SetLink(gameObject);
    }

    // 바람이 끝나면 곧바로 쓰러지고 무적이 풀린다. 쓰러져 있는 동안은 기본 공격도 하지 않는다.
    private void CollapseBoss()
    {
        CleanupVisuals();
        SinkRocks();

        // 높이는 시각 오브젝트가 아니라 루트로 낮춘다. Enemy의 피격 연출이 맞을 때마다 시각 오브젝트의
        // 로컬 위치를 원래대로 되돌리기 때문에, 자식을 내리면 첫 타격에 위로 튀어 오른다.
        // 쓰러져 있는 동안은 Kinematic을 유지해 땅속으로 떨어지거나 맞아서 밀리지 않게 한다.
        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(standHeightY - collapseDropY, collapseMoveDuration).SetEase(collapseEase))
            .SetLink(gameObject);

        if (bossVisual != null)
        {
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation * Quaternion.Euler(collapseEuler), collapseMoveDuration)
                .SetEase(collapseEase));
        }

        if (sideViewCamera != null) sideViewCamera.Shake(0.3f, 0.3f);

        _boss.NotifyPatternSolved(stayDown ? float.PositiveInfinity : collapseDuration);
    }

    private void StandUp()
    {
        _currentPhase = "대기";

        _poseSequence?.Kill();
        _poseSequence = DOTween.Sequence()
            .Append(_rb.DOMoveY(standHeightY, collapseMoveDuration).SetEase(Ease.OutQuad))
            .OnComplete(RestorePhysics)
            .SetLink(gameObject);

        if (bossVisual != null)
            _poseSequence.Join(bossVisual.DOLocalRotateQuaternion(_visualStartLocalRotation, collapseMoveDuration).SetEase(Ease.OutQuad));
    }

    private void ResetVisualPose()
    {
        _poseSequence?.Kill();
        if (bossVisual == null) return;
        bossVisual.localPosition = _visualStartLocalPosition;
        bossVisual.localRotation = _visualStartLocalRotation;
    }

    private void PushPlayerOutOf(IslandRock rock)
    {
        if (playerController == null) return;

        Vector3 offset = playerController.transform.position - rock.FootprintCenter;
        offset.y = 0f;
        if (offset.magnitude > rock.FootprintRadius + rockTelegraphMargin) return;

        Vector3 dir = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.right;
        playerController.SetBlast(dir * rockPushSpeed);
    }

    private void SinkRocks()
    {
        foreach (IslandRock rock in rocks)
        {
            if (rock != null) rock.Sink();
        }
    }

    private void CleanupVisuals()
    {
        if (fanTelegraph != null) fanTelegraph.HideImmediate();
        if (windParticles != null) windParticles.Stop();
        if (_targetMarker != null)
        {
            Destroy(_targetMarker.gameObject);
            _targetMarker = null;
        }
        ClearRockTelegraphs();
    }

    private void ClearRockTelegraphs()
    {
        foreach (GroundTelegraph telegraph in _rockTelegraphs)
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
        }
        _rockTelegraphs.Clear();
    }

    private void RestorePhysics()
    {
        if (_rb == null) return;
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }

    private static Vector3 GetFlat(Vector3 position)
    {
        return new Vector3(position.x, 0f, position.z);
    }

    private Vector3 GetCenter()
    {
        Vector3 c = centerPoint != null ? centerPoint.position : Vector3.zero;
        return new Vector3(c.x, 0f, c.z);
    }

    [Button("칼바람만 바로 시작 (Play Mode 테스트)")]
    private void TestStartWithoutBoss()
    {
        if (Application.isPlaying && !IsRunning) StartCycle();
    }
}
