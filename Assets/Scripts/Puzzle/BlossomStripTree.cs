using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 꽃이 핀 부서진 나무다. 꽃이 붙어 있는 동안은 투명 막이(blocker)가 길을 막아 넘어갈 수 없고,
// 선풍기 바람으로 꽃을 벗겨내면 막이가 사라져 2단 점프로 줄기를 넘어갈 수 있게 된다.
// "가지만 있는 모델"은 아직 제작 중이므로, 칸이 비어 있으면 모습은 그대로 두고 막는 것만 풀어 준다.
// 나중에 모델이 완성되면 나무의 자식으로 넣고(비활성) 아래 "벗겨진 모델" 칸에 연결하기만 하면 된다.
public class BlossomStripTree : MonoBehaviour, IBlowable
{
    [Title("벗기기 조건")]
    [SerializeField, LabelText("필요 바람 시간(초)")]
    private float requiredBlowTime = 1.5f;
    // 일반 바람을 이 시간만큼 누적해서 맞으면 꽃이 벗겨진다. 바람을 멈춰도 진행도는 줄어들지 않는다.

    [SerializeField, LabelText("블라스트 한 방에 벗기기")]
    private bool stripOnBlast = true;
    // 켜면 선풍기 블라스트(강한 한 방)를 맞는 즉시 벗겨진다.

    [Title("연결")]
    [SerializeField, LabelText("튕김 트리거")]
    private Collider blossomBlocker;
    // 벗기기 전까지 덤불에 올라타거나 넘으려 하면 튕겨내는 트리거(BlossomRepelZone)다. 벗겨지면 꺼진다.

    [SerializeField, LabelText("흔들 비주얼")]
    private Transform visual;
    // 흔들림 연출을 줄 나무 모습 오브젝트다. 콜라이더가 함께 흔들리지 않도록 콜라이더와 분리된 오브젝트를 연결한다.

    [SerializeField, LabelText("꽃 핀 나무 렌더러")]
    private Renderer blossomRenderer;
    // 현재 꽃 핀 나무의 MeshRenderer다. "벗겨진 모델"이 연결되어 있을 때만 벗겨지는 순간 꺼진다.

    [SerializeField, LabelText("꽃 핀 나무 콜라이더들")]
    private Collider[] blossomColliders;
    // 꽃 핀 나무 모양의 콜라이더들이다. "벗겨진 모델"이 자기 콜라이더를 가지고 들어오면 꺼서 겹치지 않게 한다.
    // 벗겨진 모델이 없으면 그대로 둬서 줄기를 계속 밟고 넘어갈 수 있게 한다.

    [SerializeField, LabelText("벗겨진 모델 (제작 중)")]
    private GameObject strippedModel;
    // 가지만 남은 나무 모델이다. 아직 없으면 비워 둔다. 연결할 때는 비활성 상태로 두고, 자체 콜라이더를 붙여 둔다.

    [Title("연출")]
    [SerializeField, LabelText("꽃잎 파티클")]
    private ParticleSystem petalParticle;
    // 바람을 맞는 동안 조금씩, 벗겨지는 순간 크게 꽃잎을 뿌린다. 비워두면 파티클 없이 진행된다.

    [SerializeField, LabelText("바람 맞는 중 초당 꽃잎 수")]
    private float petalsPerSecond = 20f;

    [SerializeField, LabelText("벗겨질 때 꽃잎 수")]
    private int stripBurstCount = 60;

    [SerializeField, LabelText("튕길 때 꽃잎 수")]
    private int repelPetalCount = 15;

    [Title("흔들림 연출 (DOTween)")]
    [SerializeField, LabelText("튕길 때 흔들림 각도")]
    private float repelShakeAngle = 6f;
    // 플레이어를 튕겨낼 때 덤불이 좌우로 기울어지는 최대 각도다. 클수록 크게 출렁인다.

    [SerializeField, LabelText("튕길 때 부풀기 비율")]
    private float repelPunchScale = 0.06f;
    // 튕겨낼 때 덤불이 순간적으로 부풀었다 돌아오는 정도(원래 크기 대비 비율)다.

    [SerializeField, LabelText("바람 맞을 때 흔들림 각도")]
    private float blowShakeAngle = 2f;

    [SerializeField, LabelText("바람 흔들림 간격(초)")]
    private float blowShakeInterval = 0.3f;
    // 바람을 맞는 동안 이 간격마다 살랑 흔들린다. 매 프레임 흔들면 연출이 계속 끊겨 떨림처럼 보인다.

    [SerializeField, LabelText("흔들림 시간")]
    private float shakeDuration = 0.4f;

    [SerializeField, LabelText("벗겨진 모델 등장 시간")]
    private float appearDuration = 0.4f;

    [SerializeField, LabelText("등장 Ease")]
    private Ease appearEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("진행도"), ProgressBar(0, 1)]
    private float Progress => requiredBlowTime > 0f ? Mathf.Clamp01(_blowTime / requiredBlowTime) : 1f;

    [ReadOnly, ShowInInspector, LabelText("벗겨짐")]
    private bool _isStripped;

    private float _blowTime;
    private float _lastCountedFixedTime = -1f;
    // 이 나무에는 콜라이더가 여러 개라 선풍기 판정 한 번에 OnBlown이 여러 번 불린다.
    // 같은 물리 프레임에서는 한 번만 누적하기 위해 마지막으로 센 시간을 기억한다.

    private float _petalCarry;
    private Vector3 _strippedScale;
    private Tween _appearTween;

    private Quaternion _visualBaseRotation;
    private Vector3 _visualBaseScale;
    private Vector3 _swayPunch;
    // DOTween.Punch가 흔드는 값이다. x 성분을 "월드 Z축 기준 기울기 각도"로 사용한다.
    private Sequence _shakeSequence;
    private float _lastBlowShakeTime = -999f;

    public bool IsStripped => _isStripped;
    // BlossomRepelZone이 이미 벗겨졌는지 확인할 때 사용한다.

    private void Awake()
    {
        if (visual != null)
        {
            _visualBaseRotation = visual.rotation;
            _visualBaseScale = visual.localScale;
        }

        if (strippedModel != null)
        {
            _strippedScale = strippedModel.transform.localScale;
            strippedModel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _appearTween?.Kill();
        _shakeSequence?.Kill();
    }

    // BlossomRepelZone이 플레이어를 튕겨낼 때 호출한다. 덤불이 출렁이고 꽃잎이 튀어 "꽃에 막혔다"는 느낌을 준다.
    public void PlayRepelFeedback()
    {
        if (_isStripped) return;

        Shake(repelShakeAngle, repelPunchScale);
        if (petalParticle != null)
            petalParticle.Emit(repelPetalCount);
    }

    // 덤불을 월드 Z축(화면 안쪽 축) 기준으로 좌우로 출렁이게 하고, 필요하면 살짝 부풀게 한다.
    private void Shake(float angle, float scaleRatio)
    {
        if (visual == null) return;

        // Play 전에 에디터 버튼으로 호출되면 Awake가 아직 안 불려 기준값이 비어 있으므로 지금 값을 기준으로 삼는다.
        if (_visualBaseScale == Vector3.zero)
        {
            _visualBaseRotation = visual.rotation;
            _visualBaseScale = visual.localScale;
        }

        // 이전 흔들림이 남아 있으면 끊고 원래 자세에서 다시 시작해 각도가 누적되지 않게 한다.
        _shakeSequence?.Kill();
        visual.rotation = _visualBaseRotation;
        visual.localScale = _visualBaseScale;
        _swayPunch = Vector3.zero;

        // 모델이 X축으로 270도 세워져 있어 오일러 각을 더하면 축이 꼬이므로,
        // 월드 축 회전(AngleAxis)을 원래 회전 앞에 곱하는 방식으로 기울인다.
        _shakeSequence = DOTween.Sequence()
            .Join(DOTween.Punch(() => _swayPunch, v =>
                {
                    _swayPunch = v;
                    visual.rotation = Quaternion.AngleAxis(v.x, Vector3.forward) * _visualBaseRotation;
                }, new Vector3(angle, 0f, 0f), shakeDuration, 8, 0.5f));

        if (scaleRatio > 0f)
            _shakeSequence.Join(visual.DOPunchScale(_visualBaseScale * scaleRatio, shakeDuration, 6, 0.5f));

        // 연출이 끝나면 원래 자세로 정확히 되돌린다.
        _shakeSequence.OnComplete(() =>
        {
            visual.rotation = _visualBaseRotation;
            visual.localScale = _visualBaseScale;
        });
    }

    // FanTool의 바람 판정에 감지되면 호출된다(일반 바람은 쏘는 동안 매 물리 프레임 호출됨).
    public void OnBlown(Vector3 direction, float force, bool impulse = false)
    {
        if (_isStripped) return;

        if (impulse && stripOnBlast)
        {
            Strip();
            return;
        }

        // 같은 물리 프레임에 여러 콜라이더로 중복 호출된 경우는 무시한다.
        if (Mathf.Approximately(Time.fixedTime, _lastCountedFixedTime)) return;
        _lastCountedFixedTime = Time.fixedTime;

        // 일반 바람은 FixedUpdate 주기로 호출되므로 fixedDeltaTime만큼 누적한다.
        _blowTime += Time.fixedDeltaTime;
        EmitBlowPetals();

        // 바람을 맞는 동안 일정 간격으로 살랑 흔들어 "꽃이 바람에 날리고 있다"는 걸 보여준다.
        if (Time.time - _lastBlowShakeTime >= blowShakeInterval)
        {
            _lastBlowShakeTime = Time.time;
            Shake(blowShakeAngle, 0f);
        }

        if (_blowTime >= requiredBlowTime)
            Strip();
    }

    // 바람을 맞는 동안 꽃잎을 조금씩 날린다. 소수점 개수는 다음 호출로 넘겨 초당 개수를 맞춘다.
    private void EmitBlowPetals()
    {
        if (petalParticle == null) return;

        _petalCarry += petalsPerSecond * Time.fixedDeltaTime;
        int count = Mathf.FloorToInt(_petalCarry);
        if (count <= 0) return;

        _petalCarry -= count;
        petalParticle.Emit(count);
    }

    private void Strip()
    {
        _isStripped = true;
        _blowTime = requiredBlowTime;

        // 벗겨지는 순간 크게 한 번 출렁인다.
        Shake(repelShakeAngle, repelPunchScale);

        // 튕김 트리거를 꺼서 2단 점프로 줄기를 넘어갈 수 있게 한다.
        if (blossomBlocker != null)
            blossomBlocker.enabled = false;

        if (petalParticle != null)
            petalParticle.Emit(stripBurstCount);

        // 가지만 있는 모델이 아직 없으면 모습은 그대로 둔다(게임 진행만 가능하게).
        if (strippedModel == null) return;

        if (blossomRenderer != null)
            blossomRenderer.enabled = false;
        foreach (Collider col in blossomColliders)
            if (col != null) col.enabled = false;

        // 가지 모델이 작은 크기에서 튀어나오듯 나타나게 한다.
        strippedModel.SetActive(true);
        strippedModel.transform.localScale = _strippedScale * 0.8f;
        _appearTween?.Kill();
        _appearTween = strippedModel.transform.DOScale(_strippedScale, appearDuration).SetEase(appearEase);
    }

    [Button("튕김 연출 테스트")]
    private void TestRepelFeedback() => PlayRepelFeedback();

    [Button("벗기기 테스트")]
    private void TestStrip()
    {
        if (!_isStripped) Strip();
    }

    [Button("초기화")]
    private void ResetTree()
    {
        _appearTween?.Kill();
        _shakeSequence?.Kill();
        if (visual != null && _visualBaseScale != Vector3.zero)
        {
            visual.rotation = _visualBaseRotation;
            visual.localScale = _visualBaseScale;
        }
        _isStripped = false;
        _blowTime = 0f;

        if (blossomBlocker != null)
            blossomBlocker.enabled = true;
        if (blossomRenderer != null)
            blossomRenderer.enabled = true;
        foreach (Collider col in blossomColliders)
            if (col != null) col.enabled = true;

        if (strippedModel != null)
        {
            strippedModel.transform.localScale = _strippedScale;
            strippedModel.SetActive(false);
        }
    }
}
