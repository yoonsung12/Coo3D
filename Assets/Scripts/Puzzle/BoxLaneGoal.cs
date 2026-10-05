using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 탑다운에서 선풍기로 밀어 온 상자가 이 트리거에 들어오면,
// 상자를 사이드뷰 라인(Z) 위에 반듯하게 스냅해 발판으로 고정하고 SideViewLaneZone을 켠다.
// 그 뒤 플레이어는 사이드뷰에서 상자 → 정거장 → 버스 순서로 밟고 지나갈 수 있다.
[RequireComponent(typeof(Collider))]
public class BoxLaneGoal : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고 상자가 놓여야 할 자리보다 조금 넉넉하게 크기를 맞춘다.\n" +
             "상자는 이 오브젝트의 X 위치와 연결된 라인의 Z 위치로 스냅된다.")]
    [Title("연결 참조")]
    [SerializeField, LabelText("목표 상자")]
    private Rigidbody targetBox;
    // 선풍기로 밀 상자의 Rigidbody(WoodenBox가 붙은 오브젝트)를 연결한다.

    [SerializeField, LabelText("사이드뷰 라인 구역")]
    private SideViewLaneZone laneZone;
    // 상자가 자리에 놓이면 켜질 SideViewLaneZone을 연결한다. 스냅 Z도 이 구역의 라인 Z를 따른다.

    [SerializeField, LabelText("플레이어")]
    private PlayerController player;
    // 정답 후 순간이동시키고 사이드뷰로 바꿀 플레이어를 연결한다. 같은 오브젝트의 PlayerHealth도 함께 사용한다.

    [SerializeField, LabelText("이동 지점")]
    private Transform transferPoint;
    // 정답 후 플레이어가 옮겨질 위치(사이드뷰 라인 위, 바닥 높이)다. 반드시 사이드뷰 라인 구역 안에 둔다.

    [Title("페이드 이동 연출")]
    [SerializeField, LabelText("페이드 아웃 시간")]
    private float fadeOutDuration = 0.4f;

    [SerializeField, LabelText("페이드 인 시간")]
    private float fadeInDuration = 0.6f;
    // 페이드 인 동안 카메라가 탑다운 → 사이드뷰로 전환되는 모습이 함께 보인다.

    [Title("스냅 연출 (DOTween)")]
    [SerializeField, LabelText("스냅 회전")]
    private Vector3 snapEuler = new Vector3(270f, 0f, 0f);
    // 상자가 사이드뷰에서 반듯하게 보이도록 맞출 최종 회전값이다. 모델이 X 270도로 세워져 있으면 (270, 0, 0)을 쓴다.

    [SerializeField, LabelText("스냅 시간")]
    private float snapDuration = 0.4f;
    // 값이 클수록 상자가 천천히 제자리로 미끄러져 들어간다.

    [SerializeField, LabelText("스냅 Ease")]
    private Ease snapEase = Ease.OutQuad;

    [SerializeField, LabelText("착지 펀치 비율")]
    private float punchRatio = 0.08f;
    // 스냅 직후 상자가 살짝 눌렸다 돌아오는 정도다. 상자 현재 스케일에 곱해서 쓰므로 스케일이 커도 비율이 유지된다.

    [SerializeField, LabelText("펀치 시간")]
    private float punchDuration = 0.25f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("정답 처리됨")]
    private bool _isSolved;

    private Sequence _snapSequence;
    private Vector3 _boxStartPosition;
    private Quaternion _boxStartRotation;
    private Vector3 _boxStartScale;
    private PlayerHealth _playerHealth;
    private BoxRespawner _boxRespawner;
    // 상자에 BoxRespawner가 있으면, 정답 후에는 되돌아가지 않도록 잠근다. 없어도 퍼즐은 동작한다.

    private void Awake()
    {
        if (targetBox != null)
            _boxRespawner = targetBox.GetComponent<BoxRespawner>();

        // 순간이동은 프로젝트 규칙대로 PlayerHealth.Respawn()을 거쳐야 하므로 미리 찾아 둔다.
        if (player != null)
            _playerHealth = player.GetComponent<PlayerHealth>();

        // 리셋 버튼에서 되돌릴 수 있도록 상자의 처음 상태를 기억한다.
        if (targetBox != null)
        {
            _boxStartPosition = targetBox.position;
            _boxStartRotation = targetBox.rotation;
            _boxStartScale = targetBox.transform.localScale;
        }
    }

    private void OnDestroy()
    {
        _snapSequence?.Kill();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isSolved || targetBox == null) return;

        // 상자는 Rigidbody가 있으므로 attachedRigidbody로 "목표 상자인지"를 비교한다.
        if (other.attachedRigidbody != targetBox) return;

        Solve();
    }

    private void Solve()
    {
        _isSolved = true;

        // 리스폰 연출 중이었다면 멈추고, 이후 방치 시간이 지나도 처음 자리로 돌아가지 않게 한다.
        _boxRespawner?.Lock();

        // 물리를 끄고(kinematic) 고정해야 스냅 중이나 이후에 선풍기 바람·플레이어 착지로 밀리지 않는다.
        // WoodenBox.OnBlown의 AddForce는 kinematic일 때 무시되므로 따로 수정할 필요가 없다.
        targetBox.linearVelocity = Vector3.zero;
        targetBox.angularVelocity = Vector3.zero;
        targetBox.isKinematic = true;

        Transform box = targetBox.transform;
        // 높이(Y)는 바닥에 놓인 현재 높이를 유지하고, X는 목표 지점, Z는 사이드뷰 라인에 맞춘다.
        Vector3 snapPosition = new Vector3(transform.position.x, box.position.y, laneZone.LaneZ);

        // 이동+회전을 동시에 하고, 끝나면 살짝 눌리는 펀치로 "딱 맞았다"는 느낌을 준다.
        _snapSequence?.Kill();
        _snapSequence = DOTween.Sequence()
            .Append(box.DOMove(snapPosition, snapDuration).SetEase(snapEase))
            .Join(box.DORotate(snapEuler, snapDuration).SetEase(snapEase))
            .Append(box.DOPunchScale(_boxStartScale * punchRatio, punchDuration, 6, 0.5f))
            .OnComplete(() =>
            {
                // 펀치가 중간에 끊겨도 스케일이 어긋나지 않게 완료 시 원래 스케일로 확정한다.
                box.localScale = _boxStartScale;
                // 상자가 자리 잡는 모습을 보여준 뒤에 플레이어를 이동시킨다.
                TransferPlayer();
            });

        // 상자가 자리를 잡자마자 사이드뷰 라인을 켠다. 이후 이 라인 안에서는 항상 사이드뷰가 유지된다.
        laneZone.Activate();
    }

    // 화면을 덮은 상태에서 플레이어를 이동 지점으로 옮기고 사이드뷰로 바꾼 뒤 다시 화면을 연다.
    private void TransferPlayer()
    {
        if (player == null || transferPoint == null) return;

        if (ScreenFader.Instance == null)
        {
            // 페이더가 없는 환경(테스트 씬 등)에서도 퍼즐이 진행되도록 연출 없이 바로 옮긴다.
            MovePlayerToLane();
            return;
        }

        ScreenFader.Instance.FadeOut(() =>
        {
            // 페이드 도중 리셋 버튼을 눌렀다면 이동하지 않는다.
            if (!_isSolved) return;

            MovePlayerToLane();
            ScreenFader.Instance.FadeIn(null, fadeInDuration);
        }, fadeOutDuration);
    }

    private void MovePlayerToLane()
    {
        // Respawn()은 CharacterController를 잠시 끄고 옮긴 뒤, 위치 기준으로 시점을 다시 맞춘다.
        // 이동 지점은 큰 탑다운 구역 안이라 여기서는 탑다운으로 판정되므로, 직후에 사이드뷰로 직접 바꿔 준다.
        if (_playerHealth != null)
            _playerHealth.Respawn(transferPoint.position);

        player.SetViewMode(ViewMode.SideView, laneZone.LaneZ);
    }

    [Button("정답 처리 테스트")]
    private void TestSolve()
    {
        if (!_isSolved && targetBox != null) Solve();
    }

    [Button("리셋")]
    private void ResetPuzzle()
    {
        if (targetBox == null) return;

        _snapSequence?.Kill();
        _isSolved = false;

        // 처음 위치로 되돌리고 다시 바람에 밀릴 수 있게 물리를 켠다.
        targetBox.isKinematic = false;
        targetBox.transform.SetPositionAndRotation(_boxStartPosition, _boxStartRotation);
        targetBox.transform.localScale = _boxStartScale;
        targetBox.linearVelocity = Vector3.zero;

        _boxRespawner?.Unlock();
        laneZone.Deactivate();
    }
}
