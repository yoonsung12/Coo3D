using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 선풍기 바람으로 돌리는 발전 터빈이다. 일정 시간 바람을 맞으면(또는 블라스트 한 방) 작동을 시작해
// 날개가 계속 돌고, 연결된 DarkZone의 전원을 켜고, 지하 조명들을 순서대로 하나씩 켠다.
// 한 번 작동하면 다시 꺼지지 않는다.
// FanTool은 바람 판정에 맞은 콜라이더에서 IBlowable을 찾으므로, 이 컴포넌트와 콜라이더는 같은 오브젝트에 둔다.
// 회전은 자식인 "회전체"만 하므로 콜라이더(이 오브젝트)는 돌지 않는다.
[RequireComponent(typeof(Collider))]
public class TurbineGenerator : MonoBehaviour, IBlowable
{
    [Title("작동 조건")]
    [SerializeField, LabelText("필요 바람 시간(초)")]
    private float requiredBlowTime = 2f;
    // 일반 바람을 이 시간만큼 누적해서 맞으면 작동한다. 바람을 멈춰도 진행도는 줄어들지 않는다.

    [SerializeField, LabelText("블라스트 한 방에 작동")]
    private bool activateOnBlast = true;
    // 켜면 선풍기 블라스트(충전 후 강한 한 방)를 맞는 즉시 작동한다.

    [Title("연결")]
    [SerializeField, LabelText("회전체")]
    private Transform rotor;
    // 실제로 돌아갈 오브젝트다. 회전 중심이 허브 중심에 오도록 만든 빈 오브젝트를 연결하고,
    // 터빈 메쉬는 그 자식으로 둔다.

    [SerializeField, LabelText("어두운 구역")]
    private DarkZone darkZone;
    // 작동하면 전원을 켤 DarkZone이다. 비워두면 조명만 켠다.

    [SerializeField, LabelText("켤 조명들 (켜지는 순서대로)")]
    private Light[] lights;
    // 작동하면 위에서부터 순서대로 켜질 조명들이다. 씬에서는 꺼진 상태(오브젝트 비활성)로 둬도 되고 켜 둬도 된다.
    // 각 조명의 Inspector Intensity 값이 켜졌을 때의 목표 밝기가 된다.

    [Title("회전 연출 (DOTween)")]
    [SerializeField, LabelText("회전축 (회전체 로컬 기준)")]
    private Vector3 spinAxis = Vector3.up;
    // 날개가 도는 축이다. Turbine 모델은 로컬 Y축이 회전축(허브 방향)이다.

    [SerializeField, LabelText("초당 회전수")]
    private float rotationsPerSecond = 1.5f;
    // 작동 후 최고 속도다. 값이 클수록 날개가 빠르게 돈다.

    [SerializeField, LabelText("바람 맞는 중 회전 비율"), Range(0f, 1f)]
    private float blowSpinRatio = 0.25f;
    // 작동 전 바람을 맞는 동안 최고 속도의 몇 배로 돌지 정한다. "조금씩 돌기 시작한다"는 피드백이다.

    [SerializeField, LabelText("가속 시간")]
    private float spinUpDuration = 1.5f;
    // 작동한 뒤 최고 속도에 도달하기까지 걸리는 시간이다. 클수록 서서히 빨라진다.

    [SerializeField, LabelText("가속 Ease")]
    private Ease spinUpEase = Ease.InQuad;

    [SerializeField, LabelText("작동 펀치 크기")]
    private float activatePunch = 0.08f;
    // 작동하는 순간 회전체가 살짝 커졌다 돌아오는 정도(현재 크기 대비 비율)다. 0이면 생략한다.

    [Title("조명 연출 (DOTween)")]
    [SerializeField, LabelText("조명 시작 지연(초)")]
    private float lightStartDelay = 0.8f;
    // 터빈이 돌기 시작한 뒤 첫 조명이 켜지기까지 기다리는 시간이다. 가속이 어느 정도 된 뒤 켜지는 느낌을 준다.

    [SerializeField, LabelText("조명 사이 간격(초)")]
    private float lightInterval = 0.25f;
    // 조명이 하나씩 켜지는 간격이다. 클수록 천천히 차례대로 켜진다.

    [SerializeField, LabelText("조명 켜지는 시간")]
    private float lightFadeDuration = 0.3f;

    [SerializeField, LabelText("조명 Ease")]
    private Ease lightEase = Ease.OutFlash;
    // OutFlash는 켜지는 순간 살짝 번쩍이는 느낌을 준다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("진행도"), ProgressBar(0, 1)]
    private float Progress => requiredBlowTime > 0f ? Mathf.Clamp01(_blowTime / requiredBlowTime) : 1f;

    [ReadOnly, ShowInInspector, LabelText("작동 중")]
    private bool _isRunning;

    private float _blowTime;
    private float _lastBlowTime = -999f;
    // 마지막으로 바람을 맞은 시간이다. 바람이 끊겼는지 판단해 회전을 멈추는 데 쓴다.

    private float _lastCountedFixedTime = -1f;
    // 같은 물리 프레임에 OnBlown이 여러 번 불려도 한 번만 누적하기 위해 기억한다.

    private bool _isBlowSpinning;
    // 작동 전, 바람 때문에 천천히 돌고 있는 중인지 여부다.

    private float[] _lightTargetIntensities;
    // 각 조명의 원래 Intensity(켜졌을 때의 목표 밝기)를 시작할 때 저장해둔다.

    private Tween _spinTween;
    // 무한 반복 회전 Tween이다. timeScale을 조절해 속도를 바꾼다(0이면 멈춤).
    private Tween _speedTween;
    private Tween _punchTween;
    private Sequence _lightSequence;

    private const float BlowTimeout = 0.15f;
    // 이 시간 동안 바람을 안 맞으면 바람이 끊긴 것으로 본다. 일반 바람은 매 물리 프레임 호출되므로 짧게 잡는다.

    private void Awake()
    {
        // 조명은 처음에 꺼 두고, 켜졌을 때의 밝기만 기억한다.
        _lightTargetIntensities = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;

            _lightTargetIntensities[i] = lights[i].intensity;
            lights[i].intensity = 0f;
            lights[i].gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (rotor == null) return;

        // 한 바퀴(360도)를 1/rotationsPerSecond초에 돌고 Incremental 루프로 계속 이어서 돈다.
        // 처음에는 timeScale 0으로 멈춰 두고, 바람을 맞거나 작동하면 timeScale을 올려 속도를 낸다.
        _spinTween = rotor
            .DOLocalRotate(spinAxis.normalized * 360f, 1f / Mathf.Max(0.01f, rotationsPerSecond), RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
        _spinTween.timeScale = 0f;
    }

    private void OnDestroy()
    {
        // 씬 전환 등으로 파괴될 때 남은 Tween이 파괴된 Transform/Light에 접근하지 않게 정리한다.
        _spinTween?.Kill();
        _speedTween?.Kill();
        _punchTween?.Kill();
        _lightSequence?.Kill();
    }

    private void Update()
    {
        // 작동 전, 바람이 끊기면 천천히 멈춘다. (바람을 맞는 동안에는 OnBlown이 계속 시간을 갱신한다.)
        if (_isRunning || !_isBlowSpinning) return;
        if (Time.time - _lastBlowTime < BlowTimeout) return;

        _isBlowSpinning = false;
        SetSpinSpeed(0f, 0.6f, Ease.OutQuad);
    }

    // FanTool의 바람 판정에 감지되면 호출된다(일반 바람은 쏘는 동안 매 물리 프레임 호출됨).
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (_isRunning) return;

        if (impulse && activateOnBlast)
        {
            Activate();
            return;
        }

        // 같은 물리 프레임에 중복 호출된 경우는 무시한다.
        if (Mathf.Approximately(Time.fixedTime, _lastCountedFixedTime)) return;
        _lastCountedFixedTime = Time.fixedTime;
        _lastBlowTime = Time.time;

        // 일반 바람은 FixedUpdate 주기로 호출되므로 fixedDeltaTime만큼 누적한다.
        _blowTime += Time.fixedDeltaTime;

        // 바람을 맞기 시작한 순간 날개가 천천히 돌기 시작해 "바람으로 돌릴 수 있다"는 걸 보여준다.
        if (!_isBlowSpinning)
        {
            _isBlowSpinning = true;
            SetSpinSpeed(blowSpinRatio, 0.4f, Ease.OutQuad);
        }

        if (_blowTime >= requiredBlowTime)
            Activate();
    }

    // 터빈을 작동시킨다. 최고 속도로 가속하고, 어두운 구역의 전원을 켜고, 조명을 차례대로 켠다.
    private void Activate()
    {
        if (_isRunning) return;

        _isRunning = true;
        _isBlowSpinning = false;

        SetSpinSpeed(1f, spinUpDuration, spinUpEase);

        if (rotor != null && activatePunch > 0f)
        {
            _punchTween?.Kill();
            _punchTween = rotor.DOPunchScale(rotor.localScale * activatePunch, 0.4f, 6, 0.5f);
        }

        if (darkZone != null)
            darkZone.PowerOn();

        PlayLightSequence();
    }

    // 회전 Tween의 timeScale을 목표 비율까지 부드럽게 바꿔 속도를 조절한다. (1 = 최고 속도, 0 = 정지)
    private void SetSpinSpeed(float ratio, float duration, Ease ease)
    {
        if (_spinTween == null) return;

        _speedTween?.Kill();
        _speedTween = DOTween.To(() => _spinTween.timeScale, x => _spinTween.timeScale = x, ratio, duration)
            .SetEase(ease);
    }

    // 조명을 배열 순서대로 하나씩 켠다. 각 조명은 0에서 원래 밝기까지 짧게 밝아진다.
    private void PlayLightSequence()
    {
        _lightSequence?.Kill();
        _lightSequence = DOTween.Sequence().AppendInterval(lightStartDelay);

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null) continue;

            float target = _lightTargetIntensities[i];
            // 순서대로 간격을 두고 켜지도록, i번째 조명은 i × 간격 시점에 시작한다.
            float startTime = lightStartDelay + i * lightInterval;

            _lightSequence.InsertCallback(startTime, () => light.gameObject.SetActive(true));
            _lightSequence.Insert(startTime, light.DOIntensity(target, lightFadeDuration).SetEase(lightEase));
        }
    }

    [Button("터빈 작동 테스트 (Play Mode)")]
    private void TestActivate()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[TurbineGenerator] Play Mode에서만 테스트할 수 있습니다."); return; }
        Activate();
    }
}
