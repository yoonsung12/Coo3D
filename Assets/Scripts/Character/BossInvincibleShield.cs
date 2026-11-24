using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 보스가 계절 패턴 중 무적(Boss.IsInvincible)일 때, 보스를 감싸는 반투명 보호막을 보여 주는 연출 전용 컴포넌트다.
// 무적이면 때려도 아무 반응이 없어서 "안 맞는 건지 무적인 건지" 알기 어려웠기 때문에, 눈으로 바로 보이게 한다.
// 판정에는 관여하지 않는다. 보호막 오브젝트에는 콜라이더를 넣지 않아 공격·이동을 막지 않는다.
public class BossInvincibleShield : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("Boss (비우면 같은 오브젝트)")]
    private Boss boss;
    // 무적 상태를 읽어 올 Boss다. 보통 이 컴포넌트를 Boss와 같은 오브젝트에 붙이므로 비워 두면 된다.

    [SerializeField, LabelText("보호막 비주얼")]
    private Transform shieldVisual;
    // 보스 루트의 자식으로 둔 반투명 구체(MeshRenderer만, 콜라이더 없음)를 연결한다.
    // 보스 몸 연출(기울기·쓰러짐)은 다른 자식이 담당하므로, 보호막은 루트 바로 아래에 둬서 그 연출과 섞이지 않게 한다.

    [Title("등장 / 사라짐 (DOTween)")]
    [SerializeField, LabelText("보호막 크기")]
    private Vector3 shieldScale = new Vector3(5.5f, 5.5f, 5.5f);
    // 완전히 나타났을 때의 localScale이다. 보스 몸(약 3.4 x 4.7)을 넉넉히 감싸도록 잡는다.

    [SerializeField, LabelText("나타나는 시간")]
    private float appearDuration = 0.35f;

    [SerializeField, LabelText("나타나는 Ease")]
    private Ease appearEase = Ease.OutBack;

    [SerializeField, LabelText("사라지는 시간")]
    private float disappearDuration = 0.3f;

    [SerializeField, LabelText("사라지는 Ease")]
    private Ease disappearEase = Ease.InBack;

    [Title("맥동 (DOTween)")]
    [SerializeField, LabelText("맥동 배율")]
    private float pulseScale = 1.05f;
    // 무적인 동안 보호막이 이 배율까지 커졌다 작아지기를 반복한다. 1이면 맥동하지 않는다.

    [SerializeField, LabelText("맥동 한 방향 시간")]
    private float pulseDuration = 0.8f;

    [Title("런타임 상태 (읽기 전용)")]
    [ShowInInspector, ReadOnly, LabelText("보호막 표시 중")]
    private bool _isShown;

    private Tween _scaleTween;
    // 등장·사라짐 Tween. 무적이 빠르게 켜졌다 꺼져도 겹치지 않게 저장해 두고 끊는다.
    private Tween _pulseTween;
    // 무적 동안 반복되는 맥동 Tween.

    private void Awake()
    {
        if (boss == null) boss = GetComponent<Boss>();

        // 시작할 때는 숨겨 둔다. 무적이 켜지는 순간 Update에서 나타난다.
        if (shieldVisual != null)
        {
            shieldVisual.localScale = Vector3.zero;
            shieldVisual.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void Update()
    {
        if (boss == null || shieldVisual == null) return;

        // 매 프레임 bool 하나만 비교하고, 상태가 바뀐 순간에만 Tween을 만든다.
        // Boss는 무적을 켜고 끄는 경로가 여러 곳(패턴 발동·파훼·중단·리셋)이라 이벤트 대신 상태를 직접 본다.
        bool invincible = boss.IsInvincible || _testForceShow;
        if (invincible == _isShown) return;

        if (invincible) Show();
        else Hide();
    }

    // 보호막을 키우며 나타나게 한 뒤 맥동을 시작한다.
    private void Show()
    {
        _isShown = true;
        KillTweens();

        shieldVisual.gameObject.SetActive(true);
        shieldVisual.localScale = Vector3.zero;
        _scaleTween = shieldVisual
            .DOScale(shieldScale, appearDuration)
            .SetEase(appearEase)
            .OnComplete(StartPulse);
    }

    // 보호막을 줄이며 사라지게 하고, 다 줄어들면 꺼 둔다.
    private void Hide()
    {
        _isShown = false;
        KillTweens();

        _scaleTween = shieldVisual
            .DOScale(Vector3.zero, disappearDuration)
            .SetEase(disappearEase)
            .OnComplete(() => shieldVisual.gameObject.SetActive(false));
    }

    private void StartPulse()
    {
        if (pulseScale <= 1f) return;

        _pulseTween = shieldVisual
            .DOScale(shieldScale * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void KillTweens()
    {
        _scaleTween?.Kill();
        _pulseTween?.Kill();
    }

    private bool _testForceShow;
    // 테스트 버튼용 플래그다. 켜 두면 무적이 아니어도 보호막을 보여 준다(Play Mode에서만 의미 있다).

    [Button("보호막 보이기/숨기기 테스트 (Play Mode)")]
    private void TestToggle()
    {
        if (!Application.isPlaying) return;
        _testForceShow = !_testForceShow;
    }
}
