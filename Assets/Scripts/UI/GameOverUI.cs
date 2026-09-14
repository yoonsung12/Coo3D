using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력이 0이 되어 사망했을 때 표시되는 게임오버 화면이다.
// PlayerHealth.OnDeath 이벤트를 구독해 자동으로 뜨고, 시간을 멈춘 뒤
// 어두운 오버레이 -> GAME OVER 텍스트 -> 계속하기 버튼 순서로 연출한다.
// 계속하기를 누르면 시간을 재개하고 PlayerHealth를 체크포인트로 리스폰시킨다.
public class GameOverUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("패널 (CanvasGroup)")]
    private CanvasGroup panel;
    // 계속하기 버튼 클릭 시 이 오브젝트를 SetActive(false)로 끄면, 클릭 처리 중인 버튼의 부모를
    // 같은 프레임에 비활성화하는 셈이라 클릭이 씹힐 수 있다(ConfirmPopupUI와 동일한 이유).
    // 그래서 오브젝트는 항상 켜둔 채 CanvasGroup으로만 보이기/입력 차단을 제어한다.

    [SerializeField, LabelText("어두운 오버레이")]
    private Image darkOverlay;
    // 화면 전체를 덮는 반투명 검정 Image다. 처음엔 투명, DOTween으로 점점 어두워진다.

    [SerializeField, LabelText("GAME OVER 텍스트")]
    private Text gameOverText;

    [SerializeField, LabelText("계속하기 버튼")]
    private Button continueButton;
    // 체크포인트 위치로 리스폰하는 버튼이다. 텍스트 등장이 끝난 뒤에 나타난다.

    [Title("사망 판정 대상")]
    [SerializeField, LabelText("Player Health")]
    private PlayerHealth playerHealth;
    // Inspector에서 Player를 연결한다. 이 컴포넌트의 OnDeath 이벤트를 구독해 자동으로 Show()를 호출한다.

    [Title("연출 설정")]
    [SerializeField, LabelText("오버레이 목표 알파")]
    private float overlayTargetAlpha = 0.75f;
    // 값이 클수록 화면이 더 어두워진다. 1이면 완전 검정.

    [SerializeField, LabelText("오버레이 페이드 시간")]
    private float overlayFadeDuration = 0.5f;

    [SerializeField, LabelText("텍스트 등장 지연")]
    private float textDelay = 0.3f;
    // 오버레이가 어느 정도 깔린 뒤 텍스트가 등장하기까지의 지연 시간이다.

    [SerializeField, LabelText("텍스트 등장 시간")]
    private float textFadeDuration = 0.4f;

    private Sequence _showSequence;
    // 등장 연출 Sequence다. 중복 실행 방지를 위해 저장해 둔다.

    private void Awake()
    {
        SetVisible(false);

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath += Show;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= Show;
    }

    private void OnDestroy()
    {
        _showSequence?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    // 게임오버 화면을 표시한다. PlayerHealth.OnDeath에서 자동 호출되고, 테스트 버튼으로도 호출할 수 있다.
    public void Show()
    {
        // 계속하기를 누르기 전까지 게임 진행(적 이동, 물리 등)이 멈춰야 하므로 시간을 정지시킨다.
        Time.timeScale = 0f;

        SetVisible(true);

        // 연출 시작 전 초기 상태(완전 투명)로 되돌린다.
        if (darkOverlay != null)
        {
            Color c = darkOverlay.color;
            darkOverlay.color = new Color(c.r, c.g, c.b, 0f);
        }

        if (gameOverText != null)
        {
            Color c = gameOverText.color;
            gameOverText.color = new Color(c.r, c.g, c.b, 0f);
        }

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        _showSequence?.Kill();
        _showSequence = DOTween.Sequence();
        // Time.timeScale = 0 상태이므로 SetUpdate(true)로 Unscaled Time 기준으로 연출을 재생한다.
        _showSequence.SetUpdate(true);

        // 1단계: 화면이 점차 어두워진다.
        if (darkOverlay != null)
            _showSequence.Append(darkOverlay.DOFade(overlayTargetAlpha, overlayFadeDuration).SetEase(Ease.InQuad).SetUpdate(true));

        // 2단계: 지연 후 GAME OVER 텍스트가 서서히 등장한다.
        if (gameOverText != null)
        {
            _showSequence.AppendInterval(textDelay);
            _showSequence.Append(gameOverText.DOFade(1f, textFadeDuration).SetEase(Ease.OutQuad).SetUpdate(true));
        }

        // 3단계: 텍스트 등장 후 계속하기 버튼을 표시한다.
        if (continueButton != null)
            _showSequence.AppendCallback(() => continueButton.gameObject.SetActive(true));
    }

    // 계속하기 버튼에 연결된다. 시간을 재개하고 체크포인트 위치로 플레이어를 되돌린다.
    private void HandleContinueClicked()
    {
        _showSequence?.Kill();
        SetVisible(false);

        Time.timeScale = 1f;

        if (playerHealth != null)
            playerHealth.RespawnAtCheckpoint();
    }

    private void SetVisible(bool visible)
    {
        if (panel == null) return;

        panel.alpha = visible ? 1f : 0f;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }

    [Button("게임오버 화면 테스트 (Play Mode)")]
    private void TestShow()
    {
        if (!Application.isPlaying) return;
        Show();
    }
}
