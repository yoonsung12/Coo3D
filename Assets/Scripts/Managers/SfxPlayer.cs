using UnityEngine;

// AudioSource.PlayClipAtPoint와 동일하게 동작하되, 효과음(SFX) 믹서 그룹으로 출력하는 헬퍼다.
// PlayClipAtPoint는 출력 그룹을 지정할 수 없어 볼륨 설정(SFX)이 적용되지 않기 때문에 대신 쓴다.
public static class SfxPlayer
{
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        // 유니티 PlayClipAtPoint 구현과 동일: 임시 오브젝트를 만들고 재생이 끝나면 지운다.
        GameObject go = new GameObject("One shot audio");
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.volume = volume;
        source.outputAudioMixerGroup = SoundSettings.MixerGroup(SoundCategory.SFX);
        source.Play();
        Object.Destroy(go, clip.length * (Time.timeScale < 0.01f ? 0.01f : Time.timeScale));
    }
}
