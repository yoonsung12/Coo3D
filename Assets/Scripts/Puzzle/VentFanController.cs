using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 벽 버튼(PressButton)을 누르면 바닥 환풍기가 돌기 시작하고, 위쪽 바람 구역(WindZoneVolume)을 켜는 퍼즐 장치다.
// 기본은 한 번 켜지면 계속 작동한다(버튼을 영구 잠금). "누르는 동안만 작동"을 켜면 버튼이 눌린 동안에만 돈다.
// 플레이어는 바람 위에서 우산을 펼쳐 위로 올라간다.
// 실제로 플레이어를 띄우는 힘은 기존 WindZoneVolume이 처리하고, 이 컴포넌트는 켜는 시점과 연출만 담당한다.
public class VentFanController : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("작동 버튼")]
    private PressButton switchButton;
    // Inspector에서 환풍기를 켤 벽 버튼(PressButton)을 연결한다.

    [SerializeField, LabelText("바람 구역")]
    private WindZoneVolume windZone;
    // Inspector에서 환풍기 위쪽의 WindZoneVolume을 연결한다. 시작할 때 꺼 두었다가 버튼을 누르면 켠다.
    // (바람 방향 (0,1,0)의 수직풍이어야 우산을 펼친 플레이어가 떠오른다.)

    [SerializeField, LabelText("누르는 동안만 작동")]
    private bool holdToRun;
    // 끄면(기본값) 버튼을 한 번 밟는 순간 버튼이 잠기고 환풍기가 계속 돈다.
    // 켜면 버튼이 눌려 있는 동안에만 바람이 나오고, 버튼에서 벗어나면 서서히 멈춘다.
    // (플레이어가 직접 밟고 있으면 바람을 탈 수 없으므로, 상자(WoodenBox)를 올려두는 퍼즐에 쓴다.)

    [SerializeField, LabelText("시작하자마자 작동")]
    private bool runOnStart;
    // 켜면 버튼 없이 씬이 시작될 때부터 계속 돈다. 항상 이용할 수 있어야 하는 복귀용 환풍기에 쓴다.

    [SerializeField, LabelText("회전할 날개")]
    private Transform fanBlade;
    // Inspector에서 실제로 돌아갈 환풍기 날개 모델을 연결한다.

    [Title("회전 연출 (DOTween)")]
    [SerializeField, LabelText("회전축 (날개 로컬 기준)")]
    private Vector3 spinAxis = Vector3.forward;
    // 날개 모델이 도는 축이다. Turbine 모델은 로컬 Z축이 회전축이다.

    [SerializeField, LabelText("초당 회전수")]
    private float rotationsPerSecond = 2f;
    // 값이 클수록 날개가 빠르게 돈다.

    [SerializeField, LabelText("가속 시간")]
    private float spinUpDuration = 1.2f;
    // 버튼을 누른 뒤 최고 속도에 도달하기까지 걸리는 시간이다. 클수록 서서히 빨라진다.

    [SerializeField, LabelText("가속 Ease")]
    private Ease spinUpEase = Ease.InQuad;

    [SerializeField, LabelText("작동 펀치 크기")]
    private float activatePunch = 0.15f;
    // 켜지는 순간 환풍기가 살짝 커졌다 돌아오는 정도(현재 크기 대비 비율)다. 0이면 생략한다.

    [SerializeField, LabelText("감속 시간"), ShowIf("holdToRun")]
    private float spinDownDuration = 0.8f;
    // 버튼에서 벗어난 뒤 날개가 완전히 멈추기까지 걸리는 시간이다. 클수록 천천히 멈춘다.

    [SerializeField, LabelText("감속 Ease"), ShowIf("holdToRun")]
    private Ease spinDownEase = Ease.OutQuad;

    [Title("작동 효과")]
    [SerializeField, LabelText("바람 파티클")]
    private ParticleSystem windParticles;
    // Inspector에서 환풍기가 도는 동안 위로 올라갈 바람 줄기 파티클을 연결한다. 비워두면 파티클 없이 동작한다.
    // WindZoneVolume의 파티클은 우산을 편 플레이어가 구역 안에 있을 때만 나오므로, 환풍기가 켜졌다는 표시는 여기서 따로 재생한다.

    [SerializeField, LabelText("작동 소리 오디오 소스")]
    private AudioSource fanAudioSource;
    // Inspector에서 Loop, 3D 사운드로 설정한 AudioSource를 연결한다. 비워두면 소리 없이 동작한다.

    [SerializeField, LabelText("작동 소리")]
    private AudioClip fanLoopClip;
    // 환풍기가 도는 동안 반복 재생할 소리다.

    [SerializeField, LabelText("작동 소리 볼륨"), Range(0f, 1f)]
    private float fanVolume = 0.6f;
    // 값이 클수록 환풍기 소리가 크게 들린다.

    [SerializeField, LabelText("소리 페이드 시간")]
    private float soundFadeDuration = 0.8f;
    // 소리가 서서히 커지거나 작아지는 데 걸리는 시간이다. 날개 가속/감속 시간과 비슷하게 맞추면 자연스럽다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("작동 중")]
    private bool _isRunning;

    private Tween _spinTween;
    // 무한 반복 회전 Tween이다. timeScale을 0→1로 올려 서서히 빨라지게 만든다.
    private Tween _spinUpTween;
    private Tween _punchTween;
    private Tween _soundFadeTween;
    // 소리 페이드가 겹치지 않도록 현재 실행 중인 페이드를 저장한다.
    private Vector3 _originalScale;
    // 누르는 동안만 작동 모드에서는 펀치가 여러 번 실행되므로, 펀치 도중 끊겨도 크기가 틀어지지 않게 원래 크기를 기억한다.

    private void Awake()
    {
        _originalScale = transform.localScale;

        // 버튼을 누르기 전에는 바람이 나오지 않게 끈다.
        // (꺼진 컴포넌트도 트리거 진입은 기록하므로, 바람 안에 서 있다가 켜져도 바로 떠오른다.)
        if (windZone != null)
            windZone.enabled = false;
    }

    private void Start()
    {
        if (switchButton != null)
            switchButton.OnStateChanged += OnButtonStateChanged;

        // Awake에서 바람을 끈 뒤에 켜야 하므로 Start에서 처리한다.
        if (runOnStart)
            Activate();
    }

    private void OnDestroy()
    {
        if (switchButton != null)
            switchButton.OnStateChanged -= OnButtonStateChanged;

        _spinTween?.Kill();
        _spinUpTween?.Kill();
        _punchTween?.Kill();
        _soundFadeTween?.Kill();
        // 씬 전환 등으로 파괴될 때 남은 Tween이 파괴된 Transform에 접근하지 않게 정리한다.
    }

    // 버튼 상태가 바뀔 때 호출된다.
    // 기본 모드: 처음 눌린 순간 한 번만 환풍기를 켠다.
    // 누르는 동안만 작동 모드: 눌리면 켜고, 다시 올라오면(Active) 끈다.
    private void OnButtonStateChanged(PressButton button)
    {
        bool pressed = button.State == PressButton.ButtonState.Pressed;

        if (pressed && !_isRunning)
            Activate();
        else if (holdToRun && !pressed && _isRunning)
            Deactivate();
    }

    // 환풍기를 켠다. 바람과 회전 연출을 시작한다.
    private void Activate()
    {
        _isRunning = true;

        // 기본 모드에서는 플레이어가 버튼에서 떨어져도 계속 작동하도록 버튼을 영구 잠금 상태로 만든다.
        if (!holdToRun && switchButton != null)
            switchButton.Lock();

        if (windZone != null)
            windZone.enabled = true;

        StartSpin();
        PlayRunEffects();
    }

    // 환풍기를 끈다(누르는 동안만 작동 모드 전용). 바람을 끊고 날개를 서서히 멈춘다.
    private void Deactivate()
    {
        _isRunning = false;

        // WindZoneVolume을 끄면 OnDisable에서 플레이어에게 주던 바람 힘과 파티클/사운드를 함께 정리한다.
        if (windZone != null)
            windZone.enabled = false;

        StopSpin();
        StopRunEffects();
    }

    // 환풍기가 켜졌음을 알리는 바람 줄기와 작동 소리를 시작한다.
    private void PlayRunEffects()
    {
        if (windParticles != null)
            windParticles.Play();

        if (fanAudioSource == null || fanLoopClip == null) return;

        _soundFadeTween?.Kill();

        // 페이드아웃 도중 다시 켜진 경우엔 처음부터 재생하지 않고 현재 볼륨에서 다시 키운다.
        if (!fanAudioSource.isPlaying)
        {
            fanAudioSource.clip = fanLoopClip;
            fanAudioSource.loop = true;
            fanAudioSource.volume = 0f;
            fanAudioSource.Play();
        }

        _soundFadeTween = fanAudioSource.DOFade(fanVolume, soundFadeDuration);
    }

    // 바람 줄기 생성을 멈추고 소리를 서서히 줄인다. 이미 나온 파티클은 끝까지 올라간 뒤 사라진다.
    private void StopRunEffects()
    {
        if (windParticles != null)
            windParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (fanAudioSource == null) return;

        _soundFadeTween?.Kill();
        _soundFadeTween = fanAudioSource.DOFade(0f, soundFadeDuration)
            .OnComplete(fanAudioSource.Stop);
    }

    // 날개를 무한 회전시키고, 회전 Tween의 timeScale을 올려 서서히 빨라지게 한다.
    private void StartSpin()
    {
        if (fanBlade == null) return;

        _spinUpTween?.Kill();

        // 감속 중에 다시 눌린 경우엔 회전 Tween을 새로 만들지 않고, 현재 속도에서 그대로 다시 가속한다.
        // (새로 만들면 날개 각도는 이어지지만 속도가 0으로 뚝 떨어졌다 다시 올라가 어색하다.)
        if (_spinTween == null || !_spinTween.IsActive())
        {
            // 한 바퀴(360도)를 1/rotationsPerSecond초에 돌고, Incremental 루프로 계속 이어서 돈다.
            _spinTween = fanBlade
                .DOLocalRotate(spinAxis.normalized * 360f, 1f / Mathf.Max(0.01f, rotationsPerSecond), RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);
            _spinTween.timeScale = 0f;
        }

        _spinUpTween = DOTween.To(() => _spinTween.timeScale, x => _spinTween.timeScale = x, 1f, spinUpDuration)
            .SetEase(spinUpEase);

        if (activatePunch > 0f)
        {
            _punchTween?.Kill();
            transform.localScale = _originalScale;
            _punchTween = transform.DOPunchScale(_originalScale * activatePunch, 0.4f, 6, 0.5f);
        }
    }

    // 회전 Tween의 timeScale을 0까지 내려 서서히 멈춘다. 다 멈추면 회전 Tween은 남겨두고(일시정지 상태) 재가속에 재사용한다.
    private void StopSpin()
    {
        if (_spinTween == null || !_spinTween.IsActive()) return;

        _spinUpTween?.Kill();
        _spinUpTween = DOTween.To(() => _spinTween.timeScale, x => _spinTween.timeScale = x, 0f, spinDownDuration)
            .SetEase(spinDownEase);
    }

    [Button("환풍기 작동 테스트 (Play Mode)")]
    private void TestActivate()
    {
        if (!Application.isPlaying || _isRunning) return;
        Activate();
    }

    [Button("환풍기 정지 테스트 (Play Mode)"), ShowIf("holdToRun")]
    private void TestDeactivate()
    {
        if (!Application.isPlaying || !_isRunning) return;
        Deactivate();
    }
}
