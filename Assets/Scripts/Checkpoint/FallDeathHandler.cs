using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어가 절벽/구멍(FallZone)에 떨어졌을 때의 처리를 담당한다.
// 낙사라고 바로 죽이지 않고, 체력을 소량만 깎은 뒤 근처 안전 지점으로 되돌리는 방식이다
// (할로우나이트/실크송의 가시밭 함정과 같은 방식).
// 되돌릴 때는 바로 순간이동하지 않고, 카메라는 빠진 자리를 비춘 채 화면을 어둡게 했다가
// 복귀 지점에서 다시 밝아지는 연출을 거친다.
[RequireComponent(typeof(PlayerHealth))]
public class FallDeathHandler : MonoBehaviour
{
    [Title("낙사 설정")]
    [SerializeField, LabelText("낙사 데미지")]
    private float fallDamage = 20f;
    // 절벽에 떨어졌을 때 깎이는 체력량이다. 최대 체력이 100이면 20은 "체력 1칸"에 해당하는 양으로 보면 된다.
    // 이 데미지로 체력이 0이 되면(이미 많이 다친 상태였던 경우) 근처로 돌아오지 않고,
    // PlayerHealth의 기존 사망 처리(게임오버)가 그대로 진행된다.

    [FoldoutGroup("낙사 연출")]
    [SerializeField, LabelText("사이드뷰 카메라")]
    private SideViewCamera sideViewCamera;
    // 빠진 자리를 계속 비추게 할 카메라다. 비워 두면 Main Camera에서 자동으로 찾는다.

    [FoldoutGroup("낙사 연출")]
    [SerializeField, LabelText("페이드 아웃 시간")]
    private float fadeOutDuration = 0.5f;

    [FoldoutGroup("낙사 연출")]
    [SerializeField, LabelText("페이드 인 시간")]
    private float fadeInDuration = 0.5f;

    [FoldoutGroup("낙사 연출")]
    [SerializeField, LabelText("검은 화면 최소 유지 시간")]
    private float minBlackHoldDuration = 0.3f;
    // 화면이 완전히 어두워진 뒤 최소한 이만큼은 기다렸다가 복귀시킨다.
    // FallZone에 빠질 때 소리가 있으면 그 소리가 끝날 때까지 더 기다린다.

    [FoldoutGroup("낙사 연출")]
    [SerializeField, LabelText("빠지는 속도")]
    private float sinkSpeed = 1.5f;
    // 빠진 뒤 화면이 어두워지는 동안 캐릭터가 아래로 내려가는 속도(초당 m)다.
    // 카메라는 따라가지 않으므로, 값이 클수록 캐릭터가 화면 아래로 빨리 사라진다.

    [ReadOnly, ShowInInspector, LabelText("낙사 연출 중")]
    private bool _isFalling;
    // 연출 중에 다른 FallZone에 또 닿아도 데미지/연출이 겹치지 않게 막는 플래그다.

    public bool IsFalling => _isFalling;
    // FallZone이 연출 중인지 확인해 빠질 때 소리를 중복 재생하지 않는 데 쓴다.

    private PlayerHealth _playerHealth;
    private PlayerController _playerController;
    private CharacterController _characterController;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        _playerController = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();

        if (sideViewCamera == null && Camera.main != null)
            sideViewCamera = Camera.main.GetComponent<SideViewCamera>();
    }

    // FallZone이 플레이어가 들어온 것을 감지했을 때 호출한다. recoverPosition은 복귀할 위치(체크포인트 또는 복귀 지점)이고,
    // extraHoldDuration은 빠질 때 소리 길이처럼 복귀 전에 최소한 기다려야 하는 시간이다.
    public void HandleFall(Vector3 recoverPosition, float extraHoldDuration = 0f)
    {
        if (_isFalling) return;

        _playerHealth.TakeDamage(fallDamage);

        // 체력이 0이 되어 이미 사망 처리(게임오버)가 시작됐다면 여기서 또 옮기면 위치가 꼬인다.
        if (_playerHealth.CurrentHealth <= 0f) return;

        // 페이더가 없는 예외 상황에서는 연출 없이 기존처럼 바로 옮긴다.
        if (ScreenFader.Instance == null)
        {
            _playerHealth.Respawn(recoverPosition);
            return;
        }

        StartCoroutine(FallRoutine(recoverPosition, extraHoldDuration));
    }

    // 빠진 자리 비추기 + 가라앉기 + 페이드 아웃 → 복귀 → 페이드 인 순서로 진행한다.
    // 매 프레임 CharacterController.Move()로 가라앉혀야 해서 Coroutine으로 작성했다.
    private IEnumerator FallRoutine(Vector3 recoverPosition, float extraHoldDuration)
    {
        _isFalling = true;
        _playerHealth.SetDamageImmune(true);
        // 화면이 어두운 동안 적/보스 공격을 맞지 않게 연출 내내 무적으로 둔다.

        // 입력으로 움직이거나 점프하지 않도록 조작을 끈다. PlayerController가 꺼지면 중력도 멈추므로
        // 가라앉는 움직임은 아래에서 직접 Move()로 준다.
        if (_playerController != null)
            _playerController.enabled = false;

        // 카메라를 빠진 자리에 붙잡아 둔다. 캐릭터가 가라앉아도 카메라는 수면/구덩이 위를 그대로 비춘다.
        if (sideViewCamera != null)
            sideViewCamera.StartFocus(transform.position, 0.01f, Ease.Linear);

        bool fadedOut = false;
        ScreenFader.Instance.FadeOut(() => fadedOut = true, fadeOutDuration);

        // 화면이 다 어두워지고 최소 유지 시간이 지났으며, 빠질 때 소리도 끝났을 때 복귀한다.
        float waitDuration = Mathf.Max(fadeOutDuration + minBlackHoldDuration, extraHoldDuration);
        float elapsed = 0f;
        while (elapsed < waitDuration || !fadedOut)
        {
            // Time.deltaTime을 곱해 프레임 수와 상관없이 초당 sinkSpeed만큼 내려가게 한다.
            if (_characterController != null && _characterController.enabled)
                _characterController.Move(Vector3.down * sinkSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 순간이동은 반드시 Respawn()을 거쳐야 CharacterController 처리와 시점(사이드뷰/탑다운) 갱신이 함께 된다.
        _playerHealth.Respawn(recoverPosition);

        // 붙잡아 둔 카메라를 풀고 복귀 지점으로 바로 옮긴다. 그러지 않으면 화면이 밝아질 때 카메라가 쓸려 오는 모습이 보인다.
        if (sideViewCamera != null)
        {
            bool focusReleased = false;
            sideViewCamera.EndFocus(0.01f, Ease.Linear, () => focusReleased = true);
            yield return new WaitUntil(() => focusReleased);
            sideViewCamera.SnapToTarget();
        }

        bool fadedIn = false;
        ScreenFader.Instance.FadeIn(() => fadedIn = true, fadeInDuration);
        yield return new WaitUntil(() => fadedIn);

        if (_playerController != null)
            _playerController.enabled = true;
        _playerHealth.SetDamageImmune(false);
        _isFalling = false;
    }

    [Button("낙사 테스트 (현재 위치로 복귀)")]
    private void TestFall()
    {
        HandleFall(transform.position);
    }
}
