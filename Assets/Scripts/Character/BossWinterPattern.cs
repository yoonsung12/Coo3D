using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 겨울 패턴("겨울나기")을 담당한다. Boss.OnPatternTriggered(Winter)를
// 구독해 인트로(공중으로 사라짐 → 맵 중앙에 착지 → 냉기 파동)를 재생한 뒤, 플레이어가 1층의
// 온기존(Brazier)에 불을 붙이는 것을 감지하면 그쪽으로 날아가 내려앉아 정지한다. 플레이어가
// 그 위 발판의 고드름(Icicle)을 검으로 떨어뜨려 보스에게 명중시키면 패턴이 파훼되고(Icicle이
// 직접 Boss.ApplyPatternDamage/NotifyPatternSolved 호출), 불이 다 타서 꺼지면 실패로 처리하고
// 다시 감시 상태로 돌아간다(재도전 가능).
//
// 광폭화 중 실제 공격 행동(순찰 돌진/도약 착지/날개짓 광풍 등)은 아직 미정이라 이 클래스에
// 포함하지 않았다 — 감시 루프 안에서 나중에 별도로 연결할 자리만 남겨두었다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossWinterPattern : MonoBehaviour
{
    // Inspector에서 온기존/고드름/유인 착지 지점을 한 세트로 묶어 연결하기 위한 자료구조다.
    [System.Serializable]
    public class LureSpot
    {
        [LabelText("이름")]
        public string label;

        [LabelText("온기존")]
        public Brazier brazier;

        [LabelText("고드름")]
        public Icicle icicle;

        [LabelText("보스 유인 착지 지점 (X, Y)")]
        public Vector2 lurePosition;
        // 이 온기존 바로 위 발판 하단의 고드름과 X좌표가 일치해야, 고드름이 떨어졌을 때 보스에게 맞는다.
    }

    [Title("유인 지점 연결")]
    [SerializeField, LabelText("온기존/고드름 목록")]
    private List<LureSpot> lureSpots = new List<LureSpot>();

    [Title("인트로 연출 설정")]
    [SerializeField, LabelText("맵 중앙 착지 지점 (X, Y)")]
    private Vector2 centerLandingPoint = new Vector2(0f, 1f);
    // 기본값은 Boss.LandingPoints의 "Floor" 지점과 동일하다. 실제 씬 좌표에 맞춰 조절한다.

    [SerializeField, LabelText("떠오르며 사라지는 높이")]
    private float vanishRiseHeight = 2f;

    [SerializeField, LabelText("사라짐 연출 시간")]
    private float vanishDuration = 0.3f;

    [SerializeField, LabelText("사라진 채 이동하는 시간")]
    private float hiddenDuration = 0.4f;
    // 화면 밖으로 사라진 상태로 맵 중앙까지 "이동하는" 느낌을 주기 위한 대기 시간이다.

    [SerializeField, LabelText("착지 펀치(순간 확대) 세기")]
    private float landPunchStrength = 0.25f;

    [SerializeField, LabelText("착지 펀치 지속시간")]
    private float landPunchDuration = 0.3f;

    [SerializeField, LabelText("냉기 충격파(링+균열)")]
    private BossColdShockwave coldShockwave;
    // 착지 순간 동심원 링과 사방으로 뻗는 얼음 균열을 재생한다. 비워두면 생략된다.

    [SerializeField, LabelText("착지 카메라 흔들림 세기")]
    private float landShakeStrength = 0.4f;

    [SerializeField, LabelText("착지 카메라 흔들림 시간")]
    private float landShakeDuration = 0.25f;

    [Title("유인 이동 설정")]
    [SerializeField, LabelText("유인 비행 속도")]
    private float lureFlightSpeed = 6f;

    [SerializeField, LabelText("도착 판정 거리")]
    private float arriveThreshold = 0.05f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("겨울 패턴 진행 중")]
    private bool _isActive;

    [ReadOnly, ShowInInspector, LabelText("현재 유인된 지점")]
    private string _currentLureLabel = "-";

    private Boss _boss;
    private Rigidbody _rb;
    private Renderer[] _visualRenderers;
    private Coroutine _routine;
    private Tween _visualTween;

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _rb = GetComponent<Rigidbody>();
        _visualRenderers = GetComponentsInChildren<Renderer>();
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

        StopRoutine();
        SetVisualsVisible(true);
        RestorePhysics();
        ResetAllIcicles();
        ExtinguishAllBraziers();
        _isActive = false;
        _currentLureLabel = "-";
    }

    private void OnDestroy()
    {
        _visualTween?.Kill();
    }

    private void HandlePatternTriggered(Boss.SeasonPattern pattern)
    {
        if (pattern != Boss.SeasonPattern.Winter) return;
        if (_isActive) return;

        _isActive = true;
        ResetAllIcicles();
        // 겨울 패턴은 실제 게임에서 한 번만 발동되지만, 만약을 위해(재도전/재발동 등) 시작할 때마다
        // 온기존도 전부 꺼진 상태로 되돌려 깨끗하게 시작한다.
        ExtinguishAllBraziers();
        _routine = StartCoroutine(WinterRoutine());
    }

    // 파훼 성공(Icicle이 직접 NotifyPatternSolved를 호출) 시 호출된다. 감시 루프를 즉시 정리한다.
    private void HandlePatternEnded()
    {
        if (!_isActive) return;

        _isActive = false;
        _currentLureLabel = "-";
        StopRoutine();
        RestorePhysics();
    }

    private void StopRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }

    private IEnumerator WinterRoutine()
    {
        yield return StartCoroutine(PlayIntroRoutine());

        // 광폭화 중 실제 공격 행동은 별도 컴포넌트가 나중에 이 루프 안에 연결할 예정이다.
        // 지금은 온기존 점화 여부만 계속 감시한다.
        while (_isActive)
        {
            LureSpot lit = FindLitSpot();
            if (lit != null)
                yield return StartCoroutine(HandleLureAttempt(lit));
            else
                yield return null;
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        Vector3 vanishPoint = transform.position + Vector3.up * vanishRiseHeight;

        // 위로 살짝 떠오르며 사라진다.
        yield return transform.DOMove(vanishPoint, vanishDuration).SetEase(Ease.OutQuad).WaitForCompletion();
        SetVisualsVisible(false);

        yield return new WaitForSeconds(hiddenDuration);

        // 화면 밖에서 맵 중앙으로 위치를 옮긴다(안 보이는 상태라 순간이동이 자연스럽다).
        Vector3 center = new Vector3(centerLandingPoint.x, centerLandingPoint.y, transform.position.z);
        _rb.position = center;
        transform.position = center;
        _rb.linearVelocity = Vector3.zero;

        // 쿵 착지하며 다시 나타난다 + 냉기 파동.
        SetVisualsVisible(true);

        _visualTween?.Kill();
        _visualTween = transform.DOPunchScale(Vector3.one * landPunchStrength, landPunchDuration, 6, 0.5f);

        // 착지 충격으로 냉기가 양옆으로 퍼지며 발판 하단에 고드름이 얼어붙어 생성되는 순간이다.
        SpawnAllIcicles();

        // 동심원 링과 사방으로 뻗는 얼음 균열로 "쾅" 착지의 충격파를 표현한다.
        if (coldShockwave != null) coldShockwave.Play();

        SideViewCamera cam = Camera.main != null ? Camera.main.GetComponent<SideViewCamera>() : null;
        if (cam != null) cam.Shake(landShakeDuration, landShakeStrength);
    }

    private LureSpot FindLitSpot()
    {
        foreach (LureSpot spot in lureSpots)
        {
            if (spot.brazier != null && spot.brazier.IsLit)
                return spot;
        }
        return null;
    }

    private IEnumerator HandleLureAttempt(LureSpot spot)
    {
        _currentLureLabel = spot.label;

        Vector3 destination = new Vector3(spot.lurePosition.x, spot.lurePosition.y, transform.position.z);
        yield return StartCoroutine(FlyTo(destination));

        if (!_isActive) yield break; // 비행 중 파훼되어 끝났으면 더 진행하지 않는다.

        // 도착 후 온기존이 꺼질 때까지(=실패) 또는 파훼되어 _isActive가 꺼질 때까지 대기한다.
        while (_isActive && spot.brazier != null && spot.brazier.IsLit)
            yield return null;

        if (_isActive)
        {
            // 여기 도달했다는 건 파훼되지 못하고 불이 다 탄 것이다 — 실패 처리, 고드름을 원위치로
            // 되돌린다. 이미 생성(Spawn)된 상태이므로 다시 숨기지 않고 그대로 보이게 둔다.
            spot.icicle?.ResetIcicle(hide: false);
        }

        _currentLureLabel = "-";
    }

    // BossFlightMovement의 평상시 비행 이동과 동일한 물리 예외(Rigidbody 직접 제어)를 사용한다.
    private IEnumerator FlyTo(Vector3 destination)
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true;

        while (_isActive && Vector3.Distance(_rb.position, destination) > arriveThreshold)
        {
            Vector3 next = Vector3.MoveTowards(_rb.position, destination, lureFlightSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }

        RestorePhysics();
    }

    private void RestorePhysics()
    {
        if (_rb == null) return;

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.linearVelocity = Vector3.zero;
    }

    private void SetVisualsVisible(bool visible)
    {
        foreach (Renderer r in _visualRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    // 겨울 패턴을 새로 시작하기 전(HandlePatternTriggered)이나 컴포넌트가 꺼질 때(OnDisable) 호출된다.
    // 아직 보스가 "쾅" 착지하기 전 상태로 되돌리는 것이므로 고드름을 다시 숨긴다.
    private void ResetAllIcicles()
    {
        foreach (LureSpot spot in lureSpots)
        {
            if (spot.icicle != null) spot.icicle.ResetIcicle(hide: true);
        }
    }

    // 인트로의 "쾅" 착지 순간에 호출되어, 발판 하단의 고드름들이 동시에 얼어붙어 생성되게 한다.
    private void SpawnAllIcicles()
    {
        foreach (LureSpot spot in lureSpots)
        {
            if (spot.icicle != null) spot.icicle.Spawn();
        }
    }

    private void ExtinguishAllBraziers()
    {
        foreach (LureSpot spot in lureSpots)
        {
            if (spot.brazier != null) spot.brazier.Extinguish();
        }
    }

    [Button("겨울 패턴 강제 발동 테스트")]
    private void TestTrigger() => HandlePatternTriggered(Boss.SeasonPattern.Winter);
}
