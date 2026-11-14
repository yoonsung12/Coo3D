using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 먹으면 체력을 회복시켜 주는 하트 아이템이다. 비둘기가 죽을 때 ItemDropper로 떨어진다.
// 코인과 달리 플레이어에게 끌려가지 않고 제자리에서 빙글빙글 돌며, 플레이어가 가까이 와야 먹힌다.
// (프리팹에서 BasePickupItem의 "자석 흡수 사용"을 끄고 "획득 거리"를 넉넉하게 잡아 둔다.)
public class HealItem : BasePickupItem
{
    [Title("보상")]
    [SerializeField, LabelText("회복량 (하트 1개분)")]
    private float healAmount = 20f;
    // PlayerHealthUI의 하트 1개가 담당하는 체력(최대체력 100 / 하트 5개 = 20)과 맞추면 딱 1칸이 찬다.

    [Title("사운드")]
    [SerializeField, LabelText("먹을 때 소리")]
    private AudioClip pickupClip;
    // Assets/Sound/Item/Heal_Pickup.wav를 연결한다. 비워 두면 소리 없이 회복만 된다.

    [SerializeField, LabelText("소리 볼륨"), Range(0f, 1f)]
    private float pickupVolume = 1f;

    [Title("회전 연출")]
    [SerializeField, LabelText("한 바퀴 도는 시간")]
    private float spinDuration = 1.5f;
    // 값이 작을수록 더 빠르게 돈다.

    [Title("바닥에 내려앉기")]
    [SerializeField, LabelText("바닥 레이어")]
    private LayerMask groundMask = (1 << 0) | (1 << 9);
    // 하트가 내려앉을 바닥 레이어다. 기본은 Default + ThickIce. 적(Enemy) 레이어는 넣지 않는다.

    [SerializeField, LabelText("바닥 위 높이")]
    private float hoverHeight = 0.5f;
    // 바닥에서 이만큼 떠서 돈다. 플레이어 몸 높이 안에 들어오게 낮게 둔다.

    [SerializeField, LabelText("떨어지는 시간")]
    private float dropDuration = 0.3f;

    [SerializeField, LabelText("바닥 찾는 최대 거리")]
    private float maxDropDistance = 10f;
    // 이 거리 안에 바닥이 없으면(낭떠러지 위 등) 생긴 자리에 그대로 둔다.

    private Tween _spinTween;
    private Tween _dropTween;

    // base.Start()를 호출해야 스폰 팝인 연출과 자동 소멸 타이머가 동작한다(CoinItem과 같은 이유).
    protected override void Start()
    {
        base.Start();
        DropToGround();

        // DOTween으로 Y축 360도 회전을 무한 반복해 하트가 제자리에서 계속 돌게 한다.
        _spinTween = transform
            .DORotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _spinTween?.Kill();
        _dropTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    // 하트는 비둘기 몸 중심(도약 중이면 공중)에서 생기므로, 아래 바닥을 찾아 그 위로 톡 떨어뜨린다.
    // 하트에는 콜라이더/Rigidbody가 없어 물리와 부딪힐 일이 없으므로 DOTween으로 위치를 옮겨도 안전하다.
    private void DropToGround()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.2f, Vector3.down, maxDropDistance, groundMask, QueryTriggerInteraction.Ignore);

        float groundY = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            // 플레이어가 바로 밑에 있으면 플레이어 몸을 바닥으로 착각하지 않도록 건너뛴다.
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hit.point.y > groundY) groundY = hit.point.y;
        }
        if (float.IsNegativeInfinity(groundY)) return;

        float targetY = groundY + hoverHeight;
        if (Mathf.Abs(transform.position.y - targetY) < 0.05f) return;

        _dropTween = transform.DOMoveY(targetY, dropDuration).SetEase(Ease.OutBounce);
    }

    // 체력이 가득 차 있으면 먹지 않고 남겨 둔다. 나중에 다치고 돌아와서 먹을 수 있게 하기 위해서다.
    protected override bool CanCollect()
    {
        PlayerHealth player = PlayerHealth.Instance;
        return player != null && player.CurrentHealth < player.MaxHealth;
    }

    protected override void ApplyEffect()
    {
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(healAmount);

        // 실제로 먹힌 순간에만 호출되므로(체력이 가득하면 CanCollect가 막음) 여기서 소리를 낸다.
        // 하트 오브젝트는 바로 파괴되므로 SfxPlayer로 따로 재생해야 끝까지 들리고, 효과음 볼륨 설정도 적용된다.
        SfxPlayer.PlayAt(pickupClip, transform.position, pickupVolume);
    }
}
