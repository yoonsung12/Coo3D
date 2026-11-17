using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 적 공격 이펙트 전용 컴포넌트다(스파크, 도약 먼지/트레일, 몸 찌그러짐, 착지 카메라 흔들림).
// NFBTEnemyAI가 공격 시점마다 PlayXxx()를 호출한다.
// 이 컴포넌트가 없는 적은 NFBTEnemyAI가 attackEffect?. 로 호출하므로 이펙트 없이 그대로 동작한다.
public class EnemyAttackEffect : MonoBehaviour
{
    [Title("연결")]
    [InfoBox("루트 오브젝트를 스케일하면 CharacterController/콜라이더 크기도 같이 변해 충돌이 틀어질 수 있습니다. 반드시 메쉬가 있는 모델 자식 Transform을 연결하세요.")]
    [SerializeField, LabelText("몸 비주얼")]
    private Transform bodyVisual;
    // Squash/Stretch(찌그러짐) 연출을 줄 대상이다. 비워 두면 이 오브젝트의 transform을 쓴다.

    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;
    // 착지 흔들림을 줄 카메라다. 비워 두면 Awake에서 Camera.main에서 한 번만 찾아온다.

    [SerializeField, LabelText("Player 위치")]
    private Transform playerTransform;
    // 흔들림 거리 체크용이다. 보통 NFBTEnemyAI가 SetPlayerTransform으로 넣어 주므로 비워 둔다.
    // 끝까지 비어 있으면 거리를 알 수 없어 카메라를 흔들지 않는다.

    [TabGroup("Fx", "쪼기")]
    [SerializeField, LabelText("쪼기 스파크")]
    private ParticleSystem peckSpark;
    // 쪼는 순간 터지는 스파크 파티클이다(AttackPoint 위치에 둔다).

    [TabGroup("Fx", "쪼기")]
    [SerializeField, LabelText("스파크 추가 지연")]
    private float peckExtraDelay = 0f;
    // 스파크는 EnemyCombat의 히트박스가 켜지는 순간(부리가 앞으로 나가는 순간)에 터진다.
    // 애니메이션과 미세하게 어긋나 보이면 이 값(초)만큼 더 늦춘다. 0이면 히트박스와 동시에 터진다.

    [TabGroup("Fx", "쪼기")]
    [SerializeField, LabelText("머리 펀치 세기")]
    private Vector3 headPunchStrength = new Vector3(0.15f, -0.15f, 0.15f);
    // 쪼는 순간 몸이 순간적으로 커졌다 돌아오는 정도다(원래 스케일 대비 비율, 0.15 = 15%). 값이 클수록 더 크게 출렁인다.

    [TabGroup("Fx", "쪼기")]
    [SerializeField, LabelText("머리 펀치 시간")]
    private float headPunchDuration = 0.2f;

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("도약 먼지")]
    private ParticleSystem leapDust;
    // 도약 순간 발밑에서 터지는 흙 파티클이다.

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("도약 트레일")]
    private TrailRenderer leapTrail;
    // 공중에 있는 동안 남는 잔상 선이다. 착지하면 꺼진다.

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("착지 링")]
    private ParticleSystem landRing;
    // 착지 지점에서 퍼지는 링 + 흙 튀김 파티클이다.

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("착지 찌그러짐 배율")]
    private Vector3 squashScale = new Vector3(1.25f, 0.7f, 1.25f);
    // 착지 순간 몸이 납작해지는 정도다. Y가 작을수록 더 납작해진다.

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("찌그러짐 시간")]
    private float squashDuration = 0.08f;

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("복원 시간")]
    private float squashRecoverDuration = 0.18f;

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("복원 Ease")]
    private Ease squashRecoverEase = Ease.OutBack;

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("흔들림 거리")]
    private float shakeRange = 12f;
    // Player가 이 거리 안에 있을 때만 착지 때 카메라가 흔들린다. 멀리서 착지한 적 때문에 화면이 흔들리지 않게 한다.

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("흔들림 시간")]
    private float shakeDuration = 0.15f;

    [TabGroup("Fx", "도약")]
    [SerializeField, LabelText("흔들림 세기")]
    private float shakeStrength = 0.15f;

    [TabGroup("Fx", "원거리")]
    [SerializeField, LabelText("백스텝 먼지")]
    private ParticleSystem backstepDust;
    // 뒤로 뛰어오르는 순간 발밑에서 터지는 흙먼지다(GroundCheck 자식에 둔다).

    [TabGroup("Fx", "원거리")]
    [SerializeField, LabelText("백스텝 깃털")]
    private ParticleSystem backstepFeathers;
    // 뛰어오를 때 몸에서 떨어지는 깃털 2~3장이다. 천천히 흩날리며 떨어진다.

    [TabGroup("Fx", "원거리")]
    [SerializeField, LabelText("백스텝 착지 먼지")]
    private ParticleSystem backstepLandDust;
    // 백스텝 착지 때 튀는 작은 흙이다. 도약 착지 링보다 약하게, 링 없이 흙만 쓴다.

    [TabGroup("Fx", "원거리")]
    [SerializeField, LabelText("발사 섬광/깃털")]
    private ParticleSystem rangedMuzzle;
    // 발사 순간 부리 앞에서 터지는 섬광 + 깃털이다(AttackPoint 자식에 둔다). 몸 앞(로컬 +Z)으로 퍼진다.

    [TabGroup("Fx", "원거리")]
    [SerializeField, LabelText("발사 반동 세기")]
    private Vector3 rangedRecoilStrength = new Vector3(0.12f, -0.12f, 0.12f);
    // 발사 순간 몸이 출렁이는 정도다(원래 스케일 대비 비율). 값이 클수록 반동이 크게 보인다.

    [TabGroup("Fx", "원거리")]
    [SerializeField, LabelText("발사 반동 시간")]
    private float rangedRecoilDuration = 0.2f;

    private Vector3 _originalScale;
    // bodyVisual의 원래 스케일이다. 연출이 끊겨도 항상 이 값으로 되돌린다.

    private Tween _bodyTween;
    // 현재 실행 중인 몸 연출(펀치/늘어남/찌그러짐)이다. 겹치지 않도록 새로 시작하기 전에 Kill한다.

    private EnemyCombat _combat;
    // 히트박스가 켜지는 순간을 알려 주는 컴포넌트다. 없는 적이면 쪼기 스파크는 PlayPeck()을 직접 불러야 나온다.

    private Tween _peckDelayTween;
    // "스파크 추가 지연"을 기다리는 DOTween 지연 호출이다. 비활성화될 때 Kill해서 늦게 터지지 않게 한다.

    private void Awake()
    {
        if (bodyVisual == null) bodyVisual = transform;
        _originalScale = bodyVisual.localScale;

        // Camera.main은 호출마다 비용이 들 수 있으므로 Awake에서 한 번만 찾는다.
        if (sideViewCamera == null && Camera.main != null)
            sideViewCamera = Camera.main.GetComponent<SideViewCamera>();

        _combat = GetComponent<EnemyCombat>();
    }

    private void OnEnable()
    {
        // 공격 시작(머리를 뒤로 당기는 순간)이 아니라 히트박스가 켜지는 순간에 스파크를 내기 위해 구독한다.
        if (_combat != null) _combat.OnHitboxEnabled += HandleHitboxEnabled;
    }

    private void HandleHitboxEnabled()
    {
        if (peckExtraDelay <= 0f)
        {
            PlayPeck();
            return;
        }
        _peckDelayTween?.Kill();
        _peckDelayTween = DOVirtual.DelayedCall(peckExtraDelay, PlayPeck, false);
    }

    public void SetPlayerTransform(Transform player) { playerTransform = player; }
    // NFBTEnemyAI가 이미 알고 있는 Player를 넘겨 받아, 착지 흔들림의 거리 계산에 쓴다.

    public void PlayPeck()
    {
        if (peckSpark != null)
        {
            peckSpark.Clear(true);
            peckSpark.Play(true);
        }

        if (bodyVisual == null) return;
        ResetBodyTween();
        // DOPunchScale은 세기를 "절대값"으로 더한다. 모델 스케일이 0.02처럼 작으면 0.15를 그대로 더할 때 몸이 몇 배로 커지므로
        // 원래 스케일에 비율로 곱해서 "원래 크기의 15%만큼" 출렁이게 한다.
        _bodyTween = bodyVisual.DOPunchScale(Vector3.Scale(_originalScale, headPunchStrength), headPunchDuration);
    }

    public void PlayLeapStart()
    {
        if (leapDust != null)
        {
            leapDust.Clear(true);
            leapDust.Play(true);
        }
        if (leapTrail != null)
        {
            leapTrail.Clear();
            leapTrail.emitting = true;
        }
    }

    public void PlayLeapLand()
    {
        if (leapTrail != null) leapTrail.emitting = false;
        if (landRing != null)
        {
            landRing.Clear(true);
            landRing.Play(true);
        }

        if (bodyVisual != null)
        {
            ResetBodyTween();
            Vector3 squashed = Vector3.Scale(_originalScale, squashScale);
            _bodyTween = DOTween.Sequence()
                .Append(bodyVisual.DOScale(squashed, squashDuration))
                .Append(bodyVisual.DOScale(_originalScale, squashRecoverDuration).SetEase(squashRecoverEase));
        }

        ShakeCameraIfPlayerNear();
    }

    // 원거리 공격의 백스텝 점프 순간이다. 발밑 먼지와 깃털만 터뜨린다(몸 연출은 점프 애니메이션이 담당).
    public void PlayBackstepJump()
    {
        RestartParticle(backstepDust);
        RestartParticle(backstepFeathers);
    }

    // 백스텝 착지 순간이다. 도약 착지보다 약하게 작은 흙만 튄다.
    public void PlayBackstepLand()
    {
        RestartParticle(backstepLandDust);
    }

    // 발사 순간이다. 섬광/깃털 + 몸 반동(쪼기와 같은 DOPunchScale 방식)을 준다.
    public void PlayRangedFire()
    {
        RestartParticle(rangedMuzzle);

        if (bodyVisual == null) return;
        ResetBodyTween();
        // 쪼기와 같은 이유로 모델 스케일(0.02)에 비율을 곱해 "원래 크기 대비" 반동을 만든다.
        _bodyTween = bodyVisual.DOPunchScale(Vector3.Scale(_originalScale, rangedRecoilStrength), rangedRecoilDuration);
    }

    private static void RestartParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Clear(true);
        ps.Play(true);
    }

    private void ShakeCameraIfPlayerNear()
    {
        if (sideViewCamera == null || playerTransform == null) return;
        if (Vector3.Distance(transform.position, playerTransform.position) > shakeRange) return;
        sideViewCamera.Shake(shakeDuration, shakeStrength);
    }

    // 진행 중인 몸 연출을 끊고 스케일을 원래대로 되돌린다.
    // 되돌리지 않으면 연출 도중 끊긴 스케일이 그대로 남아 몸이 찌그러진 채 굳는다.
    private void ResetBodyTween()
    {
        _bodyTween?.Kill();
        _bodyTween = null;
        if (bodyVisual != null) bodyVisual.localScale = _originalScale;
    }

    private void StopAllEffects()
    {
        ResetBodyTween();
        if (leapTrail != null) leapTrail.emitting = false;
    }

    private void OnDisable()
    {
        if (_combat != null) _combat.OnHitboxEnabled -= HandleHitboxEnabled;
        _peckDelayTween?.Kill();
        StopAllEffects();
    }

    private void OnDestroy() { StopAllEffects(); }

    [Button("쪼기 테스트")]
    private void TestPeck()
    {
        if (!Application.isPlaying) return;
        PlayPeck();
    }

    [Button("도약 시작 테스트")]
    private void TestLeapStart()
    {
        if (!Application.isPlaying) return;
        PlayLeapStart();
    }

    [Button("착지 테스트")]
    private void TestLeapLand()
    {
        if (!Application.isPlaying) return;
        PlayLeapLand();
    }

    [Button("백스텝 점프 테스트")]
    private void TestBackstepJump()
    {
        if (!Application.isPlaying) return;
        PlayBackstepJump();
    }

    [Button("백스텝 착지 테스트")]
    private void TestBackstepLand()
    {
        if (!Application.isPlaying) return;
        PlayBackstepLand();
    }

    [Button("원거리 발사 테스트")]
    private void TestRangedFire()
    {
        if (!Application.isPlaying) return;
        PlayRangedFire();
    }
}
