using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 사이드뷰 시점에서 플레이어의 X축/Y축 이동을 따라가는 카메라 추적 스크립트다.
// X/Y는 플레이어 위치 기준 상대 offset을 부드럽게 추적하고, Z는 "플레이어 Z 따라가기"에 따라 고정 또는 추적한다.
// 사이드뷰에서도 플레이어가 앞뒤로 움직이므로 모든 씬에서 "플레이어 Z 따라가기"를 켜서 쓴다.
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

    [SerializeField, LabelText("사이드뷰 내려다보는 각도"), Range(0f, 60f)]
    private float sidePitch = 31f;
    // 사이드뷰에서 카메라가 아래로 기울어진 각도다. 0이면 수평으로 옆에서 보고,
    // 클수록 위에서 내려다봐서 앞뒤(Z) 이동이 화면에서 잘 보인다. Y 오프셋/Z 거리와 함께 맞춰야 플레이어가 화면 안에 들어온다.

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

    [ReadOnly, ShowInInspector, LabelText("주목 연출 정도 (0=플레이어, 1=주목 지점)")]
    private float _focusBlend;
    // 0~1 사이 값을 DOTween으로 변화시켜, 추적 기준점을 플레이어에서 주목 지점으로 옮겼다 되돌린다.

    private Vector3 _focusPoint;
    // PlayFocusPan()에서 지정한, 카메라가 잠시 비춰 줄 월드 좌표다.

    private Sequence _focusSequence;
    // 주목 연출(이동 → 정지 → 복귀)을 담은 Sequence다. 다시 호출되면 끊고 새로 시작한다.

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

    [ReadOnly, ShowInInspector, LabelText("탑다운 카메라 방향 (Y 회전)")]
    private float _topDownYaw;
    // 탑다운에서 카메라가 바라보는 방향이다. PlayerController.TopDownYaw를 DOTween으로 부드럽게 따라간다.
    // 기본 0이면 기존처럼 +Z를 바라보며 플레이어 뒤(-Z)쪽 위에서 내려다본다.

    private Tween _yawTween;
    // TopDownYawZone을 드나들 때 탑다운 카메라가 도는 연출이다. 다시 바뀌면 끊고 새로 시작한다.

    [ReadOnly, ShowInInspector, LabelText("카메라 최대 높이 제한 중")]
    private bool _hasMaxHeight;
    // 천장이 낮은 구역(CameraCeilingZone)에 있는 동안 켜져서, 카메라가 천장 위로 올라가 플레이어를 가리지 않게 한다.

    [ReadOnly, ShowInInspector, LabelText("카메라 최대 높이 (월드 Y)"), ShowIf(nameof(_hasMaxHeight))]
    private float _maxHeight;

    private Object _maxHeightOwner;
    // 높이 제한을 건 구역이다. 구역끼리 붙어 있을 때 먼저 나간 구역이 새 구역의 제한을 풀어버리지 않도록 기억한다.

    [ReadOnly, ShowInInspector, LabelText("고정 시점 정도 (0=추적, 1=고정)")]
    private float _fixedBlend;
    // FixedCameraZone이 DOTween으로 0~1을 오가게 한다. 0이면 기존 추적과 완전히 같고, 1이면 고정 위치/회전에 머문다.

    private Vector3 _fixedPosition;
    private Quaternion _fixedRotation;
    // StartFixedView()로 받은, 카메라가 머물 월드 위치와 회전이다.

    private Vector3 _followPosition;
    // 고정 시점 중에도 계속 계산해 두는 "원래 추적 위치"다. 고정이 풀릴 때 이 위치에서 자연스럽게 이어 따라가게 한다.

    private Tween _fixedTween;
    // 고정 시점이 켜지고 꺼지는 전환 연출이다. 구역을 빠르게 드나들어도 현재 값에서 이어지게 다시 호출되면 끊는다.

    // 지금 카메라 높이 제한이 걸려 있는지 외부(CameraCeilingZone)에서 확인할 때 쓴다.
    public bool HasMaxHeight => _hasMaxHeight;

    // 탑다운 시점의 카메라 회전이다. 내려다보는 각도(topDownPitch)에 방향(_topDownYaw)을 더한다.
    private Quaternion TopDownRotation => Quaternion.Euler(topDownPitch, _topDownYaw, 0f);

    // 지금 시점 모드(사이드뷰 ↔ 탑다운 전환 정도)에 맞는 회전이다. 고정 시점이 풀릴 때 이 회전으로 돌아온다.
    private Quaternion ModeRotation => Quaternion.Slerp(_currentSideRotation, TopDownRotation, _topDownBlend);

    private void Start()
    {
        // 씬에 배치된 방향(Y 회전)은 유지하고, 기울기만 sidePitch로 정한다.
        // 씬마다 카메라 회전을 손으로 맞추지 않아도 모든 씬이 같은 내려다보는 각도를 쓰게 하기 위해서다.
        _sideRotation = Quaternion.Euler(sidePitch, transform.eulerAngles.y, 0f);
        _currentSideRotation = _sideRotation;
        transform.rotation = _sideRotation;
        _followPosition = transform.position;

        // 시점 모드는 PlayerController가 관리하고, 카메라는 이벤트를 받아 따라 바뀐다.
        // 이렇게 하면 구역 트리거는 플레이어에게만 알리면 되고 카메라를 따로 연결할 필요가 없다.
        if (target != null)
            _player = target.GetComponent<PlayerController>();
        if (_player != null)
        {
            _player.OnViewModeChanged += HandleViewModeChanged;
            _player.OnSideViewAxisChanged += HandleSideViewAxisChanged;
            _player.OnTopDownYawChanged += HandleTopDownYawChanged;
            _topDownYaw = _player.TopDownYaw;
        }
    }

    // 탑다운 카메라 방향이 바뀌면 현재 각도에서 새 각도까지 가까운 쪽으로 돌린다.
    // 사이드뷰 중(_topDownBlend가 0)에는 값만 바꿔 두고, 회전은 탑다운으로 전환될 때 반영된다.
    private void HandleTopDownYawChanged(float yaw)
    {
        _yawTween?.Kill();
        // 구역 경계를 오가며 방향이 연달아 바뀌어도 현재 각도에서 새 목표로 자연스럽게 이어지게 이전 Tween을 끊는다.

        // DeltaAngle로 최단 회전량을 구해, 예를 들어 350도 → 10도를 340도 거꾸로 돌지 않고 20도만 돌게 한다.
        float targetYaw = _topDownYaw + Mathf.DeltaAngle(_topDownYaw, yaw);

        _yawTween = DOTween.To(() => _topDownYaw, v =>
            {
                _topDownYaw = v;
                if (_topDownBlend > 0f)
                    transform.rotation = Quaternion.Slerp(_currentSideRotation, TopDownRotation, _topDownBlend);
            }, targetYaw, transitionDuration)
            .SetEase(transitionEase);
    }

    // 진행 축이 바뀌면, 원래 사이드뷰 회전을 Y축으로 돌려 새 깊이 방향(진행 축 × 위쪽)을 바라보게 한다.
    // 기본 축(+X)의 깊이 방향은 +Z이므로, +Z에서 새 깊이 방향까지의 각도만큼 돌리면 된다.
    private void HandleSideViewAxisChanged(Vector3 axis)
    {
        Vector3 depthAxis = Vector3.Cross(axis, Vector3.up);
        float yaw = Vector3.SignedAngle(Vector3.forward, depthAxis, Vector3.up);
        Quaternion from = _currentSideRotation;
        Quaternion to = Quaternion.AngleAxis(yaw, Vector3.up) * _sideRotation;

        _axisTween?.Kill();
        // 통로 입구를 오가며 축이 연달아 바뀌어도 현재 각도에서 새 목표로 자연스럽게 이어지게 이전 Tween을 끊는다.

        _axisTween = DOTween.To(() => 0f, v =>
            {
                _currentSideRotation = Quaternion.Slerp(from, to, v);
                // 탑다운 전환이 섞여 있는 중이면 그 비율도 함께 반영해 두 연출이 서로 덮어쓰지 않게 한다.
                transform.rotation = Quaternion.Slerp(_currentSideRotation, TopDownRotation, _topDownBlend);
            }, 1f, transitionDuration)
            .SetEase(transitionEase);
    }

    private void HandleViewModeChanged(ViewMode mode)
    {
        float targetBlend = mode == ViewMode.TopDown ? 1f : 0f;

        _blendTween?.Kill();
        // 전환 도중 다시 구역을 드나들어도 현재 값에서 새 목표로 자연스럽게 이어지게 이전 Tween을 끊는다.

        _blendTween = DOTween.To(() => _topDownBlend, v =>
            {
                _topDownBlend = v;
                // 회전은 플레이어 위치와 무관하므로 Tween이 진행되는 동안에만 갱신한다.
                // 전환이 끝난 뒤에는 회전을 건드리지 않아 기존 사이드뷰 동작과 똑같이 유지된다.
                transform.rotation = Quaternion.Slerp(_currentSideRotation, TopDownRotation, v);
                // TopDownRotation을 매번 새로 읽어, 방향(_topDownYaw) 전환 Tween이 동시에 돌아도 서로 덮어쓰지 않게 한다.
            }, targetBlend, transitionDuration)
            .SetEase(transitionEase);
    }

    private void LateUpdate()
    {
        // LateUpdate에서 카메라를 이동시켜 플레이어 이동이 완전히 처리된 뒤에 따라가게 한다.
        if (target == null) return;

        Vector3 targetPosition = CalculateTargetPosition();

        // 고정 시점 구역(FixedCameraZone) 안이면, 추적 위치는 따로 계속 계산해 두고 화면에는 고정 위치/회전을 섞어 보여준다.
        // 회전도 매 프레임 덮어쓰므로, 그 사이 시점 모드 전환 Tween이 돌아도 고정 화면이 흔들리지 않는다.
        if (_fixedBlend > 0f)
        {
            _followPosition = Vector3.Lerp(_followPosition, targetPosition, followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(_followPosition, _fixedPosition, _fixedBlend) + _shakeOffset;
            transform.rotation = BlendWithoutRoll(ModeRotation, _fixedRotation, _fixedBlend);
            return;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime) + _shakeOffset;
        _followPosition = transform.position;
    }

    // 지금 플레이어 위치 기준으로 카메라가 있어야 할 위치를 계산한다.
    // LateUpdate의 부드러운 추적과 SnapToTarget의 즉시 이동이 같은 계산을 쓰도록 분리했다.
    private Vector3 CalculateTargetPosition()
    {
        // 추적 기준점이다. _focusBlend가 0이면 target.position과 완전히 같아 기존 동작 그대로이고,
        // 1에 가까워질수록 주목 지점을 기준으로 카메라 위치를 계산한다.
        Vector3 followPosition = Vector3.Lerp(target.position, _focusPoint, _focusBlend);
        _currentTargetX = followPosition.x;

        // X/Y축 모두 플레이어 위치 기준 상대 offset을 따라간다.
        // Z는 기본적으로 고정 위치지만, "플레이어 Z 따라가기"를 켜면 플레이어 Z 기준 거리를 유지해
        // 다른 사이드뷰 라인으로 옮겨가도 카메라가 플레이어 정면에 온다.
        // Lerp로 부드럽게 이동해 갑작스러운 카메라 이동을 방지한다.
        float cameraZ = followPlayerZ ? followPosition.z + sideDistanceZ : offsetZ;
        Vector3 targetPosition = new Vector3(
            _currentTargetX + offsetX,
            followPosition.y + offsetY,
            cameraZ
        );

        // -Z 통로처럼 진행 축이 바뀐 구간에서는, 플레이어에서 깊이 방향 반대쪽으로 물러난 통로 옆 위치를 쓴다.
        // (기본 축(+X)이면 이 블록을 건너뛰어 위의 기존 계산을 그대로 사용한다.)
        if (_player != null && _player.SideViewAxis != Vector3.right)
        {
            Vector3 axis = _player.SideViewAxis;
            Vector3 depthAxis = Vector3.Cross(axis, Vector3.up);
            targetPosition = followPosition + axis * offsetX + Vector3.up * offsetY + depthAxis * sideDistanceZ;
        }

        // 탑다운 전환 중이거나 탑다운 상태면, 플레이어 뒤쪽 위에서 내려다보는 위치와 섞는다.
        // 각도(pitch)와 거리로 위치를 계산하므로 각도를 바꿔도 카메라가 항상 플레이어를 정면으로 바라본다.
        if (_topDownBlend > 0f)
        {
            float pitchRad = topDownPitch * Mathf.Deg2Rad;
            Vector3 topDownOffset = new Vector3(
                0f,
                Mathf.Sin(pitchRad) * topDownDistance,
                -Mathf.Cos(pitchRad) * topDownDistance
            );
            // 기본 방향(+Z를 바라봄) 기준 오프셋을 _topDownYaw만큼 Y축으로 돌려, 바라보는 방향의 반대편 위에 카메라를 둔다.
            Vector3 topDownPosition = followPosition + Quaternion.Euler(0f, _topDownYaw, 0f) * topDownOffset;
            targetPosition = Vector3.Lerp(targetPosition, topDownPosition, _topDownBlend);
        }

        // 천장이 낮은 구역에서는 카메라가 정해진 높이 위로 못 올라가게 한다.
        // 플레이어가 높이 점프해도 카메라가 천장 판 위로 올라가 플레이어를 가리는 일을 막는다.
        // 목표 위치만 제한하므로 LateUpdate의 Lerp로 부드럽게 따라간다.
        // 단, 주목 연출(StartFocus/PlayFocusPan)로 다른 곳을 비추는 동안에는 _focusBlend만큼 제한을 풀어 준다.
        // 예: 지하 터빈을 켜면 지상 얼음벽이 녹는 장면을 비춰야 하는데, 제한이 남아 있으면 카메라가 지하에 묶인다.
        if (_hasMaxHeight && targetPosition.y > _maxHeight)
            targetPosition.y = Mathf.Lerp(_maxHeight, targetPosition.y, _focusBlend);

        return targetPosition;
    }

    // 천장이 낮은 구역에 들어왔을 때 CameraCeilingZone이 호출한다. owner에는 호출한 구역 자신을 넘긴다.
    public void SetMaxHeight(float maxY, Object owner)
    {
        _hasMaxHeight = true;
        _maxHeight = maxY;
        _maxHeightOwner = owner;
    }

    // 구역을 나갔을 때 CameraCeilingZone이 호출한다. 제한을 건 구역이 맞을 때만 풀어서,
    // 붙어 있는 다른 구역으로 넘어간 경우 새 구역의 제한이 유지되게 한다.
    public void ClearMaxHeight(Object owner)
    {
        if (_maxHeightOwner != owner) return;

        _hasMaxHeight = false;
        _maxHeightOwner = null;
    }

    // 같은 씬 안에서 플레이어를 멀리 순간이동시킨 직후(예: EntrancePortal) 호출한다.
    // 평소처럼 Lerp로 따라가면 화면이 밝아질 때 카메라가 이전 위치에서 쓸려 오는 모습이 보이므로, 목표 위치로 바로 옮긴다.
    public void SnapToTarget()
    {
        if (target == null) return;

        // 순간이동하면서 바뀐 시점 모드/진행 축/탑다운 방향 전환을 즉시 끝낸다.
        // 위치만 옮기면 화면이 밝아지는 동안 카메라가 아직 회전 중인 모습(예: 90도 도는 모습)이 보이기 때문이다.
        // 호출하는 쪽(EntrancePortal 등)은 화면이 검게 덮인 상태에서 부르므로 즉시 끝내도 튀어 보이지 않는다.
        // IsActive()로 이미 끝났거나 Kill된 Tween은 건너뛴다(끝난 Tween에 Complete를 부르면 DOTween 경고가 난다).
        if (_blendTween.IsActive()) _blendTween.Complete();
        if (_axisTween.IsActive()) _axisTween.Complete();
        if (_yawTween.IsActive()) _yawTween.Complete();

        transform.position = CalculateTargetPosition();
        _followPosition = transform.position;
        // 고정 시점 중에 순간이동한 경우에도, 고정이 풀리면 이전 자리에서 쓸려 오지 않고 도착 지점에서 시작하게 한다.
    }

    // FixedCameraZone에 들어갔을 때 호출한다. 카메라를 position/rotation에 고정하고, duration 동안 부드럽게 넘어간다.
    // duration이 0이면 즉시 고정한다(구역 안으로 리스폰한 경우 등).
    public void StartFixedView(Vector3 position, Quaternion rotation, float duration, Ease ease)
    {
        _fixedPosition = position;
        _fixedRotation = rotation;

        // 추적 중이었다면 지금 카메라 위치를 추적 위치의 시작점으로 삼는다.
        if (_fixedBlend <= 0f)
            _followPosition = transform.position;

        _fixedTween?.Kill();
        if (duration <= 0f)
        {
            _fixedBlend = 1f;
            return;
        }
        _fixedTween = DOTween.To(() => _fixedBlend, v => _fixedBlend = v, 1f, duration).SetEase(ease);
    }

    // FixedCameraZone을 나갔을 때 호출한다. duration 동안 플레이어 추적으로 돌아오고,
    // 0이면 즉시 플레이어 위치로 붙는다(순간이동으로 구역을 벗어난 경우).
    public void EndFixedView(float duration, Ease ease)
    {
        _fixedTween?.Kill();
        if (duration <= 0f)
        {
            FinishFixedView(true);
            return;
        }
        _fixedTween = DOTween.To(() => _fixedBlend, v => _fixedBlend = v, 0f, duration)
            .SetEase(ease)
            .OnComplete(() => FinishFixedView(false));
    }

    // 두 회전을 기울기(X)·방향(Y)·롤(Z) 각도별로 섞는다.
    // Quaternion.Slerp는 기울기와 방향이 둘 다 다르면 중간에 화면이 옆으로 기울어(롤) 보이므로, 고정 시점 전환에는 이 방식을 쓴다.
    private static Quaternion BlendWithoutRoll(Quaternion from, Quaternion to, float t)
    {
        Vector3 a = from.eulerAngles;
        Vector3 b = to.eulerAngles;
        return Quaternion.Euler(Mathf.LerpAngle(a.x, b.x, t), Mathf.LerpAngle(a.y, b.y, t), Mathf.LerpAngle(a.z, b.z, t));
    }

    // 고정 시점을 완전히 끄고 회전을 지금 시점 모드에 맞게 되돌린다.
    // 평소 LateUpdate는 회전을 건드리지 않으므로, 여기서 한 번 맞춰 두지 않으면 고정 회전이 그대로 남는다.
    private void FinishFixedView(bool snap)
    {
        _fixedBlend = 0f;
        transform.rotation = ModeRotation;
        transform.position = snap && target != null ? CalculateTargetPosition() : _followPosition;
        _followPosition = transform.position;
    }

    private void OnDestroy()
    {
        _shakeTween?.Kill();
        _blendTween?.Kill();
        _axisTween?.Kill();
        _yawTween?.Kill();
        _focusSequence?.Kill();
        _fixedTween?.Kill();
        // 씬 전환 등으로 카메라가 파괴될 때 남은 전환 Tween이 파괴된 transform에 접근하지 않게 정리한다.

        if (_player != null)
        {
            _player.OnViewModeChanged -= HandleViewModeChanged;
            _player.OnSideViewAxisChanged -= HandleSideViewAxisChanged;
            _player.OnTopDownYawChanged -= HandleTopDownYawChanged;
        }
    }

    // 보스 패턴 폭발 등 임팩트가 필요한 순간에 외부(예: PollenTrail)에서 호출한다.
    public void Shake(float duration, float strength)
    {
        _shakeTween?.Kill();
        _shakeOffset = Vector3.zero;

        _shakeTween = DOTween.Shake(() => _shakeOffset, v => _shakeOffset = v, duration, strength, vibrato: 20, randomness: 90f);
    }

    // 카메라가 플레이어를 떠나 focusPoint를 잠시 비춘 뒤 다시 플레이어로 돌아오는 연출이다.
    // 도착 후 다음 목표를 보여 줄 때처럼 플레이어 조작을 잠근 상태에서 외부(예: EntrancePortal)가 호출한다.
    // 연출이 모두 끝나면 onComplete를 호출하므로, 호출한 쪽은 그때 조작을 되돌려 주면 된다.
    public void PlayFocusPan(Vector3 focusPoint, float moveDuration, float holdDuration, float returnDuration, Ease ease, System.Action onComplete = null)
    {
        _focusSequence?.Kill();
        // 연출 중 다시 호출돼도 현재 _focusBlend 값에서 이어지게 이전 Sequence만 끊는다.

        _focusPoint = focusPoint;

        _focusSequence = DOTween.Sequence();
        _focusSequence.Append(DOTween.To(() => _focusBlend, v => _focusBlend = v, 1f, moveDuration).SetEase(ease));
        _focusSequence.AppendInterval(holdDuration);
        _focusSequence.Append(DOTween.To(() => _focusBlend, v => _focusBlend = v, 0f, returnDuration).SetEase(ease));
        _focusSequence.OnComplete(() =>
        {
            _focusBlend = 0f;
            onComplete?.Invoke();
        });
        // LateUpdate의 Lerp(followSpeed)가 위에 한 번 더 덧입혀져 움직임이 더 부드럽게 보인다.
    }

    // 끝나는 시점을 미리 모를 때(예: 불이 다 탈 때까지) 쓴다. StartFocus로 주목 지점에 붙잡아 두고,
    // 조건이 끝나면 EndFocus로 플레이어에게 돌려보낸다. PlayFocusPan처럼 시간으로 자동 복귀하지 않는다.
    public void StartFocus(Vector3 focusPoint, float moveDuration, Ease ease)
    {
        _focusSequence?.Kill();
        _focusPoint = focusPoint;

        _focusSequence = DOTween.Sequence();
        _focusSequence.Append(DOTween.To(() => _focusBlend, v => _focusBlend = v, 1f, moveDuration).SetEase(ease));
        // Sequence에는 이동만 담고 끝나도 _focusBlend를 1로 유지해, EndFocus가 올 때까지 그 자리에 머문다.
    }

    // StartFocus로 붙잡아 둔 카메라를 플레이어에게 되돌린다. 복귀가 끝나면 onComplete를 호출하므로
    // 호출한 쪽은 그때 조작을 되돌려 주면 된다.
    public void EndFocus(float returnDuration, Ease ease, System.Action onComplete = null)
    {
        _focusSequence?.Kill();

        _focusSequence = DOTween.Sequence();
        _focusSequence.Append(DOTween.To(() => _focusBlend, v => _focusBlend = v, 0f, returnDuration).SetEase(ease));
        _focusSequence.OnComplete(() =>
        {
            _focusBlend = 0f;
            onComplete?.Invoke();
        });
    }

    [Button("주목 연출 테스트 (Play Mode)")]
    private void TestFocusPan(Vector3 point)
    {
        if (!Application.isPlaying) return;
        PlayFocusPan(point, 1.5f, 1.5f, 1f, Ease.InOutSine);
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
