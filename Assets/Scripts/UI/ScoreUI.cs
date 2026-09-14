using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// ScoreManager의 누적 점수를 화면에 표시한다.
// ScoreManager.OnScoreChanged 이벤트를 구독해 점수가 바뀔 때마다 자동으로 갱신하고,
// CoinUI와 동일하게 DOTween 펀치 스케일로 갱신을 강조한다.
public class ScoreUI : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("점수 텍스트")]
    private Text scoreText;
    // Inspector에서 화면 우상단에 배치한 Text를 연결한다.

    [Title("표시 설정")]
    [SerializeField, LabelText("점수 포맷")]
    private string scoreFormat = "SCORE\n{0:D6}";
    // 점수를 표시하는 형식 문자열이다.
    // {0:D6}이면 6자리 0 패딩(예: 000150)으로 표시된다.

    [Title("펀치 연출 설정")]
    [SerializeField, LabelText("펀치 스케일 강도")]
    private float punchScale = 0.3f;

    [SerializeField, LabelText("펀치 지속 시간")]
    private float punchDuration = 0.25f;

    private Vector3 _originalScale;
    private Tween _popTween;

    private bool _subscribed;
    // GameCanvas(ScoreUI)가 ScoreManager보다 하이어라키 상 먼저 있어서, OnEnable 시점엔
    // ScoreManager.Instance가 아직 Awake로 채워지기 전(null)일 수 있다. 그 경우를 대비해
    // Start에서 한 번 더 구독을 시도하고, 이 플래그로 중복 구독을 막는다.

    private void Awake()
    {
        if (scoreText != null)
            _originalScale = scoreText.transform.localScale;
    }

    private void Start()
    {
        TrySubscribe();

        // ScoreManager가 Awake에서 ES3 저장값을 불러오므로 Start에서 읽어야 초기값이 정확하다.
        int initialScore = ScoreManager.Instance != null ? ScoreManager.Instance.TotalScore : 0;
        UpdateText(initialScore);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (!_subscribed || ScoreManager.Instance == null) return;

        ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
        _subscribed = false;
    }

    private void TrySubscribe()
    {
        if (_subscribed || ScoreManager.Instance == null) return;

        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        _popTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    private void HandleScoreChanged(int newScore)
    {
        UpdateText(newScore);
        PlayPopAnimation();
    }

    private void UpdateText(int score)
    {
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, score);
    }

    private void PlayPopAnimation()
    {
        if (scoreText == null) return;

        _popTween?.Kill();
        scoreText.transform.localScale = _originalScale;
        _popTween = scoreText.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 8, elasticity: 0.6f);
    }

    [Button("점수 갱신 연출 테스트")]
    private void TestPop()
    {
        if (!Application.isPlaying) return;
        PlayPopAnimation();
    }
}
