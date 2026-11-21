using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 머리 위에 표시되는 월드 스페이스 스태미나 바다.
// 가득 차 있으면 숨기고, 줄어들면 나타나며, 소진되면 흔들린다.
public class StaminaUI : MonoBehaviour
{
    [Title("참조")]
    [SerializeField, LabelText("플레이어 스태미나 (비우면 부모에서 자동)")]
    private PlayerStamina stamina;

    [SerializeField, LabelText("채움 이미지 (Filled)")]
    private Image fillImage;

    [SerializeField, LabelText("캔버스 그룹")]
    private CanvasGroup canvasGroup;

    [Title("색상 설정")]
    [SerializeField, LabelText("정상 색상")]
    private Color normalColor = new Color(0.4f, 0.9f, 0.4f);

    [SerializeField, LabelText("경고 색상 (재사용 비율 미만/소진)")]
    private Color warningColor = new Color(1f, 0.45f, 0.2f);

    [Title("연출 설정")]
    [SerializeField, LabelText("채움 변화 시간")]
    private float fillDuration = 0.15f;

    [SerializeField, LabelText("페이드 시간")]
    private float fadeDuration = 0.25f;

    [SerializeField, LabelText("가득 찬 뒤 숨김 지연")]
    private float hideDelay = 0.6f;

    [SerializeField, LabelText("소진 시 흔들림 강도")]
    private float shakeStrength = 6f;

    [SerializeField, LabelText("소진 시 흔들림 시간")]
    private float shakeDuration = 0.3f;

    private Tween _fillTween;
    private Tween _fadeTween;
    private Tween _shakeTween;
    private Camera _cam;
    private bool _visible;

    private void Awake()
    {
        if (stamina == null)
            stamina = GetComponentInParent<PlayerStamina>();
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        if (stamina == null) return;
        stamina.OnStaminaChanged += HandleChanged;
        stamina.OnExhausted += HandleExhausted;
        RefreshInstant();
    }

    private void OnDisable()
    {
        if (stamina != null)
        {
            stamina.OnStaminaChanged -= HandleChanged;
            stamina.OnExhausted -= HandleExhausted;
        }
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void KillTweens()
    {
        _fillTween?.Kill();
        _fadeTween?.Kill();
        _shakeTween?.Kill();
    }

    private void LateUpdate()
    {
        // 사이드뷰/탑다운 모두에서 바가 정면으로 보이도록 카메라 회전을 그대로 따른다.
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            transform.rotation = _cam.transform.rotation;
    }

    private void RefreshInstant()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = stamina.Normalized;
            fillImage.color = GetColor();
        }
        _visible = stamina.Current < stamina.Max;
        if (canvasGroup != null)
            canvasGroup.alpha = _visible ? 1f : 0f;
    }

    private Color GetColor()
    {
        bool warn = stamina.IsExhausted || stamina.Normalized < stamina.ReuseRatio;
        return warn ? warningColor : normalColor;
    }

    private void HandleChanged(float current, float max)
    {
        if (fillImage != null)
        {
            fillImage.color = GetColor();
            _fillTween?.Kill();
            _fillTween = fillImage.DOFillAmount(max > 0f ? current / max : 0f, fillDuration);
        }

        if (canvasGroup == null) return;

        if (current < max)
        {
            // 줄어들었거나 회복 중이면 바를 보여준다.
            if (!_visible)
            {
                _visible = true;
                _fadeTween?.Kill();
                _fadeTween = canvasGroup.DOFade(1f, fadeDuration);
            }
        }
        else if (_visible)
        {
            // 가득 찼으면 잠깐 뒤에 숨긴다.
            _visible = false;
            _fadeTween?.Kill();
            _fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetDelay(hideDelay);
        }
    }

    private void HandleExhausted()
    {
        if (fillImage != null)
            fillImage.color = warningColor;

        _shakeTween?.Kill();
        transform.localPosition = _restLocalPos;
        _shakeTween = transform
            .DOShakePosition(shakeDuration, strength: shakeStrength * 0.01f, vibrato: 20, randomness: 90);
        // DOShakePosition은 localPosition 기준으로 흔들고 끝나면 시작 위치로 돌아온다.
        // 바는 플레이어 자식이므로 흔들리는 중에 플레이어가 움직여도 함께 따라간다.
    }

    private Vector3 _restLocalPos;
    private void Start()
    {
        // 흔들림 후 원위치 복원 기준이다.
        _restLocalPos = transform.localPosition;

        // OnEnable은 PlayerStamina.Awake()보다 먼저 실행될 수 있어 그때는 값이 0으로 읽힐 수 있다.
        // 모든 Awake가 끝난 Start에서 한 번 더 맞춰야 시작할 때 빈 바가 떠 있지 않는다.
        if (stamina != null)
            RefreshInstant();
    }
}
