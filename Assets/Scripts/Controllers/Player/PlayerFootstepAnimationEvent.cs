using UnityEngine;

// 걷기 애니메이션의 Animation Event(OnFootstep)를 받아 부모 Player의 PlayerFootstep으로 넘겨준다.
// Animation Event는 Animator가 붙은 오브젝트(캐릭터 모델)의 스크립트만 호출할 수 있는데,
// PlayerFootstep은 CharacterController가 있는 부모 Player에 붙어 있어서 이 중계 스크립트가 필요하다.
// Animator가 있는 모델 오브젝트에 붙인다.
public class PlayerFootstepAnimationEvent : MonoBehaviour
{
    private PlayerFootstep _playerFootstep;

    private void Awake()
    {
        _playerFootstep = GetComponentInParent<PlayerFootstep>();

        if (_playerFootstep == null)
            Debug.LogError("PlayerFootstepAnimationEvent의 부모에 PlayerFootstep이 없습니다.");
    }

    // Player_Run.anim의 Animation Event가 이 이름으로 호출한다. 이름을 바꾸면 이벤트도 같이 바꿔야 한다.
    private void OnFootstep()
    {
        if (_playerFootstep != null)
            _playerFootstep.PlayFootstepFromAnimation();
    }
}
