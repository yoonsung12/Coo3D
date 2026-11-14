using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// SoundSettings가 게임 시작 시 저장된 볼륨을 믹서에 "첫 프레임 이후" 다시 적용하기 위한 보조 컴포넌트다.
// AudioMixer.SetFloat는 오디오 시스템이 준비되기 전(첫 프레임 이전)에 부르면 값이 무시된다.
// 실제로 저장값이 PlayerPrefs에는 있는데 시작 직후 믹서가 0dB(100%)로 남아 "설정이 저장 안 된 것처럼" 들렸다.
// SoundSettings가 시작할 때 이 컴포넌트를 숨긴 오브젝트로 하나 만들어 두고, 씬이 바뀔 때도 다음 프레임에 한 번 더 적용한다.
public class SoundSettingsApplier : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private IEnumerator Start()
    {
        // 첫 프레임이 끝난 뒤(오디오 준비 후) 적용한다.
        yield return null;
        SoundSettings.ApplySaved();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        SoundSettings.ApplySaved();
    }
}
