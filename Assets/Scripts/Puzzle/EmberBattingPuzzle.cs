using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 "불씨 야구" 퍼즐을 관리한다. 불씨(HittableEmber)를 일정 간격으로 떨어뜨리고,
// 불씨를 친 높이로 노릴 매듭을 정하며, 매듭 3개가 다 타면 아치에 불을 붙인다.
// 아치 이후의 불 번짐, 컷신, 다리가 쓰러지는 과정은 기존 FlammableObject와 Drawbridge가 그대로 처리한다.
public class EmberBattingPuzzle : MonoBehaviour
{
    [Title("불씨 생성")]
    [SerializeField, LabelText("불씨 프리팹")]
    private HittableEmber emberPrefab;

    [SerializeField, LabelText("생성 위치")]
    private Transform spawnPoint;
    // 화로 바로 위에 둔다. 플레이어가 다니는 줄과 Z를 맞춰야 배게검 판정에 들어온다.

    [SerializeField, LabelText("생성 간격(초)")]
    private float spawnInterval = 2f;
    // 값이 작을수록 실패 후 다시 치기까지 기다리는 시간이 짧아진다. 스윙 재사용 대기(1초)보다 길게 둔다.

    [Title("높이 설정 (월드 Y)")]
    [SerializeField, LabelText("점화 높이")]
    private float igniteY = 4.9f;
    // 불씨가 이 높이 아래로 내려오면 불이 붙는다. 화로의 높이와 맞춘다.

    [SerializeField, LabelText("바닥 높이")]
    private float groundY = 0f;
    // 불씨가 이 높이까지 내려오면 꺼진다.

    [InfoBox("불씨를 친 높이가 각 값 이상이면 해당 매듭을 노린다. 아래쪽 매듭 값보다 낮으면 땅볼이 된다.\n배게검 판정은 발밑부터 약 3까지 닿으므로 위쪽 구간이 3을 넘으면 칠 수 없다.")]
    [SerializeField, LabelText("위쪽 매듭 시작 높이")]
    private float topHitMinY = 1.9f;

    [SerializeField, LabelText("가운데 매듭 시작 높이")]
    private float middleHitMinY = 1.1f;

    [SerializeField, LabelText("아래쪽 매듭 시작 높이")]
    private float bottomHitMinY = 0.4f;

    [Title("매듭 / 목표")]
    [SerializeField, LabelText("위쪽 매듭")]
    private FlammableObject topKnot;

    [SerializeField, LabelText("가운데 매듭")]
    private FlammableObject middleKnot;

    [SerializeField, LabelText("아래쪽 매듭")]
    private FlammableObject bottomKnot;

    [SerializeField, LabelText("땅볼 목표")]
    private Transform groundBallTarget;
    // 너무 늦게 쳤을 때 불씨가 향할 지점이다. 타격 위치 앞쪽 바닥 아래에 둔다.

    [SerializeField, LabelText("매듭이 다 타면 불붙일 아치")]
    private FlammableObject archToIgnite;
    // 여기에 불이 붙으면 기존 Drawbridge의 묶음 연출이 이어진다.

    [Title("고정 카메라")]
    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;

    [SerializeField, LabelText("플레이어")]
    private PlayerController player;

    [SerializeField, LabelText("카메라 구역")]
    private Collider cameraZone;
    // 타격 위치를 덮는 트리거다. 안에 있는 동안 플레이어와 매듭이 한 화면에 들어오게 카메라를 고정한다.

    [SerializeField, LabelText("카메라 지점")]
    private Transform cameraPoint;

    [SerializeField, LabelText("카메라 전환 시간")]
    private float cameraMoveDuration = 0.8f;

    [SerializeField, LabelText("카메라 Ease")]
    private Ease cameraEase = Ease.InOutSine;

    [SerializeField, LabelText("순간이동 판정 거리")]
    private float teleportDistance = 5f;
    // 한 프레임에 이보다 멀리 움직였으면 리스폰으로 보고 전환 없이 바로 카메라를 맞춘다.

    [Title("타격 연출 (DOTween)")]
    [SerializeField, LabelText("타격 흔들림 시간")]
    private float hitShakeDuration = 0.12f;

    [SerializeField, LabelText("타격 흔들림 세기")]
    private float hitShakeStrength = 0.1f;

    [SerializeField, LabelText("매듭 명중 흔들림 세기")]
    private float knotShakeStrength = 0.25f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 구역 안에 있음")]
    private bool _isInside;

    [ReadOnly, ShowInInspector, LabelText("타버린 매듭 수")]
    private int BurnedKnotCount => CountBurnedKnots();

    [ReadOnly, ShowInInspector, LabelText("풀림")]
    private bool _isSolved;

    // HittableEmber가 매 물리 프레임 읽는 값이다.
    public float IgniteY => igniteY;
    public float GroundY => groundY;

    private CharacterController _playerController;
    private Vector3 _lastPlayerPosition;
    private Coroutine _spawnCoroutine;

    private void Awake()
    {
        if (player != null)
        {
            _playerController = player.GetComponent<CharacterController>();
            _lastPlayerPosition = player.transform.position;
        }
    }

    private void OnEnable()
    {
        Subscribe(topKnot);
        Subscribe(middleKnot);
        Subscribe(bottomKnot);

        if (!_isSolved)
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        Unsubscribe(topKnot);
        Unsubscribe(middleKnot);
        Unsubscribe(bottomKnot);

        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;

        // 이 오브젝트가 꺼져도 카메라가 고정된 채로 남지 않게 바로 풀어 준다.
        if (_isInside && sideViewCamera != null)
            sideViewCamera.EndFixedView(0f, cameraEase);
        _isInside = false;
    }

    private void Update()
    {
        if (_isSolved) return;
        UpdateCameraZone();
    }

    // 안/밖 판정은 트리거 이벤트가 아니라 매 프레임 실제 위치로 한다.
    // CharacterController를 끈 채 순간이동하면(Respawn) OnTriggerExit가 오지 않기 때문이다. (FixedCameraZone과 같은 방식)
    // FixedCameraZone을 쓰지 않는 이유는, 그 컴포넌트가 조작을 탑다운으로 바꾸기 때문이다. 이 퍼즐은 사이드뷰 조작을 유지한다.
    private void UpdateCameraZone()
    {
        if (player == null || _playerController == null || sideViewCamera == null || cameraZone == null || cameraPoint == null) return;

        Vector3 center = player.transform.TransformPoint(_playerController.center);
        bool inside = cameraZone.ClosestPoint(center) == center;
        // ClosestPoint는 점이 콜라이더 안에 있으면 그 점을 그대로 돌려주므로, 같으면 안에 있다는 뜻이다.

        bool teleported = (player.transform.position - _lastPlayerPosition).sqrMagnitude > teleportDistance * teleportDistance;
        _lastPlayerPosition = player.transform.position;

        if (inside && !_isInside)
        {
            _isInside = true;
            sideViewCamera.StartFixedView(cameraPoint.position, cameraPoint.rotation, teleported ? 0f : cameraMoveDuration, cameraEase);
        }
        else if (!inside && _isInside)
        {
            _isInside = false;
            sideViewCamera.EndFixedView(teleported ? 0f : cameraMoveDuration, cameraEase);
        }
    }

    private IEnumerator SpawnRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(spawnInterval);
        // 같은 대기 객체를 재사용해 매번 가비지가 생기지 않게 한다.

        while (true)
        {
            SpawnEmber();
            yield return wait;
        }
    }

    private void SpawnEmber()
    {
        if (emberPrefab == null || spawnPoint == null) return;

        HittableEmber ember = Instantiate(emberPrefab, spawnPoint.position, emberPrefab.transform.rotation);
        ember.Init(this);
    }

    // 불씨를 친 높이로 날아갈 목표 위치를 정한다. 불씨가 이 높이 구간에 있을 때 치는 것이 곧 조준이다.
    public Vector3 GetTargetForHeight(Vector3 emberPosition)
    {
        float y = emberPosition.y;

        if (y >= topHitMinY && topKnot != null) return topKnot.transform.position;
        if (y >= middleHitMinY && y < topHitMinY && middleKnot != null) return middleKnot.transform.position;
        if (y >= bottomHitMinY && y < middleHitMinY && bottomKnot != null) return bottomKnot.transform.position;

        return groundBallTarget != null ? groundBallTarget.position : emberPosition + Vector3.down;
    }

    // 불씨가 닿은 대상이 이 퍼즐의 매듭일 때만 불을 붙이고 true를 돌려준다.
    // 이미 탄 매듭에 닿아도 불씨는 소모되어 사라진다(OnIgnited가 알아서 무시한다).
    public bool TryIgniteKnot(FlammableObject flammable)
    {
        if (flammable != topKnot && flammable != middleKnot && flammable != bottomKnot) return false;

        flammable.OnIgnited();
        if (sideViewCamera != null)
            sideViewCamera.Shake(hitShakeDuration * 2f, knotShakeStrength);
        return true;
    }

    public void PlayHitShake()
    {
        if (sideViewCamera != null)
            sideViewCamera.Shake(hitShakeDuration, hitShakeStrength);
    }

    private void Subscribe(FlammableObject knot)
    {
        if (knot != null) knot.OnBurnedOut += HandleKnotBurnedOut;
    }

    private void Unsubscribe(FlammableObject knot)
    {
        if (knot != null) knot.OnBurnedOut -= HandleKnotBurnedOut;
    }

    private int CountBurnedKnots()
    {
        int count = 0;
        if (topKnot != null && topKnot.CurrentState == FlammableObject.FireState.Ash) count++;
        if (middleKnot != null && middleKnot.CurrentState == FlammableObject.FireState.Ash) count++;
        if (bottomKnot != null && bottomKnot.CurrentState == FlammableObject.FireState.Ash) count++;
        return count;
    }

    // 매듭이 재가 될 때마다 호출된다. 3개가 다 타야 아치로 불이 넘어간다.
    private void HandleKnotBurnedOut()
    {
        if (_isSolved || CountBurnedKnots() < 3) return;

        _isSolved = true;

        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;

        // 고정 카메라가 켜져 있으면 Drawbridge의 주목 연출이 화면에 반영되지 않으므로 먼저 풀어 준다.
        if (_isInside && sideViewCamera != null)
            sideViewCamera.EndFixedView(cameraMoveDuration, cameraEase);
        _isInside = false;

        if (archToIgnite != null)
            archToIgnite.OnIgnited();
    }

    [Button("불씨 1개 생성 테스트 (Play Mode)")]
    private void TestSpawnEmber()
    {
        if (Application.isPlaying) SpawnEmber();
    }

    [Button("매듭 전부 점화 테스트 (Play Mode)")]
    private void TestIgniteAllKnots()
    {
        if (!Application.isPlaying) return;

        if (topKnot != null) topKnot.OnIgnited();
        if (middleKnot != null) middleKnot.OnIgnited();
        if (bottomKnot != null) bottomKnot.OnIgnited();
    }
}
