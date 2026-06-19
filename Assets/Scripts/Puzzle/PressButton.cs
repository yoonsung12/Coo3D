using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

// 플레이어나 WoodenBox가 가까이 오면 눌리는 바닥 버튼이다.
// 벗어나면 다시 올라오며, 문이 열릴 때만 영구적으로 잠긴다.
public class PressButton : MonoBehaviour
{
    public enum ButtonState { Active, Pressed, Locked }

    [Title("버튼 오브젝트 연결")]
    [SerializeField, LabelText("눌리는 윗면 오브젝트")]
    private Transform buttonTop;
    // Inspector에서 버튼의 윗면 오브젝트를 연결한다. 눌릴 때 Y축으로 살짝 내려간다.

    [SerializeField, LabelText("버튼 Renderer")]
    private Renderer buttonRenderer;
    // Inspector에서 색상을 변경할 MeshRenderer를 연결한다.

    [Title("연출 설정")]
    [SerializeField, LabelText("눌림 깊이")]
    private float pressDepth = 0.08f;
    // 버튼 윗면이 눌릴 때 Y축으로 내려가는 거리다.

    [SerializeField, LabelText("눌림/해제 시간")]
    private float pressDuration = 0.15f;

    [FoldoutGroup("색상 설정")]
    [SerializeField, LabelText("대기 색상")]
    private Color activeColor = Color.green;
    // 아직 아무도 밟지 않은 상태 — "밟아줘"를 의미하는 초록색이다.

    [FoldoutGroup("색상 설정")]
    [SerializeField, LabelText("눌림/잠김 색상")]
    private Color pressedColor = Color.red;
    // 밟히고 있거나 영구 잠긴 상태 — 활성화됐음을 의미하는 빨간색이다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    public ButtonState State { get; private set; } = ButtonState.Active;

    [ReadOnly, ShowInInspector, LabelText("감지된 오브젝트 수")]
    private int _presserCount;

    // 상태가 변경될 때 DoorController에 알리기 위한 이벤트
    public System.Action<PressButton> OnStateChanged;

    private Vector3 _topOriginalLocalPos;
    private Tween _moveTween;
    private Tween _colorTween;
    private Material _buttonMat;

    private void Awake()
    {
        if (buttonTop != null)
            _topOriginalLocalPos = buttonTop.localPosition;

        // 개별 머티리얼 인스턴스를 생성해 버튼마다 색상을 독립적으로 관리한다.
        if (buttonRenderer != null)
            _buttonMat = buttonRenderer.material;
    }

    private void Start()
    {
        SetColorImmediate(activeColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 영구 잠긴 버튼은 더 이상 반응하지 않는다.
        if (State == ButtonState.Locked) return;
        if (!IsValidPresser(other)) return;

        _presserCount++;

        if (State == ButtonState.Active)
            ChangeState(ButtonState.Pressed);
    }

    private void OnTriggerExit(Collider other)
    {
        if (State == ButtonState.Locked) return;
        if (!IsValidPresser(other)) return;

        _presserCount = Mathf.Max(0, _presserCount - 1);

        // 모든 오브젝트가 벗어나면 다시 Active로 복원한다.
        if (_presserCount == 0 && State == ButtonState.Pressed)
            ChangeState(ButtonState.Active);
    }

    // DoorController가 문이 열릴 때 호출해 버튼을 영구적으로 잠근다.
    public void Lock()
    {
        if (State == ButtonState.Locked) return;
        State = ButtonState.Locked;
        // 잠김 상태는 Pressed와 동일한 시각 연출을 유지한다.
        OnStateChanged?.Invoke(this);
    }

    private bool IsValidPresser(Collider col)
    {
        // PlayerController와 WoodenBox만 버튼을 누를 수 있다.
        return col.GetComponent<PlayerController>() != null
            || col.GetComponent<WoodenBox>() != null;
    }

    private void ChangeState(ButtonState newState)
    {
        State = newState;
        PlayAnimation(newState);
        OnStateChanged?.Invoke(this);
    }

    private void PlayAnimation(ButtonState newState)
    {
        if (buttonTop != null)
        {
            _moveTween?.Kill();
            float targetY = newState == ButtonState.Pressed
                ? _topOriginalLocalPos.y - pressDepth
                : _topOriginalLocalPos.y;
            // 버튼 윗면을 Y축으로 눌리거나 올라오는 연출이다.
            _moveTween = buttonTop
                .DOLocalMoveY(targetY, pressDuration)
                .SetEase(Ease.OutQuad);
        }

        Color targetColor = newState == ButtonState.Active ? activeColor : pressedColor;
        TweenColor(targetColor);
    }

    private void SetColorImmediate(Color color)
    {
        if (_buttonMat != null)
            _buttonMat.SetColor("_BaseColor", color);
    }

    private void TweenColor(Color targetColor)
    {
        if (_buttonMat == null) return;

        _colorTween?.Kill();
        // URP 셰이더의 _BaseColor 프로퍼티를 지정해 색상을 부드럽게 전환한다.
        _colorTween = DOTween.To(
            () => _buttonMat.GetColor("_BaseColor"),
            x  => _buttonMat.SetColor("_BaseColor", x),
            targetColor,
            pressDuration
        );
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _colorTween?.Kill();
    }

    [Button("눌림 테스트")]
    private void TestPress()
    {
        if (State == ButtonState.Active) ChangeState(ButtonState.Pressed);
    }

    [Button("해제 테스트")]
    private void TestRelease()
    {
        if (State == ButtonState.Pressed) ChangeState(ButtonState.Active);
    }

    [Button("잠금 테스트")]
    private void TestLock() => Lock();

    [Button("상태 초기화")]
    public void ResetButton()
    {
        _presserCount = 0;
        State = ButtonState.Active;
        PlayAnimation(ButtonState.Active);
    }
}
