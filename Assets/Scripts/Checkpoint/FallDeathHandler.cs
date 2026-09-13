using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 절벽/구멍(FallZone)에 떨어졌을 때의 처리를 담당한다.
// 낙사라고 바로 죽이지 않고, 체력을 소량만 깎은 뒤 근처 안전 지점으로 되돌리는 방식이다
// (할로우나이트/실크송의 가시밭 함정과 같은 방식).
[RequireComponent(typeof(PlayerHealth))]
public class FallDeathHandler : MonoBehaviour
{
    [Title("낙사 설정")]
    [SerializeField, LabelText("낙사 데미지")]
    private float fallDamage = 20f;
    // 절벽에 떨어졌을 때 깎이는 체력량이다. 최대 체력이 100이면 20은 "체력 1칸"에 해당하는 양으로 보면 된다.
    // 이 데미지로 체력이 0이 되면(이미 많이 다친 상태였던 경우) 근처로 돌아오지 않고,
    // PlayerHealth의 기존 사망 처리(체크포인트 리스폰)가 그대로 진행된다.

    private PlayerHealth _playerHealth;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
    }

    // FallZone이 플레이어가 들어온 것을 감지했을 때 호출한다. recoverPosition은 그 FallZone에 지정된 복귀 지점이다.
    public void HandleFall(Vector3 recoverPosition)
    {
        _playerHealth.TakeDamage(fallDamage);

        // 데미지로 죽지 않고 살아있을 때만 복귀 지점으로 옮긴다.
        // 체력이 0이 되어 이미 사망 처리(체크포인트 리스폰)가 시작됐다면 여기서 또 옮기면 위치가 꼬인다.
        if (_playerHealth.CurrentHealth > 0f)
            _playerHealth.Respawn(recoverPosition);
    }

    [Button("낙사 테스트 (현재 위치로 복귀)")]
    private void TestFall()
    {
        HandleFall(transform.position);
    }
}
