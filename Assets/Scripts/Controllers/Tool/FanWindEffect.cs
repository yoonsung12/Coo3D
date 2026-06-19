using UnityEngine;
using Sirenix.OdinInspector;

// 선풍기 바람의 시각 이펙트를 담당한다.
// ParticleSystem을 통해 일반 바람과 블라스트 연출을 재생한다.
public class FanWindEffect : MonoBehaviour
{
    [Title("파티클 연결")]
    [SerializeField, LabelText("일반 바람 파티클")]
    private ParticleSystem windParticles;
    // Inspector에서 바람이 부는 동안 재생할 ParticleSystem을 연결한다.

    [SerializeField, LabelText("블라스트 파티클")]
    private ParticleSystem blastParticles;
    // Inspector에서 블라스트 발동 시 한 번 터지는 ParticleSystem을 연결한다.

    [Title("블라스트 단계별 색상")]
    [SerializeField, LabelText("1단계 색상")]
    private Color blastColorLevel1 = Color.white;

    [SerializeField, LabelText("2단계 색상")]
    private Color blastColorLevel2 = Color.cyan;

    [SerializeField, LabelText("3단계 색상")]
    private Color blastColorLevel3 = new Color(0.2f, 0.5f, 1f);

    // 일반 바람 이펙트 재생을 시작한다.
    public void Play()
    {
        if (windParticles != null && !windParticles.isPlaying)
            windParticles.Play();
    }

    // 일반 바람 이펙트를 정지한다.
    public void Stop()
    {
        if (windParticles != null && windParticles.isPlaying)
            windParticles.Stop();
    }

    // 블라스트 발동 시 단발 버스트 이펙트를 재생한다.
    public void PlayBlastBurst(Vector3 windDirection, int level)
    {
        if (blastParticles == null) return;

        // 충전 단계에 따라 파티클 색상을 변경해 시각적으로 단계를 구분한다.
        var main = blastParticles.main;
        main.startColor = level switch
        {
            1 => blastColorLevel1,
            2 => blastColorLevel2,
            _ => blastColorLevel3
        };

        // 파티클이 바람 방향으로 발사되도록 회전을 맞춘다.
        blastParticles.transform.forward = windDirection;
        blastParticles.Play();
    }

    [Button("일반 바람 테스트")]
    private void TestPlay() => Play();

    [Button("바람 정지 테스트")]
    private void TestStop() => Stop();

    [Button("블라스트 3단계 테스트")]
    private void TestBlastBurst() => PlayBlastBurst(transform.forward, 3);
}
