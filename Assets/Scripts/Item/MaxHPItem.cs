using Sirenix.OdinInspector;
using UnityEngine;

// 먹으면 하트(최대 체력)를 하나 늘려주는 아이템이다.
public class MaxHPItem : BasePickupItem
{
    [Title("보상")]
    [SerializeField, LabelText("늘어나는 최대 체력 (하트 1개분)")]
    private float maxHealthAmount = 20f;
    // PlayerHealthUI의 하트 1개가 담당하는 체력(최대체력 / 하트개수)과 값을 맞춰야
    // 이 아이템을 먹었을 때 하트가 딱 1개만 늘어난 것처럼 보인다.

    protected override void ApplyEffect()
    {
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.IncreaseMaxHealth(maxHealthAmount);

        if (PlayerHealthUI.Instance != null)
            PlayerHealthUI.Instance.AddHeart();
    }
}
