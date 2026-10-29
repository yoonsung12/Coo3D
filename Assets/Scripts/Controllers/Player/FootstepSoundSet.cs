using Sirenix.OdinInspector;
using UnityEngine;

// 바닥 종류별 발소리 클립과 "어떤 재질이 눈/풀인지" 목록을 담는 데이터 에셋이다.
// Player가 씬마다 따로 있어서, 클립을 바꿀 때 모든 씬을 고치지 않도록 이 에셋 하나에 모아 두고 각 씬의 PlayerFootstep이 참조한다.
[CreateAssetMenu(menuName = "Coo/Footstep Sound Set", fileName = "FootstepSoundSet")]
public class FootstepSoundSet : ScriptableObject
{
    [TabGroup("바닥별 발소리", "얼음")]
    [SerializeField, LabelText("얼음 발소리")]
    private AudioClip[] iceClips;
    // IcePlatform 발판을 밟을 때 무작위로 골라 재생한다.

    [TabGroup("바닥별 발소리", "눈")]
    [SerializeField, LabelText("눈 발소리")]
    private AudioClip[] snowClips;

    [TabGroup("바닥별 발소리", "풀·흙")]
    [SerializeField, LabelText("풀·흙 발소리")]
    private AudioClip[] grassClips;

    [FoldoutGroup("재질 판정")]
    [InfoBox("바닥 오브젝트마다 표시하지 않아도, 밟은 바닥의 재질이 아래 목록에 있으면 그 종류의 발소리가 난다.")]
    [SerializeField, LabelText("눈 재질")]
    private Material[] snowMaterials;

    [FoldoutGroup("재질 판정")]
    [SerializeField, LabelText("풀·흙 재질")]
    private Material[] grassMaterials;

    // 바닥 종류에 맞는 클립 목록을 돌려준다. Default이거나 비어 있으면 null을 돌려줘 기본 발소리를 쓰게 한다.
    public AudioClip[] GetClips(FootstepSurfaceType type)
    {
        AudioClip[] clips = type switch
        {
            FootstepSurfaceType.Ice => iceClips,
            FootstepSurfaceType.Snow => snowClips,
            FootstepSurfaceType.Grass => grassClips,
            _ => null
        };
        return clips != null && clips.Length > 0 ? clips : null;
    }

    // 재질이 눈/풀 목록에 있는지 보고 바닥 종류를 정한다. 어느 목록에도 없으면 Default다.
    public FootstepSurfaceType GetTypeFromMaterial(Material material)
    {
        if (material == null) return FootstepSurfaceType.Default;
        if (Contains(snowMaterials, material)) return FootstepSurfaceType.Snow;
        if (Contains(grassMaterials, material)) return FootstepSurfaceType.Grass;
        return FootstepSurfaceType.Default;
    }

    // 런타임에 renderer.material로 복제된 재질은 이름 끝에 " (Instance)"가 붙으므로 이름으로도 비교한다.
    private static bool Contains(Material[] list, Material material)
    {
        if (list == null) return false;
        string name = material.name.Replace(" (Instance)", "");
        foreach (Material m in list)
        {
            if (m == null) continue;
            if (m == material || m.name == name) return true;
        }
        return false;
    }
}
