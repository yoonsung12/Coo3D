using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 사이드뷰 시점에서 플레이어의 X축/Y축 이동을 따라가는 카메라 추적 스크립트다.
// Z는 고정 offset을 유지하고 X/Y는 플레이어 위치 기준 상대 offset을 부드럽게 추적한다.
public class SideViewCamera : MonoBehaviour
{
    [Title("추적 대상")]
    [SerializeField, LabelText("플레이어")]
    private Transform target;
    // Inspector에서 Player 오브젝트의 Transform을 연결한다.

    [Title("카메라 오프셋")]
    [SerializeField, LabelText("X 오프셋")]
    private float offsetX = 0f;
    // 플레이어 X 위치에 더해지는 오프셋이다. 카메라를 좌우로 미세 조정할 때 사용한다.

    [SerializeField, LabelText("Y 오프셋 (높이)")]
    private float offsetY = 2f;
    // 플레이어 Y 위치에 더해지는 상대 오프셋이다(예전엔 고정 절대 높이였으나,
    // 보스 아레나처럼 발판 높이 차가 커지면서 플레이어를 따라 오르내리도록 변경했다).
    // 값이 클수록 플레이어보다 더 높은 곳에서 내려다본다.

    [SerializeField, LabelText("Z 오프셋 (거리)")]
    private float offsetZ = -10f;
    // 카메라가 플레이어로부터 Z축으로 떨어지는 거리다.
    // 음수값이며 절댓값이 클수록 카메라가 더 뒤로 물러서 화면이 넓어진다.
    // "플레이어 Z 따라가기"가 꺼져 있을 때만 사용하는 절대 Z 위치다.

    [SerializeField, LabelText("플레이어 Z 따라가기")]
    private bool followPlayerZ;
    // 켜면 카메라 Z를 플레이어 Z 기준으로 계산한다.
    // 탑다운 구역 안에 사이드뷰 라인(SideViewLaneZone)이 따로 있는 씬처럼, 사이드뷰 Z 라인이 여러 개일 때 켠다.
    // 끄면 기존처럼 Z 오프셋을 절대 위치로 사용한다.

    [SerializeField, LabelText("플레이어와의 Z 거리"), ShowIf(nameof(followPlayerZ))]
    private float sideDistanceZ = -10f;
    // 플레이어 Z에 더해지는 값이다. 음수이며 절댓값이 클수록 카메라가 플레이어로부터 더 멀어진다.
    // SideViewAxisZone으로 진행 축이 바뀐 통로에서는 이 설정과 상관없이 이 값을 통로 옆 거리로 사용한다.

    [SerializeField, LabelText("추적 속도")]
    private float followSpeed = 8f;
    // 카메라가 플레이어를 따라가는 부드러움을 결정한다.
    // 값이 클수록 더 빠르게 따라가고, 작을수록 더 느리게 따라간다.

    [FoldoutGroup("탑다운 설정")]
    [SerializeField, LabelText("플레이어와의 거리")]
    private float topDownDistance = 14f;
    // 탑다운 모드에서 카메라가 플레이어로부터 떨어진 직선 거리다. 클수록 더 넓은 범위가 보인다.

    [FoldoutGroup("탑다운 설정")]
    [SerializeField, LabelText("내려다보는 각도"), Range(30f, 90f)]
    private float topDownPitch = 60f;
    // 90이면 완전히 수직으로 내려다보고(머리만 보임), 작을수록 비스듬히 봐서 캐릭터 옆모습이 더 보인다.

    [FoldoutGroup("전환 연출")]
    [SerializeField, LabelText("전환 시간")]
    private float transitionDuration = 0.6f;
    // 사이드뷰 ↔ 탑다운 전환에 걸리는 시간이다. 너무 짧으면 화면이 확 튀어 어지러울 수 있다.

    [FoldoutGroup("전환 연출")]
    [SerializeField, LabelText("전환 Ease")]
    private Ease transitionEase = Ease.InOutSine;
    // 전환 움직임의 가감속 느낌을 정하는 DOTween Ease 타입이다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 타겟 X")]
    private float _currentTargetX;

    [ReadOnly, ShowInInspector, LabelText("탑다운 전환 정도 (0=사이드, 1=탑다운)")]
    private float _topDownBlend;
    // 0~1 사이 값을 DOTween으로 변화시켜, 두 시점의 위치/회전을 섞어 부드럽게 전환한다.

    private PlayerController _player;
    // 플레이어의 시점 모드 변경 이벤트를 구독하기 위해 target에서 가져온다.

    private Quaternion _sideRotation;
    // 씬에 배치된 사이드뷰 카메라의 원래 회전이다. 사이드뷰로 돌아올 때 이 회전으로 복구한다.

    private Quaternion _currentSideRotation;
    // 지금 진행 축 기준의 사이드뷰 회전이다. 기본 축(+X)이면 _sideRotation과 같고,
    // -Z 통로처럼 축이 바뀌면 _sideRotation을 Y축으로 돌린 값이 된다.

    private Tween _blendTween;

    private Tween _axisTween;
    // 진행 축이 바뀔 때 카메라가 통로 옆으로 돌아가는 회전 연출이다. 다시 바뀌면 끊고 새로 시작한다.

    private Vector3 _shakeOffset;
    // 보스 패턴 폭발 등 임팩트 연출에서 Shake()를 호출하면 이 오프셋이 흔들리며 최종 위치에 더해진다.
    // transform.position을 직접 흔들면 매 프레임 아래 추적 로직이 덮어써서 무효화되므로 별도 필드로 분리했다.

    private Tween _shakeTween;

    private void Start()
    {
        _sideRotation = transform.rotation;
        _currentSideRotation = _sideRotation;

        // 시점 모드는 PlayerController가 관리하고, 카메라는 이벤트를 받아 따라 바뀐다.
        // 이렇게 하면 구역 트리거는 플레이어에게만 알리면 되고 카메라를 따로 연결할 필요가 없다.
        if (target != null)
            _player = target.GetComponent<PlayerController>();
        if (_player != null)
        {
            _player.OnViewModeChanged += HandleViewModeChanged;
            _player.OnSideViewAxisChanged += HandleSideViewAxisChanged;
        }
    }

    // 진행 축이 바뀌면, 원래 사이드뷰 회전을 Y축으로 돌려 새 깊이 방향(진행 축 × 위쪽)을 바라보게 한다.
    // 기본 축(+X)의 깊이 방향은 +Z이므로, +Z에서 새 깊이 방향까지의 각도만큼 돌리면 된다.
    private void HandleSideViewAxisChanged(Vector3 axis)
    {
        Vector3 depthAxis = Vector3.Cross(axis, Vector3.up);
        float yaw = Vector3.SignedAngle(Vector3.forward, depthAxis, Vector3.up);
        Quaternion from = _currentSideRotation;
        Quaternion to = Quaternion.AngleAxis(yaw, Vector3.up) * _sideRotation;
        Quaternion topRotation = Quaternion.Euler(topDownPitch, 0f, 0f);

        _axisTween?.Kill();
        // 통로 입구를 오가며 축이 연달아 바뀌어도 현재 각도에서 새 목표로 자연스럽게 이어지게 이전 Tween을 끊는다.

        _axisTween = DOTween.To(() => 0f, v =>
            {
                _currentSideRotation = Quaternion.Slerp(from, to, v);
                // 탑다운 전환이 섞여 있는 중이면 그 비율도 함께 반영해 두 연출이 서로 덮어쓰지 않게 한다.
                transform.rotation = Quaternion.Slerp(_currentSideRotation, topRotation, _topDownBlend);
            }, 1f, transitionDuration)
            .SetEase(transitionEase);
    }

    private void HandleViewModeChanged(ViewMode mode)
    {
        float targetBlend = mode == ViewMode.TopDown ? 1f : 0f;
        Quaternion topRotation = Quaternion.Euler(topDownPitch, 0f, 0f);

        _blendTween?.Kill();
        // 전환 도중 다시 구역을 드나들어도 현재 값에서 새 목표로 자연스럽게 이어지게 이전 Tween을 끊는다.

        _blendTween = DOTween.To(() => _topDownBlend, v =>
            {
                _topDownBlend = v;
                // 회전은 플레이어 위치와 무관하므로 Tween이 진행되는 동안에만 갱신한다.
                // 전환이 끝난 뒤에는 회전을 건드리지 않아 기존 사이드뷰 동작과 똑같이 유지된다.
                transform.rotation = Quaternion.Slerp(_currentSideRotation, topRotation, v);
            }, targetBlend, transitionDuration)
            .SetEase(transitionEase);
    }

    private void LateUpdate()
    {
        // LateUpdate에서 카메라를 이동시켜 플레이어 이동이 완전히 처리된 뒤에 따라가게 한다.
        if (target == null) return;

        _currentTargetX = target.position.x;

        // X/Y축 모두 플레이어 위치 기준 상대 offset을 따라간다.
        // Z는 기본적으로 고정 위치지만, "플레이어 Z 따라가기"를 켜면 플레이어 Z 기준 거리를 유지해
        // 다른 사이드뷰 라인으로 옮겨가도 카메라가 플레이어 정면에 온다.
        // Lerp로 부드럽게 이동해 갑작스러운 카메라 이동을 방지한다.
        float cameraZ = followPlayerZ ? target.position.z + sideDistanceZ : offsetZ;
        Vector3 targetPosition = new Vector3(
            _currentTargetX + offsetX,
            target.position.y + offsetY,
            cameraZ
        );

        // -Z 통로처럼 진행 축이 바뀐 구간에서는, 플레이어에서 깊이 방향 반대쪽으로 물러난 통로 옆 위치를 쓴다.
        // (기본 축(+X)이면 이 블록을 건너뛰어 위의 기존 계산을 그대로 사용한다.)
        if (_player != null && _player.SideViewAxis != Vector3.right)
        {
            Vector3 axis = _player.SideViewAxis;
            Vector3 depthAxis = Vector3.Cross(axis, Vector3.up);
            targetPosition = target.position + axis * offsetX + Vector3.up * offsetY + depthAxis * sideDistanceZ;
        }

        // 탑다운 전환 중이거나 탑다운 상태면, 플레이어 뒤쪽 위에서 내려다보는 위치와 섞는다.
        // 각도(pitch)와 거리로 위치를 계산하므로 각도를 바꿔도 카메라가 항상 플레이어를 정면으로 바라본다.
        if (_topDownBlend > 0f)
        {
            float pitchRad = topDownPitch * Mathf.Deg2Rad;
            Vector3 topDownPosition = target.position + new Vector3(
                0f,
                Mathf.Sin(pitchRad) * topDownDistance,
                -Mathf.Cos(pitchRad) * topDownDistance
            );
            targetPosition = Vector3.Lerp(targetPosition, topDownPosition, _topDownBlend);
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime) + _shakeOffset;
    }

    private void OnDestroy()
    {
        _shakeTween?.Kill();
        _blendTween?.Kill();
        _axisTween?.Kill();
        // 씬 전환 등으로 카메라가 파괴될 때 남은 전환 Tween이 파괴된 transform에 접근하지 않게 정리한다.

        if (_player != null)
        {
            _player.OnViewModeChanged -= HandleViewModeChanged;
            _player.OnSideViewAxisChanged -= HandleSideViewAxisChanged;
        }
    }

    // 보스 패턴 폭발 등 임팩트가 필요한 순간에 외부(예: PollenTrail)에서 호출한다.
    public void Shake(float duration, float strength)
    {
        _shakeTween?.Kill();
        _shakeOffset = Vector3.zero;

        _shakeTween = DOTween.Shake(() => _shakeOffset, v => _shakeOffset = v, duration, strength, vibrato: 20, randomness: 90f);
    }

    [Button("현재 위치를 오프셋으로 저장")]
    private void CaptureCurrentOffset()
    {
        // 에디터에서 카메라를 원하는 위치에 놓은 뒤 이 버튼을 눌러 offset 값을 빠르게 설정할 수 있다.
        if (target == null) return;
        offsetX = transform.position.x - target.position.x;
        offsetY = transform.position.y - target.position.y;
        if (followPlayerZ)
            sideDistanceZ = transform.position.z - target.position.z;
        else
            offsetZ = transform.position.z;
    }
}
