using Sirenix.OdinInspector;
using UnityEngine;

// 절벽/구멍 아래 빈 공간에 배치하는 낙사 판정 트리거다.
// 들어온 오브젝트가 플레이어(FallDeathHandler)면 체력을 깎고 복귀 지점으로 되돌리고,
// 적(Enemy)이면 그대로 즉사시킨다.
[RequireComponent(typeof(Collider))]
public class FallZone : MonoBehaviour
{
    [Title("복귀 설정")]
    [SerializeField, LabelText("복귀 지점")]
    private Transform recoverPoint;
    // 플레이어가 떨어졌을 때 되돌아올 안전한 위치다. 절벽/구멍 바로 위 땅처럼,
    // 다시 떨어지지 않을 만한 위치에 빈 오브젝트를 놓고 연결한다.

    [SerializeField, LabelText("체크포인트가 있으면 체크포인트로 복귀")]
    private bool preferCheckpoint;
    // 맵이 길어서 하나의 FallZone이 넓은 구간을 덮는 씬(예: Summer)에서 켠다.
    // 이 씬에서 밟은 체크포인트가 있으면 그 위치로, 없으면 위의 복귀 지점으로 돌아간다.
    // 끄면 기존처럼 항상 복귀 지점으로 돌아간다.

    [FoldoutGroup("빠질 때 소리")]
    [SerializeField, LabelText("소리")]
    private AudioClip fallClip;
    // 플레이어가 이 구역에 빠지는 순간 재생할 소리다. (예: 물이면 첨벙 소리)
    // 비워 두면 소리 없이 기존처럼 동작한다.

    [FoldoutGroup("빠질 때 소리")]
    [SerializeField, LabelText("AudioSource")]
    private AudioSource fallAudioSource;
    // 같은 오브젝트에 AudioSource(Play On Awake 끔, Spatial Blend 0=2D)를 붙여 연결한다.
    // 소리를 듣는 쪽이 카메라라 3D로 두면 거리 때문에 작게 들리므로 2D로 둔다.

    [FoldoutGroup("빠질 때 소리")]
    [SerializeField, LabelText("볼륨"), Range(0f, 1f)]
    private float fallVolume = 1f;

    private void Awake()
    {
        // 플레이어/적이 그냥 통과해서 떨어져야 하므로 트리거로 강제한다.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var fallHandler = other.GetComponentInParent<FallDeathHandler>();
        if (fallHandler != null)
        {
            // 이미 낙사 연출 중이면(가라앉다가 다른 FallZone에 닿은 경우) 소리/복귀를 다시 처리하지 않는다.
            if (fallHandler.IsFalling) return;

            PlayFallSound();
            float soundDuration = GetFallSoundDuration();

            // 다른 씬에서 찍힌 체크포인트 좌표는 이 씬에서 의미가 없으므로 같은 씬일 때만 사용한다.
            bool useCheckpoint = preferCheckpoint
                && CheckpointManager.HasCheckpoint
                && CheckpointManager.CurrentCheckpointScene == gameObject.scene.name;
            if (useCheckpoint)
            {
                fallHandler.HandleFall(CheckpointManager.CurrentCheckpointPosition, soundDuration);
                return;
            }

            if (recoverPoint == null)
            {
                Debug.LogWarning($"[FallZone] '{name}'에 복귀 지점(recoverPoint)이 연결되지 않았습니다.", this);
                return;
            }

            fallHandler.HandleFall(recoverPoint.position, soundDuration);
            return;
        }

        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
            enemy.TakeDamage(float.MaxValue);
    }

    // AudioSource는 이 구역 오브젝트에 있으므로 플레이어가 복귀 지점으로 순간이동해도 소리는 끊기지 않고 끝까지 재생된다.
    private void PlayFallSound()
    {
        if (fallClip == null || fallAudioSource == null) return;
        fallAudioSource.PlayOneShot(fallClip, fallVolume);
    }

    // 낙사 연출이 이 시간만큼은 검은 화면을 유지하게 해서, 소리가 다 끝난 뒤 화면이 밝아지게 한다.
    private float GetFallSoundDuration()
    {
        if (fallClip == null || fallAudioSource == null) return 0f;
        return fallClip.length;
    }
}
