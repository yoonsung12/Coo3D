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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        // 물리 힘이 아니라 수위를 직접 따라가는 발판이라 Kinematic으로 설정하고 MovePosition으로 움직인다.

        if (rideTrigger != null)
            rideTrigger.isTrigger = true;
    }

    private void OnDestroy()
    {
        _bounceTween?.Kill();
    }

    private void FixedUpdate()
    {
        if (waterArea == null) return;

        float targetY = waterArea.CurrentWaterY;
        Vector3 pos = _rb.position;
        float previousY = pos.y;

        pos.y = targetY;
        _rb.MovePosition(pos);

        // 이번 FixedUpdate에서 실제로 움직인 거리를 속도(유닛/초)로 환산해 저장한다.
        // 플레이어가 탑승 중이면 이 값을 그대로 수직 속도로 넘겨받는다.
        _currentRiseSpeed = (targetY - previousY) / Time.fixedDeltaTime;

        if (targetY > previousY + 0.001f)
            PlayRiseBounce();

        if (_riderInTrigger != null)
            _riderInTrigger.SetPlatformVelocity(new Vector3(0f, _currentRiseSpeed, 0f));
    }

    // 물이 차올라 상자가 올라갈 때 살짝 출렁이는 느낌을 주는 연출이다.
    private void PlayRiseBounce()
    {
        if (_bounceTween != null && _bounceTween.IsActive()) return;
        _bounceTween = transform.DOPunchPosition(Vector3.up * bouncePunch, bounceDuration, 4, 0.5f);
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
