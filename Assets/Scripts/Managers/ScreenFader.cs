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

    // Title.unity를 거치지 않고(예: SampleScene을 바로 열어 Play) 다른 씬에서 곧바로 Play해도
    // 페이드가 항상 동작하도록, 첫 씬이 로드되기 전에 Resources의 프리팹으로 인스턴스를 미리 만들어 둔다.
    // Title에 배치된 기존 ScreenFader는 그대로 두어도 되는데, Awake()의 중복 생성 방지 로직이
    // 이미 있어서 나중에 로드되면 스스로 파괴되고 이 인스턴스만 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null) return;

        GameObject prefab = Resources.Load<GameObject>("ScreenFader");
        if (prefab == null)
        {
            Debug.LogWarning("[ScreenFader] Resources/ScreenFader 프리팹을 찾을 수 없습니다.");
            return;
        }

        Instantiate(prefab);
    }

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
