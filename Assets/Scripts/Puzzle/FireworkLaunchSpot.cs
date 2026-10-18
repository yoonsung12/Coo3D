using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;

// 고드름 아래 바닥에 정해진 불꽃놀이 설치 자리다. 범위 안에서 Interact(Space 꾹)를 누르면
// 가방의 불꽃놀이를 하나 써서 발사대(Firework)를 보이게 한다. 한 번 설치하면 더 이상 반응하지 않는다.
// VendingMachine과 같은 방식(Trigger 범위 + Player 맵의 Interact 액션 + 월드 프롬프트)을 따른다.
[RequireComponent(typeof(Collider))]
public class FireworkLaunchSpot : MonoBehaviour
{
    [Title("입력 설정")]
    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // Inspector에서 Assets/InputSystem_Actions 에셋을 연결한다. Player 맵의 Interact 액션을 사용한다.

    [Title("연결")]
    [SerializeField, LabelText("설치될 불꽃놀이")]
    private Firework firework;
    // 처음엔 비활성화해 둔 발사대 오브젝트를 연결한다. 설치하면 활성화된다.

    [SerializeField, LabelText("바닥 표시 (선택)")]
    private GameObject marker;
    // 설치 자리를 알려주는 바닥 표시다. 설치 후에는 숨긴다.

    [Title("상호작용 프롬프트")]
    [SerializeField, LabelText("프롬프트 Canvas Group")]
    private CanvasGroup promptCanvasGroup;

    [SerializeField, LabelText("프롬프트 텍스트")]
    private Text promptText;
    // "Space(꾹): 불꽃놀이 설치 (보유 N)" 또는 "불꽃놀이가 필요하다"를 보여준다. Interact 액션은 Space 꾹 누르기(Hold)다.
    // 프로젝트의 다른 프롬프트(EntrancePortal 등)와 같은 UI Text를 쓴다. TMP 기본 폰트에는 한글이 없기 때문이다.

    [SerializeField, LabelText("프롬프트 페이드 시간")]
    private float promptFadeDuration = 0.2f;

    [Title("설치 연출")]
    [SerializeField, LabelText("설치 팝인 시간")]
    private float installPopDuration = 0.3f;

    [SerializeField, LabelText("설치 팝인 Ease")]
    private Ease installPopEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 범위 안에 있는지")]
    private bool _playerInRange;

    [ReadOnly, ShowInInspector, LabelText("설치 완료")]
    private bool _installed;

    private InputAction _interactAction;
    private Tween _promptFadeTween;
    private Tween _popTween;
    private FireworkBag _subscribedBag;

    private void Awake()
    {
        var playerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        _interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);

        if (promptCanvasGroup != null)
            promptCanvasGroup.alpha = 0f;
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

        UnsubscribeBag();
        _promptFadeTween?.Kill();
        _popTween?.Kill();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_installed) return;
        if (other.GetComponent<PlayerController>() == null) return;

        _playerInRange = true;
        SubscribeBag();
        RefreshPromptText();
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        _playerInRange = false;
        UnsubscribeBag();
        SetPromptVisible(false);
    }

    // 범위 안에 있는 동안만 가방 개수 변화를 구독해 문구를 갱신한다.
    private void SubscribeBag()
    {
        if (_subscribedBag != null || FireworkBag.Instance == null) return;
        _subscribedBag = FireworkBag.Instance;
        _subscribedBag.OnCountChanged += OnBagCountChanged;
    }

    private void UnsubscribeBag()
    {
        if (_subscribedBag == null) return;
        _subscribedBag.OnCountChanged -= OnBagCountChanged;
        _subscribedBag = null;
    }

    private void OnBagCountChanged(int count)
    {
        RefreshPromptText();
    }

    private void RefreshPromptText()
    {
        if (promptText == null) return;

        int count = FireworkBag.Instance != null ? FireworkBag.Instance.Count : 0;
        promptText.text = count > 0 ? $"Space(꾹): 불꽃놀이 설치 (보유 {count})" : "불꽃놀이가 필요하다";
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptCanvasGroup == null) return;

        _promptFadeTween?.Kill();
        _promptFadeTween = promptCanvasGroup.DOFade(visible ? 1f : 0f, promptFadeDuration);
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!_playerInRange || _installed) return;
        TryInstall();
    }

    // 가방에서 하나 소비에 성공하면 발사대를 보이게 한다. 개수가 없으면 아무 일도 없다.
    private void TryInstall()
    {
        if (_installed || firework == null) return;
        if (FireworkBag.Instance == null || !FireworkBag.Instance.TryUse()) return;

        _installed = true;

        Transform fw = firework.transform;
        Vector3 fullScale = fw.localScale;
        fw.localScale = Vector3.zero;
        firework.gameObject.SetActive(true);

        _popTween?.Kill();
        _popTween = fw.DOScale(fullScale, installPopDuration).SetEase(installPopEase);

        if (marker != null) marker.SetActive(false);

        UnsubscribeBag();
        SetPromptVisible(false);
    }

    [Button("설치 테스트")]
    private void TestInstall()
    {
        if (!Application.isPlaying) return;
        TryInstall();
    }
}
