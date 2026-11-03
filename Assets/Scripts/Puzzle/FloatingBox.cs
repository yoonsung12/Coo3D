using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 물 위에 떠서 수위를 따라 오르내리는 발판이다.
// 플레이어가 위에 올라타면 PlayerController의 SetPlatformVelocity 훅을 통해
// 상자와 함께 위아래로 실려 이동하게 한다.
[RequireComponent(typeof(Rigidbody))]
public class FloatingBox : MonoBehaviour
{
    [Title("연결 참조")]
    [SerializeField, LabelText("따라갈 물 웅덩이")]
    private WaterArea waterArea;
    // Inspector에서 이 상자가 수위를 따라갈 WaterArea를 연결한다.

    [SerializeField, LabelText("탑승 감지 콜라이더")]
    private BoxCollider rideTrigger;
    // 이 GameObject에 발판용 BoxCollider(Is Trigger 체크 안 함)와 별도로,
    // 발판 바로 위쪽에 얇게 겹치는 BoxCollider(Is Trigger 체크)를 하나 더 추가해 연결한다.
    // 플레이어가 이 트리거 안에 있는 동안 상자에 탑승한 것으로 간주한다.

    [Title("뜨는 높이")]
    [SerializeField, LabelText("물 위 높이")]
    private float floatHeightOffset = 0f;
    // 상자 기준점(피벗)을 수위보다 이만큼 위에 둔다. 값이 클수록 상자가 물 위로 더 많이 드러난다.
    // 피벗이 모델 가운데에 있는 상자는 0이면 절반이 물에 잠기므로, 모델 높이의 절반 정도를 넣으면 떠 있는 것처럼 보인다.

    [SerializeField, LabelText("따라가는 속도")]
    private float followSpeed = 1.5f;
    // 상자가 수위를 따라 오르내리는 최대 속도(유닛/초)다.
    // 빗방울 하나마다 수위가 순간적으로 뛰어도 상자는 이 속도로 부드럽게 따라가, 탑승한 플레이어가 튕기지 않는다.
    // 값이 클수록 수위를 빨리 따라잡지만, 너무 크면 플레이어가 위로 밀려 뜰 수 있다.

    [Title("연출 설정")]
    [SerializeField, LabelText("차오를 때 출렁임 세기")]
    private float bouncePunch = 0.1f;

    [SerializeField, LabelText("출렁임 시간")]
    private float bounceDuration = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상승 속도")]
    private float _currentRiseSpeed;

    private Rigidbody _rb;
    private PlayerController _riderInTrigger;
    private Tween _bounceTween;

    private float _floatY;
    // 출렁임을 뺀 상자의 "실제" 높이다. 상승 속도는 이 값으로만 계산해 연출 흔들림이 플레이어에게 전달되지 않게 한다.
    private bool _hasFloatY;

    private Vector3 _bounceOffset;
    // DOTween이 흔드는 출렁임 값이다. Transform을 직접 흔들면 MovePosition과 서로 싸우므로, 이 값을 이동 목표에 더하기만 한다.

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        // 물리 힘이 아니라 수위를 직접 따라가는 발판이라 Kinematic으로 설정하고 MovePosition으로 움직인다.

        if (rideTrigger != null)
            rideTrigger.isTrigger = true;
    }

    private void OnDisable()
    {
        // 상자가 비활성화/파괴될 때 OnTriggerExit가 호출되지 않을 수 있으므로,
        // 여기서 직접 플레이어의 발판 속도를 정리해 탑승 중이던 플레이어가
        // 허공에 붕 뜬 채로 갇히는 것을 방지한다 (WindZoneVolume.OnDisable()과 동일한 패턴).
        _riderInTrigger?.ClearPlatformVelocity();
        _riderInTrigger = null;

        _bounceTween?.Kill();
        // 비활성화되는 순간 출렁임 Tween이 남아 있지 않도록 정리한다.
    }

    private void OnDestroy()
    {
        _bounceTween?.Kill();
    }

    private void FixedUpdate()
    {
        if (waterArea == null) return;

        float targetY = waterArea.CurrentWaterY + floatHeightOffset;
        // 수위에 "물 위 높이"를 더해 상자가 물에 덜 잠기고 떠 있는 것처럼 보이게 한다.

        if (!_hasFloatY)
        {
            _floatY = targetY;
            _hasFloatY = true;
            // 첫 프레임에는 예전처럼 바로 수면 위치로 맞춘다.
        }

        float previousY = _floatY;
        _floatY = Mathf.MoveTowards(_floatY, targetY, followSpeed * Time.fixedDeltaTime);
        // 수위로 순간이동하지 않고 "따라가는 속도"만큼씩 다가간다. FixedUpdate 한 번에 움직이는 양이므로 fixedDeltaTime을 곱한다.

        // 이번 FixedUpdate에서 실제 높이가 움직인 거리를 속도(유닛/초)로 환산해 저장한다.
        // 출렁임은 빼고 계산하므로, 탑승한 플레이어는 상자가 실제로 오르는 만큼만 같이 올라간다.
        _currentRiseSpeed = (_floatY - previousY) / Time.fixedDeltaTime;

        Vector3 pos = _rb.position;
        pos.y = _floatY + _bounceOffset.y;
        _rb.MovePosition(pos);
        // 출렁임 값은 보이는 위치에만 더한다.

        if (_floatY > previousY + 0.001f)
            PlayRiseBounce();

        if (_riderInTrigger != null)
            _riderInTrigger.SetPlatformVelocity(new Vector3(0f, _currentRiseSpeed, 0f));
    }

    // 물이 차올라 상자가 올라갈 때 살짝 출렁이는 느낌을 주는 연출이다.
    private void PlayRiseBounce()
    {
        if (_bounceTween != null && _bounceTween.IsActive()) return;

        _bounceTween = DOTween.Punch(() => _bounceOffset, x => _bounceOffset = x, Vector3.up * bouncePunch, bounceDuration, 4, 0.5f)
            .OnKill(() => _bounceOffset = Vector3.zero);
        // Transform 대신 출렁임 값만 흔든다. 끝나거나 끊기면 값을 0으로 돌려 상자가 제자리에 오게 한다.
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _riderInTrigger = player;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null || player != _riderInTrigger) return;

        _riderInTrigger.ClearPlatformVelocity();
        _riderInTrigger = null;
    }
}
