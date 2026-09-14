using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

// 구슬 조각 수집 상태를 관리하는 컴포넌트다. Player 오브젝트에 붙인다.
// 조각을 모두 모으면 OnMarbleCompleted 이벤트를 발행해 AltarRestoreEffect 등이 반응할 수 있게 한다.
public class MarbleInventory : MonoBehaviour
{
    public static MarbleInventory Instance { get; private set; }

    [Title("구슬 조각 설정")]
    [SerializeField, LabelText("완성에 필요한 조각 수")]
    private int requiredFragments = 3;
    // 이 수만큼 조각을 모으면 온전한 구슬이 완성된다.

    [Title("현재 상태")]
    [SerializeField, LabelText("수집한 조각 수"), ReadOnly]
    private int fragmentsCollected = 0;
    // Play Mode에서 현재 몇 개를 모았는지 Inspector에서 바로 확인할 수 있다.

    // 구슬이 완성됐는지 여부. 제단 상호작용 시 확인한다.
    public bool IsMarbleComplete => fragmentsCollected >= requiredFragments;

    // 조각 1개 획득 시 발행된다. (현재 수집 수, 필요 수)를 함께 전달한다.
    public event Action<int, int> OnFragmentCollected;

    // 필요 수를 모두 채웠을 때 1회 발행된다.
    public event Action OnMarbleCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬(스테이지)이 바뀌면 이전 씬에서 모은 조각은 초기화한다.
        fragmentsCollected = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 구슬 조각 1개를 추가한다. MarbleFragment에서 호출한다.
    public void AddFragment()
    {
        if (IsMarbleComplete) return;
        // 이미 완성된 상태에서 추가 획득이 들어오지 않도록 막는다.

        fragmentsCollected++;
        OnFragmentCollected?.Invoke(fragmentsCollected, requiredFragments);

        if (IsMarbleComplete)
            OnMarbleCompleted?.Invoke();
    }

    // 현재 수집 상태를 반환한다. UI 표시, AltarRestoreEffect의 부족 안내 등에서 사용한다.
    public (int collected, int required) GetFragmentStatus()
        => (fragmentsCollected, requiredFragments);

    [Button("조각 1개 추가 (테스트)")]
    private void TestAddFragment()
    {
        AddFragment();
    }

    [Button("상태 초기화 (테스트)")]
    private void TestReset()
    {
        fragmentsCollected = 0;
    }
}
