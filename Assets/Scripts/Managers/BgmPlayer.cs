using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 전체에서 하나의 BGM을 반복 재생한다.
// 씬이 바뀌어도 음악이 처음부터 다시 시작하지 않도록 SaveManager와 같은 방식(DontDestroyOnLoad + 중복 제거)으로 하나만 살려 둔다.
// 보스전 씬처럼 BGM을 끄고 싶은 씬에 들어가면 서서히 꺼지고, 다른 씬으로 돌아오면 다시 서서히 켜진다.
[RequireComponent(typeof(AudioSource))]
public class BgmPlayer : MonoBehaviour
{
    private static BgmPlayer _instance;

    [Title("BGM 설정")]
    [SerializeField, LabelText("BGM 클립")]
    private AudioClip bgmClip;
    // Assets/Sound/BGM/BGM_Main.wav를 연결한다. 끝과 처음이 자연스럽게 이어지도록 미리 가공한 루프 파일이다.

    [SerializeField, LabelText("볼륨"), Range(0f, 1f)]
    private float volume = 0.5f;
    // 효과음보다 크게 들리면 낮춘다.

    [SerializeField, LabelText("BGM 끄는 씬 이름")]
    private List<string> silentScenes = new List<string> { "BossArena", "BossArena_Island" };
    // 이 씬에 들어가면 BGM이 서서히 꺼진다. 보스전은 나중에 전용 곡을 넣을 수 있게 비워 둔다.

    [Title("페이드 연출")]
    [SerializeField, LabelText("페이드인 시간")]
    private float fadeInDuration = 1.5f;

    [SerializeField, LabelText("페이드아웃 시간")]
    private float fadeOutDuration = 1f;

    [ReadOnly, ShowInInspector, LabelText("재생 중")]
    private bool IsPlaying => _source != null && _source.isPlaying;

    private AudioSource _source;
    private Tween _fadeTween;
    // 씬을 빠르게 오갈 때 페이드가 겹치지 않도록 현재 페이드를 저장해 두고 새로 시작하기 전에 끊는다.

    private void Awake()
    {
        // 다음 씬에 놓인 BgmPlayer는 이미 살아 있는 것이 있으면 스스로 사라져, 음악이 끊기거나 두 번 겹치지 않게 한다.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        _source.clip = bgmClip;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        // 2D 사운드로 재생해 카메라/플레이어 위치와 상관없이 항상 같은 크기로 들리게 한다.
        _source.volume = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (_instance != this) return;
        ApplyScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _fadeTween?.Kill();
        _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive로 겹쳐 여는 씬은 무시하고, 실제로 플레이 씬이 바뀌었을 때만 판단한다.
        if (mode != LoadSceneMode.Single) return;
        ApplyScene(scene.name);
    }

    // 지금 씬이 BGM을 꺼야 하는 씬인지 보고 켜거나 끈다.
    private void ApplyScene(string sceneName)
    {
        if (silentScenes.Contains(sceneName)) FadeOut();
        else FadeIn();
    }

    private void FadeIn()
    {
        if (bgmClip == null || _source == null) return;
        if (!_source.isPlaying) _source.Play();
        // 이미 재생 중이면 처음으로 돌리지 않고 이어서 들리게 한다.

        _fadeTween?.Kill();
        _fadeTween = _source.DOFade(volume, fadeInDuration).SetUpdate(true);
        // SetUpdate(true): 일시정지(timeScale 0) 중에 씬이 바뀌어도 페이드가 멈추지 않게 한다.
    }

    private void FadeOut()
    {
        if (_source == null) return;
        // 테스트 버튼은 Play Mode에서만 동작한다(Awake 전에는 AudioSource를 아직 못 잡았다).
        _fadeTween?.Kill();
        _fadeTween = _source.DOFade(0f, fadeOutDuration).SetUpdate(true).OnComplete(() => _source.Pause());
        // Stop 대신 Pause: 보스전에서 돌아왔을 때 끊긴 자리부터 이어서 재생된다.
    }

    [Button("재생 테스트")]
    private void TestPlay() => FadeIn();

    [Button("정지 테스트")]
    private void TestStop() => FadeOut();
}
