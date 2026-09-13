using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 화면 전체를 검은색으로 덮어 페이드 인/아웃 연출을 하는 공용 유틸리티다.
// 낙사/게임오버/스테이지 전환 등 여러 기능에서 재사용하므로 씬을 이동해도 파괴되지 않는 싱글턴으로 둔다.
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Title("연결")]
    [SerializeField, LabelText("페이드 패널 (CanvasGroup)")]
    private CanvasGroup fadePanel;
    // Inspector에서 화면을 덮는 검은 Image가 달린 CanvasGroup을 연결한다.

    [Title("페이드 연출 설정")]
    [SerializeField, LabelText("기본 페이드 시간")]
    private float defaultDuration = 0.5f;
    // FadeIn/FadeOut 호출 시 duration을 따로 넘기지 않으면 사용할 기본 시간이다.

    [SerializeField, LabelText("Ease 타입")]
    private Ease fadeEase = Ease.InOutQuad;

    private Tween _fadeTween;
    // 페이드 중 다시 호출되는 경우를 대비해 현재 실행 중인 Tween을 저장해 둔다.

    private void Awake()
    {
        // 씬 전환 후에도 페이더가 하나만 존재하도록 중복 생성을 막는다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetAlphaImmediate(0f);
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    // 화면을 검은색으로 덮는다. duration을 생략하면 기본 시간을 사용한다.
    public void FadeOut(Action onComplete = null, float duration = -1f)
    {
        Fade(1f, duration, onComplete);
    }

    // 덮인 화면을 다시 걷어낸다. duration을 생략하면 기본 시간을 사용한다.
    public void FadeIn(Action onComplete = null, float duration = -1f)
    {
        Fade(0f, duration, onComplete);
    }

    private void Fade(float targetAlpha, float duration, Action onComplete)
    {
        float actualDuration = duration >= 0f ? duration : defaultDuration;

        // CanvasGroup의 alpha가 0이면 입력을 막지 않아야 하므로, 덮이는 중(목표가 1)에만 막는다.
        fadePanel.blocksRaycasts = targetAlpha > 0f;

        _fadeTween?.Kill();
        _fadeTween = fadePanel
            .DOFade(targetAlpha, actualDuration)
            .SetEase(fadeEase)
            .SetUpdate(true)
            // 게임오버처럼 Time.timeScale이 0으로 멈춘 상태에서도 페이드가 정상적으로 진행되게 한다.
            .OnComplete(() => onComplete?.Invoke());
    }

    private void SetAlphaImmediate(float alpha)
    {
        fadePanel.alpha = alpha;
        fadePanel.blocksRaycasts = alpha > 0f;
    }

    [Button("페이드아웃 테스트")]
    private void TestFadeOut()
    {
        FadeOut();
    }

    [Button("페이드인 테스트")]
    private void TestFadeIn()
    {
        FadeIn();
    }
}
