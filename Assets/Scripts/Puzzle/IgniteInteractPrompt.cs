using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

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

    [Title("클릭 유도 아이콘")]
    [SerializeField, LabelText("마우스 아이콘 Image")]
    private Image mouseIconImage;
    // 프롬프트 Canvas 안의 마우스 아이콘 Image를 연결한다. 비워두면 기존처럼 텍스트만 표시된다.

    [SerializeField, LabelText("아이콘 A (클릭)")]
    private Sprite clickSprite;
    // 왼쪽 버튼이 강조된 마우스 스프라이트(mouse_paper_strip9_0)를 연결한다.

    [SerializeField, LabelText("아이콘 B (기본)")]
    private Sprite idleSprite;
    // 아무 버튼도 강조되지 않은 마우스 스프라이트(mouse_paper_strip9_8)를 연결한다.

    [SerializeField, LabelText("전환 간격(초)")]
    private float iconSwapInterval = 0.4f;
    // 두 스프라이트가 번갈아 바뀌는 간격이다. 값이 작을수록 더 빠르게 깜빡여 클릭을 재촉하는 느낌이 난다.

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
    private Sequence _iconSwapSequence;
    // 아이콘 깜빡임 반복 연출이다. 프롬프트가 숨겨질 때마다 Kill()해서 다시 보일 때 중복 실행되지 않게 한다.
    private bool _isPromptVisible;

    private void Awake()
    {
        _triggerCollider = GetComponent<SphereCollider>();
        _triggerCollider.isTrigger = true;
        _triggerCollider.radius = detectRadius;

        if (promptCanvasGroup != null)
            promptCanvasGroup.alpha = 0f;

        if (mouseIconImage != null && clickSprite != null)
            mouseIconImage.sprite = clickSprite;
    }

    // 보스전 뿌리(FuseLine)처럼 부모가 SetActive(false)로 꺼지는 경우가 있어서, 무한 반복 연출이
    // 꺼진 오브젝트를 계속 건드리지 않도록 비활성화 시에도 정리하고 다음 활성화 때 처음부터 판정하게 한다.
    private void OnDisable()
    {
        _promptFadeTween?.Kill();
        StopIconSwap();
        _isPromptVisible = false;
        _playerInRange = false;
        if (promptCanvasGroup != null)
            promptCanvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        _promptFadeTween?.Kill();
        _iconSwapSequence?.Kill();
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

        if (visible) StartIconSwap();
        else StopIconSwap();
    }

    // 클릭 스프라이트와 기본 스프라이트를 일정 간격으로 번갈아 보여주는 반복 연출을 시작한다.
    // Update()에서 타이머를 세는 대신 DOTween Sequence(교체 → 대기 → 교체 → 대기)를 무한 반복시킨다.
    private void StartIconSwap()
    {
        if (mouseIconImage == null || clickSprite == null || idleSprite == null) return;

        _iconSwapSequence?.Kill();
        mouseIconImage.sprite = clickSprite;

        _iconSwapSequence = DOTween.Sequence()
            .AppendInterval(iconSwapInterval)
            .AppendCallback(() => mouseIconImage.sprite = idleSprite)
            .AppendInterval(iconSwapInterval)
            .AppendCallback(() => mouseIconImage.sprite = clickSprite)
            .SetLoops(-1);
    }

    // 깜빡임을 멈추고, 다음에 다시 보일 때 항상 클릭 스프라이트부터 시작하도록 되돌린다.
    private void StopIconSwap()
    {
        _iconSwapSequence?.Kill();
        _iconSwapSequence = null;

        if (mouseIconImage != null && clickSprite != null)
            mouseIconImage.sprite = clickSprite;
    }

    [Button("아이콘 깜빡임 테스트")]
    private void TestIconSwap() => StartIconSwap();
}
