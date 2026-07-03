using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 상호작용 범위 안에서 Interact 입력을 받으면 코인을 소비해 MaxHP 아이템을 생성하는 자판기다.
[RequireComponent(typeof(Collider))]
public class VendingMachine : MonoBehaviour
{
    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다. Player 맵의 Interact 액션을 사용한다.

    [Title("판매 설정")]
    [SerializeField, LabelText("필요 코인")]
    private int coinCost = 5;

    [SerializeField, LabelText("생성할 MaxHP 아이템 프리팹")]
    private GameObject maxHPItemPrefab;

    [SerializeField, LabelText("아이템 생성 위치 (비워두면 이 오브젝트 위치 사용)")]
    private Transform spawnPoint;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 범위 안에 있는지")]
    private bool _playerInRange;

    private InputAction _interactAction;

    private void Awake()
    {
        // InputActionAsset에서 Player 맵의 Interact 액션을 찾아 연결한다.
        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _interactAction.Enable();
        _interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        _interactAction.performed -= OnInteractPerformed;
        _interactAction.Disable();
    }

    // 상호작용 범위(Trigger Collider)에 플레이어가 들어오고 나가는 것을 감지한다.
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            _playerInRange = false;
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!_playerInRange) return;
        TryPurchase();
    }

    private void TryPurchase()
    {
        if (maxHPItemPrefab == null) return;
        if (CoinManager.Instance == null) return;

        // 코인이 부족하면 TrySpendCoin이 false를 반환하고 아무것도 바뀌지 않는다.
        if (!CoinManager.Instance.TrySpendCoin(coinCost)) return;

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Instantiate(maxHPItemPrefab, position, Quaternion.identity);
    }

    [Button("테스트 구매 (범위 무시)")]
    private void TestPurchase()
    {
        TryPurchase();
        // Play Mode에서 범위 안에 들어가지 않고도 구매 로직만 빠르게 확인하기 위한 버튼이다.
    }
}
