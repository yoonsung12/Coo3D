using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// "우산 문 비둘기"에 붙인다. 한 대만 맞아도 놀라서 날아가 버리고, 물고 있던 우산은 그 자리 바닥에 떨어뜨린다.
// 떨어진 우산은 실제로 주울 수 있는 우산 아이템(UmbrellaUnlockItem)으로 바뀐다.
// 비둘기는 AI 없이 가만히 서 있는 과녁이라, 이 컴포넌트는 Enemy의 피격 이벤트(OnDamageTaken)만 받아서 처리한다.
// 체력이 0이 되는 기존 사망 연출(작아지며 사라짐)과 겹치지 않도록 프리팹의 Enemy 최대 체력은 크게(9999) 둔다.
[RequireComponent(typeof(Enemy))]
public class UmbrellaCarrier : MonoBehaviour
{
    [Title("연결 참조")]
    [SerializeField, LabelText("입에 문 우산")]
    private Transform heldUmbrella;
    // 머리 뼈(head0) 밑에 붙어 있는 접은 우산 오브젝트를 연결한다. 맞으면 몸에서 떼어 바닥에 떨어뜨린다.

    [SerializeField, LabelText("우산 아이템 프리팹")]
    private UmbrellaUnlockItem itemPrefab;
    // 우산이 떨어진 자리에 생성할 주울 수 있는 우산 아이템 프리팹이다.

    [SerializeField, LabelText("Tool Manager (비우면 자동)")]
    private ToolManager toolManager;
    // 씬의 플레이어 ToolManager를 연결한다. 비워두면 아이템이 먹힐 때 플레이어에게서 찾는다.

    [SerializeField, LabelText("획득 안내 패널")]
    private CanvasGroup noticePanel;
    // "우산을 얻었다!" 안내 패널의 CanvasGroup을 연결한다. 비워두면 안내 없이 해금만 된다.

    [Title("우산 떨어뜨리기 (DOTween)")]
    [SerializeField, LabelText("떨어지는 시간")]
    private float dropDuration = 0.45f;

    [SerializeField, LabelText("바닥에서 띄울 높이")]
    private float itemHoverHeight = 0.5f;
    // 아이템이 바닥에 파묻혀 보이지 않도록 이만큼 띄워서 생성한다.

    [Title("날아가기 (DOTween)")]
    [SerializeField, LabelText("움찔 시간")]
    private float flinchDelay = 0.2f;
    // 맞고 나서 날아오르기 전에 잠깐 멈칫하는 시간이다. 피격 깜빡임이 보일 만큼 둔다.

    [SerializeField, LabelText("날아갈 방향·거리 (로컬)")]
    private Vector3 flyOffset = new Vector3(0f, 8f, 5f);
    // 비둘기 기준 좌표다. Z가 앞(부리 방향), Y가 위다. 기본값은 바라보던 쪽 위로 비스듬히 날아간다.

    [SerializeField, LabelText("날아가는 시간")]
    private float flyDuration = 1.4f;

    [SerializeField, LabelText("날아가기 Ease")]
    private Ease flyEase = Ease.InQuad;
    // InQuad는 처음엔 천천히 떠오르다가 점점 빨라져, 놀라서 박차고 날아가는 느낌을 준다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("날아감")]
    private bool _hasFled;

    private Enemy _enemy;
    private EnemyAnimationController _enemyAnim;
    private Sequence _dropSequence;
    private Sequence _flySequence;
    // 연출 도중 씬이 바뀌는 경우를 대비해 저장해 두었다가 정리한다.

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _enemyAnim = GetComponent<EnemyAnimationController>();
    }

    private void OnEnable()
    {
        _enemy.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        _enemy.OnDamageTaken -= HandleDamageTaken;
    }

    private void OnDestroy()
    {
        _flySequence?.Kill();
        // 우산 Sequence는 우산 오브젝트에 묶여(SetLink) 있어 따로 정리하지 않아도 된다.
    }

    // 처음 한 대 맞았을 때만 반응한다. 이후로는 콜라이더를 꺼서 더 맞지도 않는다.
    private void HandleDamageTaken(float amount)
    {
        if (_hasFled) return;
        _hasFled = true;

        // 날아가는 중에 또 맞거나 플레이어와 부딪히지 않게 몸 콜라이더를 끈다.
        foreach (Collider col in GetComponents<Collider>())
            col.enabled = false;

        // 일반 적처럼 "처치함"으로 기록해, 체크포인트 저장 후 이어하기를 하면 Enemy.Start가 이 비둘기를 숨긴다.
        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkEnemyDefeated(SaveManager.GetObjectId(gameObject));

        DropUmbrella();
        FlyAway();
    }

    // 입에서 우산을 떼어 바로 아래 바닥으로 툭 떨어뜨린다.
    private void DropUmbrella()
    {
        if (heldUmbrella == null || itemPrefab == null) return;

        // 비둘기 몸은 날아가므로 우산은 먼저 몸에서 떼어 씬에 독립시킨다.
        heldUmbrella.SetParent(null, true);

        Vector3 dropPosition = heldUmbrella.position;
        // QueryTriggerInteraction.Ignore: 구역 트리거가 아니라 실제 바닥 콜라이더 위에 떨어뜨리기 위해서다.
        if (Physics.Raycast(heldUmbrella.position, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            dropPosition = hit.point + Vector3.up * itemHoverHeight;

        // 떨어지면서 살짝 기울어져 "툭 놓친" 느낌을 준다.
        Vector3 tilt = new Vector3(0f, heldUmbrella.eulerAngles.y + 30f, 25f);

        _dropSequence?.Kill();
        _dropSequence = DOTween.Sequence()
            .Append(heldUmbrella.DOMove(dropPosition, dropDuration).SetEase(Ease.InQuad))
            .Join(heldUmbrella.DORotate(tilt, dropDuration).SetEase(Ease.OutQuad))
            .OnComplete(SpawnItem)
            .SetLink(heldUmbrella.gameObject);
    }

    // 떨어진 우산을 같은 자리의 "주울 수 있는 아이템"으로 바꿔 준다.
    private void SpawnItem()
    {
        Vector3 position = heldUmbrella.position;
        Destroy(heldUmbrella.gameObject);

        UmbrellaUnlockItem item = Instantiate(itemPrefab, position, Quaternion.identity);
        item.Setup(toolManager, noticePanel);
    }

    // 잠깐 움찔한 뒤 날갯짓(걷기 애니메이션)을 하며 비스듬히 날아올라 사라진다.
    // 콜라이더를 꺼 둔 kinematic 오브젝트라 transform을 DOTween으로 옮겨도 물리 충돌과 부딪히지 않는다.
    private void FlyAway()
    {
        Vector3 target = transform.TransformPoint(flyOffset);

        _flySequence?.Kill();
        _flySequence = DOTween.Sequence()
            .AppendInterval(flinchDelay)
            .AppendCallback(() => _enemyAnim?.SetMoving(true))
            .Append(transform.DOMove(target, flyDuration).SetEase(flyEase))
            .OnComplete(() => gameObject.SetActive(false));
    }

    [Button("한 대 맞기 테스트 (Play Mode)")]
    private void TestHit()
    {
        if (Application.isPlaying) _enemy.TakeDamage(1f);
    }
}
