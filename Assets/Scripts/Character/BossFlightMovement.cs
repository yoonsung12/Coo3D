using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 평상시 이동을 담당한다. 전투 시작부터 일정 주기마다 현재 위치가 아닌
// 다른 착지 지점(Boss.LandingPoints)으로 자동으로 날아간다. 계절 패턴 진행 중(무적)과 파훼 직후
// 무방비 시간에는 이동 타이머가 멈춘다 — 그 동안의 보스 이동은 각 패턴 컨트롤러(BossSpringPattern 등)가
// 직접 담당하기 때문이다.
//
// 이동 방식은 BossSpringPattern의 돌진과 동일한 물리 예외다: Boss의 Rigidbody를 직접 MovePosition으로
// 움직이고, 이동 중에는 useGravity를 끄고 isKinematic을 켜서 발판에 막히지 않고 목표 지점까지
// 관통하도록 한 뒤, 도착하면(또는 이동 중 계절 패턴이 발동하면) 즉시 원래 상태로 복구한다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossFlightMovement : MonoBehaviour
{
    [Title("비행 이동 설정")]
    [SerializeField, LabelText("이동 주기 (초)")]
    private float moveInterval = 5f;
    // 이 시간마다 자동으로 다른 착지 지점으로 날아간다.

    [SerializeField, LabelText("비행 속도")]
    private float flightSpeed = 6f;

    [SerializeField, LabelText("도착 판정 거리")]
    private float arriveThreshold = 0.05f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("다음 이동까지 남은 시간")]
    private float _timer;

    private Boss _boss;
    private Rigidbody _rb;
    private Coroutine _flightRoutine;
    private bool _patternBlocking;
    // 계절 패턴이 진행 중이거나(무적) 파훼 직후 무방비 시간일 때 true가 된다.
    // 이동 타이머는 멈추고, 이미 시작된 비행도 도중에 중단하고 제어권을 넘겨준다.

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _rb = GetComponent<Rigidbody>();
        _timer = moveInterval;
    }

    private void OnEnable()
    {
        _boss.OnPatternTriggered += HandlePatternTriggered;
        _boss.OnPatternEnded += HandlePatternEnded;
    }

    private void OnDisable()
    {
        _boss.OnPatternTriggered -= HandlePatternTriggered;
        _boss.OnPatternEnded -= HandlePatternEnded;

        if (_flightRoutine != null)
        {
            StopCoroutine(_flightRoutine);
            _flightRoutine = null;
        }

        _boss.SetFlying(false);

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
        }
    }

    private void HandlePatternTriggered(Boss.SeasonPattern pattern) => _patternBlocking = true;

    // 파훼 성공 후 무방비 시간까지 끝나야(=기본 패턴으로 완전히 복귀해야) 이동 타이머를 재개한다.
    private void HandlePatternEnded() => _patternBlocking = false;

    private void Update()
    {
        if (_boss.IsDead) return;
        if (_patternBlocking) return;
        if (_flightRoutine != null) return; // 이미 비행 중이면 새로 트리거하지 않는다.

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = moveInterval;
        TryStartFlight();
    }

    private void TryStartFlight()
    {
        Vector3 destination = PickRandomLandingPoint();
        _flightRoutine = StartCoroutine(FlightRoutine(destination));
    }

    // Boss.LandingPoints 중 현재 위치와 거의 같은 지점을 제외하고 무작위로 하나를 고른다.
    // 후보가 전부 제외되는 극단적인 경우(착지 지점이 1개뿐인 등)엔 현재 위치를 그대로 반환한다.
    private Vector3 PickRandomLandingPoint()
    {
        Vector3 current = transform.position;
        List<Vector3> candidates = new List<Vector3>();

        foreach (Boss.LandingPoint point in _boss.LandingPoints)
        {
            Vector3 world = new Vector3(point.position.x, point.position.y, current.z);
            if (Vector3.Distance(world, current) > arriveThreshold)
                candidates.Add(world);
        }

        if (candidates.Count == 0) return current;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private IEnumerator FlightRoutine(Vector3 destination)
    {
        _boss.SetFlying(true);

        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true; // 돌진과 동일하게 플랫폼에 막히지 않고 관통해서 이동한다.

        // 목표에 도착하거나, 이동 중 계절 패턴이 발동하면(_patternBlocking) 즉시 멈춘다.
        while (!_patternBlocking && Vector3.Distance(_rb.position, destination) > arriveThreshold)
        {
            Vector3 next = Vector3.MoveTowards(_rb.position, destination, flightSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.linearVelocity = Vector3.zero;

        _boss.SetFlying(false);
        _flightRoutine = null;
    }

    [Button("즉시 리포지션 (테스트)")]
    private void TestForceFlight()
    {
        if (_flightRoutine != null || _patternBlocking) return;
        _timer = moveInterval;
        TryStartFlight();
    }
}
