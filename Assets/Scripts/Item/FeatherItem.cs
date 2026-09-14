using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 비둘기 사망 시 씬에 생성되는 깃털 아이템이다.
// 흡수 이동/스폰 팝인/자동 소멸은 BasePickupItem이 공통으로 처리하고,
// 여기서는 획득 시 PillowDurability에 깃털을 전달하는 역할만 담당한다.
public class FeatherItem : BasePickupItem
{
    [Title("드롭 연출")]
    [SerializeField, LabelText("떠오르는 높이")]
    private float floatHeight = 0.5f;
    // 드롭 직후 위로 살짝 떠오르는 거리다. BasePickupItem의 스폰 팝인과 함께 재생된다.

    [SerializeField, LabelText("떠오르는 시간")]
    private float floatDuration = 0.4f;

    private Tween _floatTween;

    protected override void Start()
    {
        base.Start();

        _floatTween = transform.DOMoveY(transform.position.y + floatHeight, floatDuration)
            .SetEase(Ease.OutQuad);
        // 자연스럽게 튀어오른 뒤 멈추는 느낌으로 Ease.OutQuad를 사용한다.
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _floatTween?.Kill();
    }

    protected override void ApplyEffect()
    {
        if (PillowDurability.Instance != null)
            PillowDurability.Instance.CollectFeather();
    }
}
