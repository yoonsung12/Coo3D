using Sirenix.OdinInspector;
using UnityEngine;

// 지하 상자에 놓여 있는 불꽃놀이 묶음이다. 플레이어가 다가가 줍으면 FireworkBag에 개수가 더해진다.
// 흡수 이동/스폰 팝인은 BasePickupItem이 처리한다. BasePickupItem은 시작할 때 스케일을 0에서 1로
// 키우므로, 이 오브젝트의 루트 스케일은 1이어야 하고 모양은 자식 오브젝트에 둬야 한다.
// 씬에 배치할 때는 부모 클래스의 "자동 소멸 시간"을 0으로 두어 사라지지 않게 한다.
public class FireworkItem : BasePickupItem
{
    [Title("불꽃놀이 설정")]
    [SerializeField, LabelText("지급 개수")]
    private int amount = 3;
    // 한 번 주울 때 가방에 들어가는 불꽃놀이 수다. 발사대가 3곳이라 기본 3개다.

    protected override void ApplyEffect()
    {
        if (FireworkBag.Instance != null)
            FireworkBag.Instance.Add(amount);
    }
}
