using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 횃불이 켜진 상태로 점화 가능한 오브젝트(FlammableObject, FuseLine 등) 근처에 들어오면
// 머리 위에 "불붙이기" 프롬프트를 띄운다. VendingMachine의 상호작용 프롬프트와 같은 방식이다.
// FlammableObject/FuseLine이 있는 오브젝트에 직접 붙이지 않고 자식 오브젝트로 따로 둔다.
// 두 스크립트 다 GetComponent<Collider>()로 자기 콜라이더를 참조하기 때문에, 감지용 콜라이더를
// 같은 오브젝트에 하나 더 추가하면 어떤 콜라이더가 잡힐지 꼬일 수 있어서다.
[RequireComponent(typeof(SphereCollider))]
public class IgniteInteractPrompt : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("프롬프트 Canvas Group")]
    private CanvasGroup promptCanvasGroup;
    // 오브젝트 머리 위에 "불붙이기" 텍스트를 띄우는 World Space Canvas를 연결한다.

    [SerializeField, LabelText("프롬프트 페이드 시간")]
    private float promptFadeDuration = 0.2f;

    [Title("감지 설정")]
    [SerializeField, LabelText("감지 반경")]
    private float detectRadius = 1.5f;
    // TorchTool의 점화 범위와 비슷한 값으로 맞추면, 프롬프트가 뜨는 거리와 실제로
    // 불이 붙는 거리가 자연스럽게 일치한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어가 범위 안에 있는지")]
    private bool _playerInRange;

    private SphereCollider _triggerCollider;
    private Tween _promptFadeTween;
    private bool _isPromptVisible;

    private void Awake()
    {
        _triggerCollider = GetComponent<SphereCollider>();
        _triggerCollider.isTrigger = true;
        _triggerCollider.radius = detectRadius;

        if (promptCanvasGroup != null)
            promptCanvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        _promptFadeTween?.Kill();
    }

    private void Update()
    {
        // 범위 안에 있어도 횃불이 꺼져 있으면 프롬프트를 띄우지 않는다.
        // 매 프레임 확인해야, 범위 안에 서 있는 상태에서 나중에 횃불을 켜도 바로 반응한다.
        bool shouldShow = _playerInRange && TorchTool.IsTorchLit;
        if (shouldShow == _isPromptVisible) return;

        _isPromptVisible = shouldShow;
        SetPromptVisible(shouldShow);
    }

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

    // "불붙이기" 프롬프트를 DOTween 페이드로 보여주거나 숨긴다.
    private void SetPromptVisible(bool visible)
    {
        if (promptCanvasGroup == null) return;

        _promptFadeTween?.Kill();
        _promptFadeTween = promptCanvasGroup.DOFade(visible ? 1f : 0f, promptFadeDuration);
    }
}
