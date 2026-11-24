using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// "몇 번 맞혀야 하는지"를 칸(●○○)으로 보여 주는 표시다.
// 가을 패턴(뿌리 내려찍기)에서 플레이어가 필요한 명중 횟수를 몰라 헤매는 문제를 돕기 위해 만들었다.
// 패턴이 Show(필요 횟수) → SetHits(현재 횟수) → Hide() 순서로 부른다. 판정에는 관여하지 않는다.
// 화면 UI(보스 체력바 아래)에 두는 것이 기본이다. 이 오브젝트에 World Space Canvas가 붙어 있으면
// 월드에 떠 있는 표시로 보고 항상 카메라를 바라보게 돌린다(보스가 커서 머리 위 표시는 가려져 지금은 쓰지 않는다).
public class BossHitCounter : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("CanvasGroup")]
    private CanvasGroup canvasGroup;
    // 전체를 페이드하기 위한 CanvasGroup이다. 이 오브젝트(World Space Canvas)에 붙인 것을 연결한다.

    [SerializeField, LabelText("칸 부모")]
    private Transform pipParent;
    // 칸들이 나란히 놓일 부모다. Horizontal Layout Group을 붙여 두면 개수가 바뀌어도 자동으로 정렬된다.

    [SerializeField, LabelText("칸 원본")]
    private Image pipTemplate;
    // 칸 하나의 원본 Image다. 꺼 둔 상태로 두면 Show()에서 필요 횟수만큼 복제해 쓴다.

    [SerializeField, LabelText("바라볼 카메라 (월드 표시일 때만, 비우면 Main Camera)")]
    private Camera targetCamera;

    [Title("색")]
    [SerializeField, LabelText("빈 칸 색")]
    private Color emptyColor = new Color(1f, 1f, 1f, 0.35f);

    [SerializeField, LabelText("채운 칸 색")]
    private Color filledColor = new Color(1f, 0.8f, 0.2f, 1f);

    [Title("연출 (DOTween)")]
    [SerializeField, LabelText("나타남/사라짐 시간")]
    private float fadeDuration = 0.3f;

    [SerializeField, LabelText("채울 때 펀치 크기")]
    private float fillPunch = 0.6f;
    // 칸이 채워지는 순간 크기가 이만큼 커졌다 돌아온다. 0이면 펀치 없이 색만 바뀐다.

    [SerializeField, LabelText("채울 때 펀치 시간")]
    private float fillPunchDuration = 0.35f;

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("명중 / 필요")]
    private string StateText => _hits + " / " + _pips.Count;

    private readonly List<Image> _pips = new List<Image>();
    // Show()에서 만든 칸들이다. 다음 Show()에서 개수가 다르면 다시 만든다.
    private int _hits;

    private Tween _fadeTween;
    // 나타남/사라짐 Tween. 겹치지 않게 저장해 두고 끊는다.
    private readonly List<Tween> _pipTweens = new List<Tween>();
    // 칸마다 실행 중인 펀치/색 Tween이다. 파괴·비활성화 때 함께 정리한다.

    private bool _faceCamera;
    // 이 오브젝트가 World Space Canvas일 때만 true다. 화면 UI에서는 돌리면 안 되므로 꺼 둔다.

    private void Awake()
    {
        Canvas ownCanvas = GetComponent<Canvas>();
        _faceCamera = ownCanvas != null && ownCanvas.renderMode == RenderMode.WorldSpace;

        if (pipTemplate != null) pipTemplate.gameObject.SetActive(false);

        // 처음에는 투명하게 둔다. 패턴이 Show()를 부르면 나타난다.
        // 오브젝트 자체는 켜 둔 채 알파만 바꾼다. 꺼 두면 Show()에서 켜는 순간 Awake가 늦게 돌며 다시 숨겨 버리기 때문이다.
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    // 보스가 회전하거나 쓰러져도 숫자가 뒤집히지 않도록, 매 프레임 카메라와 같은 방향을 보게 한다.
    // 카메라가 움직인 뒤에 맞춰야 떨리지 않으므로 LateUpdate에서 한다.
    private void LateUpdate()
    {
        if (!_faceCamera) return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }

    // 필요 횟수만큼 빈 칸을 만들고 나타난다.
    public void Show(int required)
    {
        if (pipTemplate == null || pipParent == null) return;

        BuildPips(Mathf.Max(required, 0));
        SetHits(0, false);

        _fadeTween?.Kill();
        if (canvasGroup != null)
            _fadeTween = canvasGroup.DOFade(1f, fadeDuration);
    }

    // 명중 횟수를 반영한다. 새로 채워진 칸에만 펀치 연출을 건다.
    public void SetHits(int hits) => SetHits(hits, true);

    private void SetHits(int hits, bool animate)
    {
        int previous = _hits;
        _hits = Mathf.Clamp(hits, 0, _pips.Count);

        for (int i = 0; i < _pips.Count; i++)
        {
            Image pip = _pips[i];
            bool filled = i < _hits;
            bool justFilled = animate && filled && i >= previous;

            if (!justFilled)
            {
                pip.color = filled ? filledColor : emptyColor;
                continue;
            }

            // 방금 채워진 칸: 색을 바꾸고 크게 튕겨서 "하나 맞혔다"를 눈에 띄게 한다.
            _pipTweens.RemoveAll(t => t == null || !t.IsActive()); // 끝난 Tween은 목록에서 비운다.
            pip.transform.localScale = Vector3.one;
            _pipTweens.Add(pip.DOColor(filledColor, fillPunchDuration * 0.5f));
            if (fillPunch > 0f)
                _pipTweens.Add(pip.transform.DOPunchScale(Vector3.one * fillPunch, fillPunchDuration, 6, 0.6f));
        }
    }

    // 서서히 투명해진다. delay를 주면 그만큼 보여 준 뒤 사라진다(마지막 칸이 채워지는 모습을 보여 주기 위해).
    public void Hide(float delay = 0f)
    {
        _fadeTween?.Kill();
        if (canvasGroup != null)
            _fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetDelay(delay);
    }

    // 개수가 바뀌었을 때만 칸을 새로 만든다. 같으면 있던 칸을 재사용한다.
    private void BuildPips(int count)
    {
        if (_pips.Count == count) return;

        foreach (Image pip in _pips)
        {
            if (pip != null) Destroy(pip.gameObject);
        }
        _pips.Clear();

        for (int i = 0; i < count; i++)
        {
            Image pip = Instantiate(pipTemplate, pipParent);
            pip.name = "Pip_" + (i + 1);
            pip.gameObject.SetActive(true);
            _pips.Add(pip);
        }
    }

    private void KillTweens()
    {
        _fadeTween?.Kill();
        foreach (Tween t in _pipTweens) t?.Kill();
        _pipTweens.Clear();
    }

    [Button("3칸 보이기 테스트 (Play Mode)")]
    private void TestShow()
    {
        if (!Application.isPlaying) return;
        Show(3);
    }

    [Button("명중 +1 테스트 (Play Mode)")]
    private void TestAddHit()
    {
        if (!Application.isPlaying) return;
        SetHits(_hits + 1);
    }
}
