using Sirenix.OdinInspector;
using UnityEngine;

// SeasonGaugeManager의 디버프 발동/해제 이벤트를 구독해 PlayerController의 이동을 제어한다.
// Player 오브젝트에 부착한다.
[RequireComponent(typeof(PlayerController))]
public class DebuffController : MonoBehaviour
{
    [Title("여름 디버프 설정")]
    [SerializeField, LabelText("이동 속도 배율")]
    private float slowMultiplier = 0.5f;
    // 여름(Slow) 디버프 발동 시 적용되는 이동 속도 배율이다. 값이 작을수록 더 느려진다.

    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        SeasonGaugeManager.OnDebuffTriggered += HandleDebuffTriggered;
    }

    private void OnDisable()
    {
        SeasonGaugeManager.OnDebuffTriggered -= HandleDebuffTriggered;
    }

    // 디버프가 바뀔 때마다 호출된다. 이전 디버프를 원래대로 되돌린 뒤 새 디버프를 적용한다.
    private void HandleDebuffTriggered(DebuffType newDebuff)
    {
        RemoveAllDebuffEffects();
        ApplyDebuff(newDebuff);
    }

    private void ApplyDebuff(DebuffType debuff)
    {
        switch (debuff)
        {
            case DebuffType.Bound: // 봄: 이동만 막기
                _playerController.SetBound(true);
                Debug.Log("[DebuffController] 디버프: 속박");
                break;

            case DebuffType.Slow: // 여름: 이동 속도를 배율만큼 낮춤
                _playerController.SetSpeedMultiplier(slowMultiplier);
                Debug.Log("[DebuffController] 디버프: 이속 저하");
                break;

            case DebuffType.Reverse: // 가을: 좌우 방향 반전
                _playerController.SetReverse(true);
                Debug.Log("[DebuffController] 디버프: 방향 반전");
                break;

            case DebuffType.Frozen: // 겨울: 모든 이동 정지
                _playerController.SetFrozen(true);
                Debug.Log("[DebuffController] 디버프: 빙결");
                break;
        }
    }

    // 이전 디버프 종류와 무관하게 모든 디버프 효과를 원래대로 되돌린다.
    // newDebuff로 바로 전환되는 경우에도 이전 효과가 남지 않도록 매번 전체를 초기화한다.
    private void RemoveAllDebuffEffects()
    {
        _playerController.SetBound(false);
        _playerController.SetSpeedMultiplier(1f);
        _playerController.SetReverse(false);
        _playerController.SetFrozen(false);
    }
}
