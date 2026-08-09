using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

// 횃불 도구를 처리한다.
// 처음 사용 버튼을 누르면 곧바로 켜지지 않고 부싯돌 타이밍(QTE)을 성공해야 불이 붙는다.
// 불이 붙으면 지속시간 동안 손을 떼도 계속 켜져 있고, 시간이 다 되면 자동으로 꺼진다.
// 도구를 집어넣으면(장착 해제) 남은 지속시간과 상관없이 즉시 꺼진다.
public class TorchTool : BaseTool
{
    // IgniteInteractPrompt 등 다른 스크립트가 GameObject.Find 없이 "지금 횃불이 켜져 있는지"를
    // 확인할 수 있도록 정적으로 노출한다. PauseMenuUI.IsPaused와 같은 패턴이다.
    public static bool IsTorchLit { get; private set; }

    // 횃불의 세 가지 상태를 나타낸다.
    private enum TorchState
    {
        Off,        // 꺼짐 — 사용 버튼을 누르면 점화 미니게임이 시작된다.
        Igniting,   // 점화 미니게임 진행 중 — 다시 누르면 그 순간 타이밍을 판정한다.
        Lit         // 켜짐 — 지속시간이 흐르는 동안 기존처럼 점화/녹이기가 가능하다.
    }

    [Title("점화 미니게임 연결")]
    [SerializeField, LabelText("부싯돌 타이밍 UI")]
    private TorchIgniteMinigameUI igniteMinigameUI;
    // Inspector에서 GameCanvas 아래의 TorchIgniteGaugePanel(TorchIgniteMinigameUI)을 연결한다.
    // 비워두면 미니게임 없이 즉시 점화된다(안전장치).

    [Title("지속시간 설정")]
    [SerializeField, LabelText("지속시간(초)")]
    private float burnDuration = 8f;
    // 점화에 성공한 뒤 횃불이 켜져 있는 시간이다. 횃불을 손에 들고 있는 동안에만 줄어들고,
    // 값이 0이 되면 자동으로 꺼진다. 도구를 집어넣으면 남은 값과 상관없이 즉시 꺼진다.

    [Title("빛 설정")]
    [SerializeField, LabelText("횃불 라이트")]
    private Light torchLight;
    // Inspector에서 Player 자식의 TorchLight Point Light를 연결한다.

    [SerializeField, LabelText("최대 밝기")]
    private float lightMaxIntensity = 1f;
    // 횃불이 완전히 켜졌을 때의 밝기다. 기존 TorchLight의 원래 Intensity 값을 그대로 사용한다.

    [SerializeField, LabelText("밝아지는 시간")]
    private float lightFadeInDuration = 0.15f;
    // 점화 성공 시 라이트가 0에서 최대 밝기까지 밝아지는 데 걸리는 시간이다.

    [SerializeField, LabelText("어두워지는 시간")]
    private float lightFadeOutDuration = 0.3f;
    // 소화 시 라이트가 서서히 어두워지는 데 걸리는 시간이다.

    [Title("불 붙이기 설정")]
    [SerializeField, LabelText("점화 범위")]
    private float igniteRange = 1.5f;
    // 켜진 상태에서 사용 버튼을 눌렀을 때 불을 붙일 수 있는 전방 최대 거리다.

    [SerializeField, LabelText("점화 감지 레이어")]
    private LayerMask igniteLayer;
    // Inspector에서 불이 붙을 수 있는 오브젝트들의 레이어를 설정한다.

    [Title("얼음 녹이기 설정")]
    [SerializeField, LabelText("녹이기 반경")]
    private float meltRadius = 2f;
    // 켜진 상태에서 사용 버튼을 누르는 동안 얼음 오브젝트를 녹일 수 있는 주변 반경이다.

    [SerializeField, LabelText("초당 열량")]
    private float heatPerSecond = 10f;
    // 값이 클수록 얼음이 더 빠르게 녹는다.

    [SerializeField, LabelText("녹이기 감지 레이어")]
    private LayerMask meltLayer;
    // Inspector에서 녹을 수 있는 얼음 오브젝트들의 레이어를 설정한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    private TorchState _state = TorchState.Off;

    [ReadOnly, ShowInInspector, LabelText("남은 지속시간")]
    private float _burnTimeRemaining;

    [ReadOnly, ShowInInspector, LabelText("장착 중 여부")]
    private bool _isEquipped;

    private PlayerController _player;
    private Tween _lightTween;

    private void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();

        // 처음에는 꺼진 상태로 시작한다. 씬을 다시 로드해도 static 값이 이전 상태로 남아있지 않도록 초기화한다.
        IsTorchLit = false;
        SetLight(false);
    }

    private void Update()
    {
        // 손에 들고 있고 불이 켜져 있을 때만 지속시간이 줄어든다.
        if (_state != TorchState.Lit || !_isEquipped) return;

        _burnTimeRemaining -= Time.deltaTime;
        if (_burnTimeRemaining <= 0f)
            ExtinguishTorch();
    }

    private void OnDestroy()
    {
        _lightTween?.Kill();
    }

    // 도구를 장착할 때 호출된다. 라이트는 미니게임에 성공해야 켜진다.
    public override void OnEquip()
    {
        base.OnEquip();
        // base.OnEquip()에서 비주얼 활성화와 장착 사운드를 처리한다.
        _isEquipped = true;
    }

    // 도구를 해제할 때 호출된다. 지속시간이 남아 있어도 즉시 꺼진다.
    public override void OnUnequip()
    {
        _isEquipped = false;
        StopUsing();
        base.OnUnequip();
        // base.OnUnequip()에서 비주얼 사라짐 연출을 처리한다.
    }

    // 사용 버튼을 처음 눌렀을 때 한 번 호출된다. 현재 상태에 따라 다르게 반응한다.
    public override void OnUsePerformed()
    {
        switch (_state)
        {
            case TorchState.Lit:
                // 이미 켜져 있으면 기존처럼 그 자리에서 바로 점화를 시도한다.
                Ignite();
                break;

            case TorchState.Igniting:
                // 미니게임 도중 다시 누른 순간의 타이밍을 판정한다.
                JudgeIgniteMinigame();
                break;

            case TorchState.Off:
                // 꺼진 상태에서 처음 누르면 점화 미니게임을 시작한다.
                BeginIgniteMinigame();
                break;
        }
    }

    // 사용 버튼을 누르는 동안 매 프레임 호출된다. 켜진 상태에서만 얼음을 녹인다.
    public override void OnUseFrame()
    {
        if (_state != TorchState.Lit) return;
        MeltNearby();
    }

    // 사용 중단(도구 해제 등) 시 미니게임을 취소하고 즉시 소화한다.
    public override void StopUsing()
    {
        CancelIgniteMinigame();
        ExtinguishTorch();
    }

    // 점화 미니게임을 시작한다.
    private void BeginIgniteMinigame()
    {
        if (igniteMinigameUI == null)
        {
            // 미니게임 UI가 연결되어 있지 않으면 안전하게 바로 점화한다.
            LightTorch();
            return;
        }

        _state = TorchState.Igniting;
        igniteMinigameUI.Begin();
    }

    // 진행 중인 미니게임의 타이밍을 판정한다. 성공하면 점화하고, 실패하면 꺼짐 상태로 되돌아가
    // 바로 다음 클릭으로 재시도할 수 있다.
    private void JudgeIgniteMinigame()
    {
        bool success = igniteMinigameUI != null && igniteMinigameUI.TryJudge();

        if (success)
            LightTorch();
        else
            _state = TorchState.Off;
    }

    // 진행 중인 미니게임을 결과 판정 없이 취소한다(도구 해제 등).
    private void CancelIgniteMinigame()
    {
        if (_state != TorchState.Igniting) return;

        igniteMinigameUI?.Cancel();
        _state = TorchState.Off;
    }

    // 점화에 성공했을 때 호출된다. 지속시간을 채우고 라이트를 켠다.
    private void LightTorch()
    {
        _state = TorchState.Lit;
        _burnTimeRemaining = burnDuration;
        IsTorchLit = true;
        SetLight(true);
    }

    // 지속시간 만료 또는 도구 해제로 횃불이 꺼질 때 호출된다.
    private void ExtinguishTorch()
    {
        if (_state == TorchState.Off) return;

        _state = TorchState.Off;
        _burnTimeRemaining = 0f;
        IsTorchLit = false;
        SetLight(false);
    }

    // 라이트를 DOTween으로 서서히 켜거나 끈다.
    private void SetLight(bool on)
    {
        if (torchLight == null) return;

        _lightTween?.Kill();

        if (on)
        {
            torchLight.enabled = true;
            torchLight.intensity = 0f;
            _lightTween = torchLight
                .DOIntensity(lightMaxIntensity, lightFadeInDuration)
                .SetEase(Ease.OutQuad);
        }
        else
        {
            _lightTween = torchLight
                .DOIntensity(0f, lightFadeOutDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() => torchLight.enabled = false);
        }
    }

    // 주변 IMeltable 오브젝트를 이번 프레임만큼 녹인다.
    private void MeltNearby()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, meltRadius, meltLayer);
        float heatThisFrame = heatPerSecond * Time.deltaTime;

        foreach (Collider col in cols)
        {
            IMeltable meltable = col.GetComponent<IMeltable>();
            meltable?.OnMelted(heatThisFrame);
        }
    }

    // 전방 일정 범위 내 IIgnitable 오브젝트에 불을 붙인다.
    private void Ignite()
    {
        if (_player == null) return;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = _player.FacingDirection;

        // 전방으로 SphereCast를 사용해 플레이어가 바라보는 방향의 오브젝트를 감지한다.
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.5f, dir, igniteRange, igniteLayer);

        foreach (RaycastHit hit in hits)
        {
            IIgnitable ignitable = hit.collider.GetComponent<IIgnitable>();
            ignitable?.OnIgnited();
        }

        Debug.Log($"[TorchTool] 점화 시도: 감지된 오브젝트 수 = {hits.Length}");
    }

    [Button("점화 미니게임 성공 테스트")]
    private void TestLightTorch() => LightTorch();

    [Button("소화 테스트")]
    private void TestExtinguish() => ExtinguishTorch();

    [Button("점화(불붙이기) 테스트")]
    private void TestIgnite() => Ignite();

    [Button("얼음 녹이기 테스트")]
    private void TestMelt() => MeltNearby();
}
