using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// 볼륨 카테고리. 믹서 그룹/PlayerPrefs 키와 1:1로 대응한다.
public enum SoundCategory
{
    Master,
    BGM,
    SFX,
    UI
}

// 카테고리별 볼륨(0~1)을 저장하고 AudioMixer에 적용하는 정적 클래스다.
// 별도의 매니저 오브젝트 없이 어디서든 SoundSettings.Set(...) 으로 접근하기 위해 static으로 만들었다.
// 믹서는 Resources.Load를 쓰기 위해 Assets/Resources/Audio/MainMixer.mixer 에 둔다.
public static class SoundSettings
{
    private const string MixerResourcePath = "Audio/MainMixer";
    private static readonly string[] PrefKeys = { "Coo3D_Vol_Master", "Coo3D_Vol_BGM", "Coo3D_Vol_SFX", "Coo3D_Vol_UI" };
    private static readonly string[] ParamNames = { "MasterVolume", "BGMVolume", "SFXVolume", "UIVolume" };
    private static readonly string[] GroupPaths = { "Master", "Master/BGM", "Master/SFX", "Master/UI" };

    private static AudioMixer _mixer;
    private static readonly float[] _values = { 1f, 1f, 1f, 1f };
    private static readonly AudioMixerGroup[] _groups = new AudioMixerGroup[4];
    private static bool _loaded;

    // 씬이 로드된 직후 저장값을 믹서에 적용한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        EnsureLoaded();
        ApplyAll();

        // 첫 프레임 이전의 SetFloat는 믹서가 무시하므로(실제로 재현됨), 숨긴 보조 오브젝트가 첫 프레임 뒤와
        // 씬이 바뀐 다음 프레임에 저장값을 다시 적용한다. 하나만 만들어 씬을 넘어 유지한다.
        if (_applier == null)
        {
            GameObject go = new GameObject("SoundSettingsApplier");
            go.hideFlags = HideFlags.HideInHierarchy;
            _applier = go.AddComponent<SoundSettingsApplier>();
        }
    }

    private static SoundSettingsApplier _applier;

    // 저장된 값을 믹서에 다시 적용한다. SoundSettingsApplier가 오디오 준비 후 호출한다.
    public static void ApplySaved()
    {
        EnsureLoaded();
        ApplyAll();
    }

    // 믹서와 저장값을 한 번만 불러온다. (에디터 도메인 리로드 꺼짐 대비로 값은 매번 PlayerPrefs에서 읽는다)
    private static void EnsureLoaded()
    {
        if (_loaded && _mixer != null) return;

        _mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        for (int i = 0; i < PrefKeys.Length; i++)
        {
            _values[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeys[i], 1f));
            if (_mixer != null)
            {
                AudioMixerGroup[] found = _mixer.FindMatchingGroups(GroupPaths[i]);
                _groups[i] = found.Length > 0 ? System.Array.Find(found, g => g.name == GroupPaths[i].Substring(GroupPaths[i].LastIndexOf('/') + 1)) : null;
            }
        }
        _loaded = true;
    }

    public static float Get(SoundCategory category)
    {
        EnsureLoaded();
        return _values[(int)category];
    }

    // 값을 즉시 믹서에 반영하고 PlayerPrefs에 저장한다.
    public static void Set(SoundCategory category, float value)
    {
        EnsureLoaded();
        int i = (int)category;
        _values[i] = Mathf.Clamp01(value);
        ApplyOne(i);
        PlayerPrefs.SetFloat(PrefKeys[i], _values[i]);
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        for (int i = 0; i < _values.Length; i++)
            Set((SoundCategory)i, 1f);
    }

    // 소리를 낼 때 outputAudioMixerGroup에 넣을 그룹을 돌려준다. 믹서가 없으면 null(믹서를 거치지 않음).
    public static AudioMixerGroup MixerGroup(SoundCategory category)
    {
        EnsureLoaded();
        return _groups[(int)category];
    }

    private static void ApplyAll()
    {
        for (int i = 0; i < _values.Length; i++)
            ApplyOne(i);
    }

    private static void ApplyOne(int i)
    {
        if (_mixer == null) return;
        _mixer.SetFloat(ParamNames[i], ToDecibel(_values[i]));
    }

    // 사람 귀는 로그 스케일이라 슬라이더(선형)를 dB로 바꿔야 자연스럽게 들린다.
    private static float ToDecibel(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
}
