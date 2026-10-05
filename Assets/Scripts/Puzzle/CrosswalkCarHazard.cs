using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 도로(보라색 선 너머)를 덮는 트리거다. 탑다운 구역 전용 기믹이다.
// - 신호가 초록불이거나, 빨간불이어도 횡단보도 밖(무단횡단)이면: 화면 밖에서 차가 순식간에 돌진해 플레이어를 친다.
//   맞으면 데미지를 받고 근처 체크포인트로 돌아간다.
// - 빨간불에 횡단보도로 건너면: 차가 다가오다가 정지선에서 멈춘다.
// 차는 DOTween으로 움직이지만 플레이어를 물리적으로 밀지 않고, 겹침 검사로만 "치였다"를 판정한다.
// 그래서 CharacterController 충돌과 꼬이지 않는다.
[RequireComponent(typeof(Collider))]
public class CrosswalkCarHazard : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("신호 컨트롤러")]
    private TrafficSignalController signal;

    [SerializeField, LabelText("횡단보도 범위")]
    private Collider crosswalkArea;
    // 횡단보도를 덮는 콜라이더(트리거)다. 빨간불일 때 이 안에서 건너면 안전하다.

    [SerializeField, LabelText("차")]
    private Transform car;
    // 돌진할 차 오브젝트다. 모델은 이 오브젝트의 자식으로 두면 나중에 교체하기 쉽다.

    [SerializeField, LabelText("차 충돌 범위")]
    private Collider carHitBox;
    // 차 크기에 맞춘 콜라이더(트리거)다. 플레이어와 겹치면 치인 것으로 본다.

    [SerializeField, LabelText("차 대기 위치")]
    private Transform carStartPoint;
    // 화면 밖, 차가 기다리는 위치다. 차는 여기서 -X(서쪽)로 달려온다.

    [SerializeField, LabelText("정지선 위치")]
    private Transform stopPoint;
    // 빨간불일 때 차가 멈출 위치(차 중심)다. 횡단보도 바로 앞에 둔다.

    [Title("돌진 설정")]
    [SerializeField, LabelText("돌진 시간")]
    private float rushDuration = 0.6f;
    // 대기 위치에서 플레이어를 지나칠 때까지 걸리는 시간이다. 짧을수록 "순식간에" 달려온다.

    [SerializeField, LabelText("플레이어를 지나쳐 가는 거리")]
    private float rushOvershoot = 20f;

    [SerializeField, LabelText("데미지")]
    private float damage = 20f;

    [SerializeField, LabelText("카메라 흔들림 시간")]
    private float shakeDuration = 0.3f;

    [SerializeField, LabelText("카메라 흔들림 세기")]
    private float shakeStrength = 0.6f;

    [SerializeField, LabelText("차선 Z 목록")]
    private float[] laneZs = { -33.5f, -41.5f };
    // 차가 달릴 수 있는 차선 중심 Z다. 플레이어와 가장 가까운 차선으로 달려온다.

    [Title("정지 설정")]
    [SerializeField, LabelText("정지선까지 오는 시간")]
    private float approachDuration = 1.5f;

    [SerializeField, LabelText("정지 Ease")]
    private Ease approachEase = Ease.OutCubic;

    [SerializeField, LabelText("카메라")]
    private SideViewCamera sideViewCamera;
    // 치였을 때 화면을 흔들 카메라다. 비워두면 흔들지 않는다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    private CarState _state = CarState.Waiting;

    private enum CarState { Waiting, Rushing, Approaching, Stopped }

    private Tween _carTween;
    private PlayerController _targetPlayer;
    private bool _hitThisRush;
    private Collider _roadArea;

    private void Awake()
    {
        _roadArea = GetComponent<Collider>();
    }

    private void OnDestroy()
    {
        _carTween?.Kill();
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 트리거는 몸(캡슐)이 살짝 걸치기만 해도 호출되므로, 보도 가장자리에 서 있어도 들어온다.
        // 안전 판정(횡단보도 안인지)과 기준을 맞추기 위해 플레이어 "중심"이 도로 안일 때만 반응한다.
        if (!IsInsideXZ(_roadArea, player.transform.position)) return;

        bool safe = IsSafe(player);

        if (!safe)
        {
            // 돌진 중이 아니면(대기 중이든 정지선에 서 있든) 플레이어를 향해 돌진한다.
            if (_state != CarState.Rushing)
                Rush(player);
        }
        else if (_state == CarState.Waiting)
        {
            ApproachAndStop();
        }
    }

    // 빨간불에 횡단보도 위에 있으면 안전하다. 돌진 중인 차도 이때는 플레이어를 치지 않는다.
    private bool IsSafe(PlayerController player)
    {
        return signal != null && signal.IsRed && IsInsideXZ(crosswalkArea, player.transform.position);
    }

    // 범위 안인지는 바닥(XZ) 기준으로만 본다. 점프 중이어도 그 위에 있으면 안에 있는 것으로 본다.
    private static bool IsInsideXZ(Collider area, Vector3 position)
    {
        if (area == null) return false;
        Bounds b = area.bounds;
        return position.x >= b.min.x && position.x <= b.max.x && position.z >= b.min.z && position.z <= b.max.z;
    }

    private void Rush(PlayerController player)
    {
        _state = CarState.Rushing;
        _targetPlayer = player;
        _hitThisRush = false;

        // 플레이어와 가장 가까운 차선으로 서쪽을 향해 내달린다.
        // 정지선에 서 있던 차가 같은 차선이고 아직 플레이어 동쪽에 있으면 그 자리에서 그대로 출발하고,
        // 그 외에는 화면 밖 대기 위치에서 출발한다(차를 다른 차선으로 눈앞에서 순간이동시키지 않기 위해서).
        float laneZ = GetNearestLaneZ(player.transform.position.z);
        bool sameLane = Mathf.Abs(car.position.z - laneZ) < 0.1f;
        bool aheadOfPlayer = car.position.x > player.transform.position.x;
        if (!(sameLane && aheadOfPlayer))
        {
            Vector3 from = carStartPoint.position;
            from.z = laneZ;
            car.position = from;
        }
        Vector3 to = new Vector3(player.transform.position.x - rushOvershoot, car.position.y, laneZ);

        _carTween?.Kill();
        _carTween = car.DOMove(to, rushDuration)
            .SetEase(Ease.InQuad)
            .OnUpdate(CheckHit)
            .OnComplete(ResetCar);
    }

    // 돌진하는 매 프레임 차와 플레이어가 겹쳤는지 검사한다.
    private void CheckHit()
    {
        if (_hitThisRush || _targetPlayer == null || carHitBox == null) return;

        // 돌진하는 사이 플레이어가 빨간불 횡단보도로 피했다면 치지 않는다.
        if (IsSafe(_targetPlayer)) return;

        Collider playerCol = _targetPlayer.GetComponent<Collider>();
        if (playerCol == null || !carHitBox.bounds.Intersects(playerCol.bounds)) return;

        _hitThisRush = true;
        HitPlayer(_targetPlayer);
    }

    private void HitPlayer(PlayerController player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null) return;

        if (sideViewCamera != null)
            sideViewCamera.Shake(shakeDuration, shakeStrength);

        health.TakeDamage(damage);

        // 데미지로 죽지 않았을 때만 체크포인트로 옮긴다(죽었다면 기존 사망 처리가 리스폰을 담당한다).
        // 순간이동은 프로젝트 규칙대로 Respawn()을 거쳐 시점(탑다운/사이드뷰)도 함께 맞춘다.
        if (health.CurrentHealth > 0f)
            health.Respawn(CheckpointManager.CurrentCheckpointPosition);
    }

    // 빨간불: 대기 위치에서 정지선까지 감속하며 다가와 멈춘다.
    private void ApproachAndStop()
    {
        _state = CarState.Approaching;

        car.position = carStartPoint.position;
        _carTween?.Kill();
        _carTween = car.DOMove(stopPoint.position, approachDuration)
            .SetEase(approachEase)
            .OnComplete(() => _state = CarState.Stopped);
    }

    // 돌진이 끝난 차를 화면 밖 대기 위치로 되돌린다.
    private void ResetCar()
    {
        car.position = carStartPoint.position;
        _state = CarState.Waiting;
    }

    private float GetNearestLaneZ(float z)
    {
        float best = laneZs.Length > 0 ? laneZs[0] : z;
        foreach (float laneZ in laneZs)
            if (Mathf.Abs(laneZ - z) < Mathf.Abs(best - z)) best = laneZ;
        return best;
    }

    [Button("차 돌진 테스트 (플레이어에게)")]
    private void TestRush()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) Rush(player);
    }

    [Button("정지선 진입 테스트")]
    private void TestApproach() => ApproachAndStop();
}
