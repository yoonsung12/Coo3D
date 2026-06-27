using UnityEngine;
using Sirenix.OdinInspector;

// 횃불 도구를 처리한다.
// 사용 버튼을 누르는 동안만 빛이 켜지고, 얼음을 녹이며, 불을 붙일 수 있다.
public class TorchTool : BaseTool
{
    [Title("빛 설정")]
    [SerializeField, LabelText("횃불 라이트")]
    private Light torchLight;
    // Inspector에서 Player 자식의 TorchLight Point Light를 연결한다.

    [Title("불 붙이기 설정")]
    [SerializeField, LabelText("점화 범위")]
    private float igniteRange = 1.5f;
    // 사용 버튼을 눌렀을 때 불을 붙일 수 있는 전방 최대 거리다.

    [SerializeField, LabelText("점화 감지 레이어")]
    private LayerMask igniteLayer;
    // Inspector에서 불이 붙을 수 있는 오브젝트들의 레이어를 설정한다.

    [Title("얼음 녹이기 설정")]
    [SerializeField, LabelText("녹이기 반경")]
    private float meltRadius = 2f;
    // 사용 버튼을 누르는 동안 얼음 오브젝트를 녹일 수 있는 주변 반경이다.

    [SerializeField, LabelText("초당 열량")]
    private float heatPerSecond = 10f;
    // 값이 클수록 얼음이 더 빠르게 녹는다.

    [SerializeField, LabelText("녹이기 감지 레이어")]
    private LayerMask meltLayer;
    // Inspector에서 녹을 수 있는 얼음 오브젝트들의 레이어를 설정한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("사용 중 여부")]
    private bool _isUsing;

    private PlayerController _player;

    private void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();

        // 처음에는 꺼진 상태로 시작한다.
        SetLight(false);
    }

    // 도구를 장착할 때 호출된다. 라이트는 사용 버튼을 눌러야 켜진다.
    public override void OnEquip()
    {
        base.OnEquip();
        // base.OnEquip()에서 비주얼 활성화와 장착 사운드를 처리한다.
    }

    // 도구를 해제할 때 호출된다.
    public override void OnUnequip()
    {
        StopUsing();
        base.OnUnequip();
        // base.OnUnequip()에서 비주얼 사라짐 연출을 처리한다.
    }

    // 사용 버튼을 처음 눌렀을 때 한 번 호출된다. 라이트를 켜고 불을 붙인다.
    public override void OnUsePerformed()
    {
        _isUsing = true;
        SetLight(true);
        Ignite();
    }

    // 사용 버튼을 누르는 동안 매 프레임 호출된다. 얼음을 지속적으로 녹인다.
    public override void OnUseFrame()
    {
        MeltNearby();
    }

    // 사용 버튼을 뗐을 때 호출된다.
    public override void OnUseRelease()
    {
        StopUsing();
    }

    // 사용 중단 시 라이트를 끈다.
    public override void StopUsing()
    {
        _isUsing = false;
        SetLight(false);
    }

    private void SetLight(bool on)
    {
        if (torchLight != null)
            torchLight.enabled = on;
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

    [Button("점화 테스트")]
    private void TestIgnite() => Ignite();

    [Button("얼음 녹이기 테스트")]
    private void TestMelt() => MeltNearby();
}
