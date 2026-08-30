using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 봄 패턴(꽃가루 돌진)을 담당한다. Boss.OnPatternTriggered(Spring)를 구독해
// 시작하고, 조준(라인 추적) → 돌진(가장 가까운 착지 지점으로) → 착지 폭발(플레이어 데미지) →
// 트레일 생성/노후화 소멸 사이클을 파훼될 때까지 반복한다. 체력/무적/페이즈 골격은 Boss.cs를
// 그대로 쓰고, 여기서는 봄 패턴 전용 이동/연출만 담당한다.
//
// 돌진 이동은 EnemyMovement.Move()(좌우 전용)로 표현할 수 없는 대각선/2층 이동이 필요해서,
// 이 컴포넌트가 Boss의 Rigidbody를 직접 MovePosition으로 움직인다 — CharacterController/Rigidbody
// 물리 이동 규칙의 예외다. 돌진 중에는 useGravity를 끄고 isKinematic을 켜서(플랫폼에 막히지 않고
// 목표 착지 지점까지 관통하도록) 직선 궤적을 유지하며, 돌진이 끝나면 즉시 원래 상태로 복구한다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossSpringPattern : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;
    // Boss.cs의 playerTransform과 같은 오브젝트를 연결한다.
    // Boss.cs 쪽 참조는 private이라 이 컴포넌트가 직접 읽을 수 없어 별도로 하나 더 연결해둔다.

    [SerializeField, LabelText("조준선 LineRenderer")]
    private LineRenderer aimLine;
    // Inspector에서 Boss 자식의 AimLine(LineRenderer)을 연결한다.

    [SerializeField, LabelText("돌진 접촉 히트박스")]
    private EnemyAttackHitbox dashHitbox;
    // Inspector에서 Boss 자식의 DashHitbox(EnemyAttackHitbox)를 연결한다. 돌진 중에만 활성화한다.

    [SerializeField, LabelText("꽃가루 트레일 프리팹")]
    private PollenTrail trailPrefab;

    [SerializeField, LabelText("착지 폭발 프리팹")]
    private BossLandingBlast landingBlastPrefab;
    // Inspector에서 Assets/Prefabs/Boss/BossLandingBlast.prefab을 연결한다.

    [Title("조준 설정")]
    [SerializeField, LabelText("조준 지속시간")]
    private float aimDuration = 1.2f;

    [SerializeField, LabelText("조준선 사라지는 시간")]
    private float aimLineFadeDuration = 0.2f;

    [Title("돌진 설정")]
    [SerializeField, LabelText("돌진 속도")]
    private float dashSpeed = 14f;

    [SerializeField, LabelText("돌진 사이클 대기시간")]
    private float postDashDelay = 0.6f;
    // 착지 폭발/트레일을 만든 뒤 다음 조준을 시작하기 전 잠깐 멈추는 시간이다.

    [Title("착지 지점")]
    [SerializeField, LabelText("착지 가능 지점 목록")]
    private List<LandingPoint> landingPoints = new List<LandingPoint>
    {
        new LandingPoint { label = "Floor", position = new Vector2(0f, 0.5f) },
        new LandingPoint { label = "Platform_MidLeft", position = new Vector2(-6.4f, 2.05f) },
        new LandingPoint { label = "Platform_MidRight", position = new Vector2(6.4f, 2.05f) },
        new LandingPoint { label = "Platform_TopCenter", position = new Vector2(0f, 3.35f) },
        new LandingPoint { label = "Platform_TopFarLeft", position = new Vector2(-12.8f, 3.35f) },
        new LandingPoint { label = "Platform_TopFarRight", position = new Vector2(12.8f, 3.35f) },
    };
    // 돌진은 항상 이 목록 중 lockedTarget과 가장 가까운 지점을 향해 이동한다.
    // 각 좌표는 BossArena 씬의 바닥/발판 윗면 + 보스 몸(BoxCollider 반높이 0.5)을 더한 착지 높이다.

    // Inspector에서 착지 지점을 알아보기 쉽게 이름표를 붙이기 위한 자료구조다.
    [System.Serializable]
    private class LandingPoint
    {
        [LabelText("이름")]
        public string label;

        [LabelText("좌표 (X, Y)")]
        public Vector2 position;
    }

    [Title("트레일 설정")]
    [SerializeField, LabelText("동시 유지 트레일 최대 개수")]
    private int maxAliveTrails = 3;
    // 새 트레일이 이 개수를 넘기면 가장 오래된 트레일부터 사라진다("돌진 3회 지나면 소멸"과 같은 결과).

    [Title("폭발 판정 설정")]
    [SerializeField, LabelText("폭발 판정 반경")]
    private float explosionRadius = 2f;

    [SerializeField, LabelText("보스 데미지")]
    private float explosionDamage = 15f;

    [SerializeField, LabelText("점화 후 폭발 지연")]
    private float explodeDelay = 0.4f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    [ReadOnly, ShowInInspector, LabelText("생존 트레일 수")]
    private int _aliveTrailCount;

    private Boss _boss;
    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private readonly List<PollenTrail> _trails = new List<PollenTrail>();
    private Vector3 _lockedTarget;

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _rb = GetComponent<Rigidbody>();

        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }

        if (dashHitbox != null)
            dashHitbox.DisableHitbox();
    }

    private void OnEnable()
    {
        _boss.OnPatternTriggered += HandlePatternTriggered;
    }

    private void OnDisable()
    {
        _boss.OnPatternTriggered -= HandlePatternTriggered;

        if (_cycleRoutine != null)
        {
            StopCoroutine(_cycleRoutine);
            _cycleRoutine = null;
        }

        if (dashHitbox != null)
            dashHitbox.DisableHitbox();

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
        }
    }

    private void HandlePatternTriggered(Boss.SeasonPattern pattern)
    {
        if (pattern != Boss.SeasonPattern.Spring) return;
        if (_cycleRoutine != null) return;

        _cycleRoutine = StartCoroutine(SpringCycleRoutine());
    }

    // 조준 → 돌진 → 트레일 생성 사이클을 파훼될 때까지 반복한다.
    // 파훼 여부는 Boss.IsInvincible(공개 프로퍼티)로 판단한다 — PollenTrail의 폭발 판정이
    // 성공하면 Boss.NotifyPatternSolved()가 호출되어 이 값이 false가 된다.
    private IEnumerator SpringCycleRoutine()
    {
        while (_boss.IsInvincible)
        {
            yield return AimPhaseRoutine();
            if (!_boss.IsInvincible) break;

            yield return DashPhaseRoutine();
            if (!_boss.IsInvincible) break;

            SpawnLandingBlast();
            AgeTrailsAfterDash();

            _currentPhase = "대기";
            yield return new WaitForSeconds(postDashDelay);
        }

        _currentPhase = "대기";
        ClearAllTrails();
        _cycleRoutine = null;
    }

    // 조준선이 실시간으로 플레이어를 따라가다가, 조준 시간이 끝나는 순간의 위치를 lockedTarget으로 고정한다.
    private IEnumerator AimPhaseRoutine()
    {
        _currentPhase = "조준";

        if (aimLine != null)
        {
            aimLine.enabled = true;
            aimLine.widthMultiplier = 1f;
        }

        float timer = 0f;
        _lockedTarget = playerTransform != null ? playerTransform.position : transform.position;

        while (_boss.IsInvincible && timer < aimDuration)
        {
            if (playerTransform != null)
            {
                _lockedTarget = playerTransform.position;

                if (aimLine != null)
                {
                    aimLine.SetPosition(0, transform.position);
                    aimLine.SetPosition(1, _lockedTarget);
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (aimLine == null) yield break;

        // 조준선 두께를 0으로 줄이며 사라지는 연출이다. 알파(투명도) 대신 두께를 쓰는 이유는
        // LineRenderer 기본 Material이 Opaque라 알파 변화가 화면에 반영되지 않기 때문이다.
        DOTween.To(() => aimLine.widthMultiplier, w => aimLine.widthMultiplier = w, 0f, aimLineFadeDuration);
        yield return new WaitForSeconds(aimLineFadeDuration);
        aimLine.enabled = false;
    }

    // lockedTarget과 가장 가까운 착지 지점을 향해 현재 위치에서 직선으로 돌진한다.
    // 돌진하는 동안 꽃가루 트레일이 이동 거리만큼 실시간으로 늘어나며 따라와서,
    // 돌진이 끝난 뒤 갑자기 생기는 게 아니라 "흩뿌리며 돌진하는" 느낌을 준다.
    private IEnumerator DashPhaseRoutine()
    {
        _currentPhase = "돌진";

        Vector3 start = transform.position;
        Vector3 destination = ComputeNearestLandingPoint(_lockedTarget, start.z);
        PollenTrail trail = SpawnGrowingTrail(start, destination);

        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true; // 돌진 중에는 플랫폼에 막히지 않고 관통하도록 물리 충돌을 끈다.

        if (dashHitbox != null)
            dashHitbox.EnableHitbox();

        while (_boss.IsInvincible && Vector3.Distance(_rb.position, destination) > 0.05f)
        {
            Vector3 next = Vector3.MoveTowards(_rb.position, destination, dashSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
            UpdateGrowingTrail(trail, start, next);
            yield return new WaitForFixedUpdate();
        }

        UpdateGrowingTrail(trail, start, _rb.position); // 마지막 위치까지 정확히 반영해서 길이를 마무리한다.

        if (dashHitbox != null)
            dashHitbox.DisableHitbox();

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.linearVelocity = Vector3.zero;
    }

    // landingPoints 중 target(보통 lockedTarget)과 가장 가까운 지점을 찾아 월드 좌표로 반환한다.
    // 사이드뷰(X-Y 평면) 기준이라 Z는 돌진을 시작하는 보스의 현재 Z 그대로 고정한다.
    private Vector3 ComputeNearestLandingPoint(Vector3 target, float z)
    {
        LandingPoint nearest = null;
        float nearestDistSqr = float.MaxValue;

        foreach (LandingPoint point in landingPoints)
        {
            float distSqr = ((Vector2)target - point.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = point;
            }
        }

        Vector2 chosen = nearest != null ? nearest.position : (Vector2)target;
        return new Vector3(chosen.x, chosen.y, z);
    }

    // 착지 지점에 짧은 경고 후 자동으로 터지는 폭발을 생성한다. 반경 안 플레이어에게 데미지를 준다.
    private void SpawnLandingBlast()
    {
        if (landingBlastPrefab == null) return;
        Instantiate(landingBlastPrefab, transform.position, Quaternion.identity);
    }

    // 돌진이 끝난 뒤, 기존 트레일들 중 개수 제한을 넘는 것부터 제거한다.
    // 이번 돌진에서 자란 트레일은 이미 SpawnGrowingTrail()에서 _trails에 추가되어 있다.
    private void AgeTrailsAfterDash()
    {
        _trails.RemoveAll(t => t == null); // 점화로 먼저 사라진 트레일은 목록에서 정리한다.

        while (_trails.Count > maxAliveTrails)
        {
            PollenTrail oldest = _trails[0];
            _trails.RemoveAt(0);
            if (oldest != null)
                oldest.FadeOutAndDestroy();
        }

        _aliveTrailCount = _trails.Count;
    }

    // 돌진 시작 지점에 길이 0에 가까운 트레일을 생성한다. 방향(회전)은 목적지 기준으로 미리 고정하고,
    // 실제 길이는 돌진 중 UpdateGrowingTrail()이 매 스텝 갱신한다.
    private PollenTrail SpawnGrowingTrail(Vector3 start, Vector3 destination)
    {
        if (trailPrefab == null) return null;

        Vector3 dir = destination - start;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        PollenTrail trail = Instantiate(trailPrefab, start, Quaternion.Euler(0f, 0f, angle));
        Vector3 baseScale = trail.transform.localScale;
        trail.transform.localScale = new Vector3(0.1f, baseScale.y, baseScale.z);
        trail.Initialize(_boss, explosionRadius, explosionDamage, explodeDelay);

        _trails.Add(trail);
        return trail;
    }

    // 트레일의 길이와 중심 위치를 start~current 구간에 맞춰 매 스텝 다시 계산한다.
    private void UpdateGrowingTrail(PollenTrail trail, Vector3 start, Vector3 current)
    {
        if (trail == null) return;

        Vector3 diff = current - start;
        float length = Mathf.Max(diff.magnitude, 0.1f);
        Vector3 baseScale = trail.transform.localScale;

        trail.transform.position = start + diff * 0.5f;
        trail.transform.localScale = new Vector3(length, baseScale.y, baseScale.z);
    }

    private void ClearAllTrails()
    {
        foreach (PollenTrail trail in _trails)
        {
            if (trail != null)
                trail.FadeOutAndDestroy();
        }

        _trails.Clear();
        _aliveTrailCount = 0;
    }
}
