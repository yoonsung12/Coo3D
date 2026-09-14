using Sirenix.OdinInspector;
using UnityEngine;

// 씬에 미리 배치되는 구슬 조각 아이템이다.
// 흡수 이동/스폰 팝인/자동 소멸은 BasePickupItem이 공통으로 처리하고,
// 여기서는 획득 시 MarbleInventory에 조각 1개를 추가하는 역할만 담당한다.
public class MarbleFragment : BasePickupItem
{
    [Title("사운드")]
    [SerializeField, LabelText("획득 효과음")]
    private AudioClip pickupSound;
    // Inspector에서 획득 시 재생할 AudioClip을 연결한다. 비워두면 소리 없이 동작한다.

    protected override void ApplyEffect()
    {
        if (pickupSound != null)
            // 오브젝트가 Destroy된 뒤에도 소리가 끝까지 재생되도록 PlayClipAtPoint를 사용한다.
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        if (MarbleInventory.Instance != null)
            MarbleInventory.Instance.AddFragment();
    }
}
