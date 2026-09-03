using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹"의 여름 패턴(비 → 중앙 인력 → 레이저)을 담당한다. Boss.OnPatternTriggered(Summer)를
// 구독해 시작하고, 상공 중앙으로 상승해 고정 호버링한 채로 비→인력→레이저 사이클을 파훼될 때까지
// 반복한다. 레이저 단계에서 대포알(BossCannonball)에 맞아 저지되면(InterruptLaser) 즉시 물리를
// 복원해 자유낙하시키고 Boss.NotifyPatternSolved()로 그로기(무방비 시간)에 들어간다.
//
// 별도의 "제어된 하강" 로직이 필요 없는 이유: 보스가 호버링하는 지점(hoverPosition)이 정확히
// Platform_TopCenter(x=0) 바로 위라서, 자유낙하만 시켜도 자연스럽게 그 발판 위로 떨어지기 때문이다.
// 그로기 중에는 Boss.IsInvincible이 이미 false라서 플레이어의 기존 근접 공격이 그대로 유효하다.
//
// 상승/호버링은 BossFlightMovement/BossSpringPattern과 동일한 물리 예외(Rigidbody 직접 제어)를
// 재사용한다: 상승 중과 호버링 내내 isKinematic=true/useGravity=false로 두어 제자리에 고정하고,
// 대포알에 저지당하는 순간에만 물리를 되돌려 중력으로 떨어지게 한다.
[RequireComponent(typeof(Boss), typeof(Rigidbody))]
public class BossSummerPattern : MonoBehaviour
{
    // BossCannon이 GameObject.Find 없이 "지금 레이저가 활성 상태인지" 확인할 수 있도록
    // TorchTool.IsTorchLit과 동일한 방식으로 정적으로 노출한다.
    public static bool IsLaserActive { get; private set; }

    [Title("연결")]
    [SerializeField, LabelText("Player Transform")]
    private Transform playerTransform;

    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;
    // 인력(SetWindZone/ClearWindZone)을 우산 게이트 없이 직접 걸기 위해 필요하다.

    [SerializeField, LabelText("대포")]
    private BossCannon cannon;

    [SerializeField, LabelText("레이저 빔 프리팹")]
    private BossLaserBeam laserBeamPrefab;

    [SerializeField, LabelText("빗방울 프리팹")]
    private GameObject rainDropPrefab;
    // 기존 RainDrop 프리팹을 그대로 재사용한다(우산 방어+시즌 게이지 로직 그대로).

    [SerializeField, LabelText("바람 모으기 소리")]
    private AudioSource pullWindAudioSource;
    // Inspector에서 "Wind of gathering Sound" 클립이 연결된 AudioSource를 지정한다.
    // 인력(중앙으로 끌어모으는) 단계 동안에만 재생된다.

    [Title("상승/호버링 설정")]
    [SerializeField, LabelText("호버링 위치 (X, Y)")]
    private Vector2 hoverPosition = new Vector2(0f, 9.75f);
    // 맵 중앙(x=0) 상공이다. Platform_TopCenter(x=0)의 바로 위라서, 저지당해 자유낙하하면
    // 자연스럽게 그 발판 위에 떨어진다.

    [SerializeField, LabelText("상승 속도")]
    private float ascendSpeed = 6f;

    [Title("비 단계 설정")]
    [SerializeField, LabelText("비 지속시간")]
    private float rainDuration = 3.5f;

    [SerializeField, LabelText("빗방울 생성 간격")]
    private float rainDropInterval = 0.15f;

    [SerializeField, LabelText("비 스폰 범위 절반 너비")]
    private float rainSpawnHalfWidth = 30f;

    [SerializeField, LabelText("빗방울 스폰 높이")]
    private float rainSpawnY = 11.25f;

    [Title("인력 단계 설정")]
    [SerializeField, LabelText("인력 지속시간")]
    private float pullDuration = 5f;

    [SerializeField, LabelText("인력 세기")]
    private float pullStrength = 9f;
    // 플레이어 기본 이동속도(5)보다 확실히 커야 걸어서 벗어날 수 없고, 선풍기 충전+블라스트
    // 타이밍으로만 탈출할 수 있다(사용자 피드백: 인력이 약해 그냥 걸어나갈 수 있었음).

    [SerializeField, LabelText("대포 견인 이동 시간")]
    private float cannonPullDuration = 1.2f;
    // 인력 단계가 시작되면 대포가 이 시간 동안 stopX까지 한 번에 슈욱 이동한다(매 프레임 조금씩 X).

    [Title("레이저 단계 설정")]
    [SerializeField, LabelText("레이저 제한시간")]
    private float laserDuration = 5f;
    // 대포(맵 왼쪽 끝)까지 달려가서 발사할 시간을 고려해 여유 있게 잡는다.
    // 실제 체감 여유 시간은 BossLaserBeam의 경고 시간까지 더해진다(레이저가 뜨는 순간부터
    // IsLaserActive가 true라 경고 중에도 대포로 달려가고 발사할 수 있다).

    [SerializeField, LabelText("레이저 스폰 Y (지면 높이)")]
    private float laserSpawnY = 0f;
    // Floor의 실제 윗면(중심 y=-0.5 + 두께 절반 0.5 = 0)에 맞춘 값이다. 기존에 0.5(보스가 착지하는
    // 높이 = 바닥면 + 보스 콜라이더 반높이)를 그대로 가져다 써서 빔이 바닥보다 0.5만큼 떠 있었다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 단계")]
    private string _currentPhase = "대기";

    private Boss _boss;
    private Rigidbody _rb;
    private Coroutine _cycleRoutine;
    private BossLaserBeam _activeLaser;
    private Tween _pullSoundTween;

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _rb = GetComponent<Rigidbody>();
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

        IsLaserActive = false;
        playerController?.ClearWindZone();
        StopPullSound();

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
        }
    }

    private void HandlePatternTriggered(Boss.SeasonPattern pattern)
    {
        if (pattern != Boss.SeasonPattern.Summer) return;
        if (_cycleRoutine != null) return;

        _cycleRoutine = StartCoroutine(SummerCycleRoutine());
    }

    private IEnumerator SummerCycleRoutine()
    {
        yield return AscendRoutine();

        while (_boss.IsInvincible)
        {
            yield return RainPhaseRoutine();
            if (!_boss.IsInvincible) break;

            yield return PullPhaseRoutine();
            if (!_boss.IsInvincible) break;

            yield return LaserPhaseRoutine();
            // InterruptLaser()가 호출되면 그 안에서 곧바로 Boss.NotifyPatternSolved()를 호출해
            // Boss.IsInvincible이 false가 되므로, while 조건이 다음 반복에서 자연스럽게 끝난다.
        }

        _currentPhase = "대기";
        _cycleRoutine = null;
    }

    // 맵 중앙 상공(hoverPosition)까지 날아올라 고정 호버링한다. 도착 후에도 isKinematic을 계속
    // 켜둬서 중력 없이 제자리에 고정되게 한다 — 사이클 내내 보스가 스스로 움직이지 않기 때문이다.
    private IEnumerator AscendRoutine()
    {
        _currentPhase = "상승";
        _boss.SetFlying(true); // 공중에 있는 동안은 기본 근접/원거리 공격을 쉰다.

        Vector3 destination = new Vector3(hoverPosition.x, hoverPosition.y, transform.position.z);

        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = false;
        _rb.isKinematic = true;

        while (Vector3.Distance(_rb.position, destination) > 0.05f)
        {
            Vector3 next = Vector3.MoveTowards(_rb.position, destination, ascendSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator RainPhaseRoutine()
    {
        _currentPhase = "비";

        float timer = 0f;
        float dropTimer = 0f;

        while (timer < rainDuration)
        {
            dropTimer += Time.deltaTime;
            if (dropTimer >= rainDropInterval)
            {
                dropTimer = 0f;
                SpawnRainDrop();
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void SpawnRainDrop()
    {
        if (rainDropPrefab == null) return;

        float randomX = Random.Range(-rainSpawnHalfWidth, rainSpawnHalfWidth);
        Vector3 pos = new Vector3(randomX, rainSpawnY, transform.position.z);
        Instantiate(rainDropPrefab, pos, Quaternion.identity);
    }

    // 매 프레임 플레이어와 대포를 동시에 중앙 방향으로 끌어당긴다. 플레이어는 우산 게이트 없이
    // 항상 적용되고(WindZoneVolume과 다른 점), 대포는 왼쪽 끝(BossCannon.stopX)에서 스스로 멈춘다.
    private IEnumerator PullPhaseRoutine()
    {
        _currentPhase = "인력";
        PlayPullSound();
        cannon?.PullToStop(cannonPullDuration);
        // 대포는 매 프레임 조금씩이 아니라 인력 단계 시작 시 한 번에 목표 위치까지 슈욱 이동한다.

        float timer = 0f;
        while (timer < pullDuration)
        {
            if (playerTransform != null && playerController != null)
            {
                float dir = Mathf.Sign(hoverPosition.x - playerTransform.position.x);
                playerController.SetWindZone(new Vector3(dir * pullStrength, 0f, 0f));
            }

            timer += Time.deltaTime;
            yield return null;
        }

        playerController?.ClearWindZone();
        StopPullSound();
    }

    // 인력 단계 시작 시 바람 소리를 서서히 키우며 재생한다.
    private void PlayPullSound()
    {
        if (pullWindAudioSource == null) return;

        _pullSoundTween?.Kill();
        pullWindAudioSource.volume = 0f;
        pullWindAudioSource.Play();
        _pullSoundTween = pullWindAudioSource.DOFade(1f, 0.3f);
    }

    // 인력 단계가 끝나면 바람 소리를 서서히 줄이고 멈춘다.
    private void StopPullSound()
    {
        if (pullWindAudioSource == null) return;

        _pullSoundTween?.Kill();
        _pullSoundTween = pullWindAudioSource.DOFade(0f, 0.3f)
            .OnComplete(() => pullWindAudioSource.Stop());
    }

    // 레이저를 생성하고 저지당하거나 제한시간이 끝날 때까지 대기한다.
    private IEnumerator LaserPhaseRoutine()
    {
        _currentPhase = "레이저";

        if (laserBeamPrefab == null) yield break;

        Vector3 spawnPos = new Vector3(hoverPosition.x, laserSpawnY, transform.position.z);
        _activeLaser = Instantiate(laserBeamPrefab, spawnPos, Quaternion.identity);

        bool? interrupted = null;
        _activeLaser.OnResolved += wasInterrupted => interrupted = wasInterrupted;

        IsLaserActive = true;
        _activeLaser.Activate(laserDuration);

        yield return new WaitUntil(() => interrupted.HasValue);

        IsLaserActive = false;
        _activeLaser = null;
    }

    // 대포알이 보스에 명중했을 때 BossCannonball이 호출한다. 레이저를 즉시 중단시키고
    // 보스를 자유낙하시킨 뒤 그로기(무방비 시간)에 들어가게 한다.
    public void InterruptLaser()
    {
        if (_activeLaser == null) return;

        _activeLaser.Interrupt();

        _boss.SetFlying(false);
        _rb.isKinematic = false;
        _rb.useGravity = true;

        _boss.NotifyPatternSolved();
    }
}
