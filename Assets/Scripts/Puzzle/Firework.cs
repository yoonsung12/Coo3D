using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 고드름 아래에 설치된 불꽃놀이 발사대다. 횃불(TorchTool)로 불을 붙이면 심지가 타는 연출 뒤
// 로켓이 고드름 밑동으로 날아가 부딪치고, 고드름이 떨어져 발사대를 깔아뭉갠다.
// TorchTool은 맞은 콜라이더의 GameObject에서 IIgnitable을 찾으므로, 이 스크립트는
// 점화용 트리거 콜라이더와 같은 오브젝트에 붙어 있어야 한다. 트리거라서 플레이어가 밟고 올라설 수 없다.
public class Firework : MonoBehaviour, IIgnitable
{
    [Title("연결")]
    [SerializeField, LabelText("맞출 고드름")]
    private FallingIcicle targetIcicle;

    [SerializeField, LabelText("로켓 (날아가는 자식)")]
    private Transform rocket;

    [SerializeField, LabelText("심지 흔들림 대상 (발사대 본체)")]
    private Transform fuseShakeTarget;
    // 심지가 타는 동안 흔들릴 발사대 모양 오브젝트다. 비우면 흔들림 없이 시간만 기다린다.

    [Title("연출 설정")]
    [SerializeField, LabelText("심지 타는 시간")]
    private float fuseDuration = 1.5f;
    // 불을 붙인 뒤 로켓이 발사되기까지의 지연이다.

    [SerializeField, LabelText("심지 흔들림 세기")]
    private float fuseShakeStrength = 0.05f;

    [SerializeField, LabelText("로켓 비행 시간")]
    private float rocketDuration = 0.6f;

    [SerializeField, LabelText("로켓 비행 Ease")]
    private Ease rocketEase = Ease.InQuad;

    [SerializeField, LabelText("깔려 부서지는 시간")]
    private float crushDuration = 0.2f;

    [SerializeField, LabelText("심지 파티클 (선택)")]
    private ParticleSystem fuseParticle;

    [SerializeField, LabelText("폭발 파티클 (선택)")]
    private ParticleSystem burstParticle;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("점화됨")]
    private bool _ignited;

    private Sequence _sequence;
    private Tween _crushTween;

    private void OnEnable()
    {
        if (targetIcicle != null)
            targetIcicle.Landed += OnIcicleLanded;
    }

    private void OnDisable()
    {
        if (targetIcicle != null)
            targetIcicle.Landed -= OnIcicleLanded;
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        _crushTween?.Kill();
    }

    // 횃불이 닿았을 때 한 번만 반응한다.
    public void OnIgnited()
    {
        if (_ignited || targetIcicle == null || rocket == null) return;
        _ignited = true;

        if (fuseParticle != null) fuseParticle.Play();

        // 로켓이 맞출 위치: 고드름 콜라이더의 맨 아래 중앙이다. 아직 매달려 있을 때의 위치를 쓴다.
        Physics.SyncTransforms();
        Collider icicleCollider = targetIcicle.GetComponent<Collider>();
        Bounds b = icicleCollider.bounds;
        Vector3 hitPoint = new Vector3(b.center.x, b.min.y, b.center.z);

        _sequence?.Kill();
        _sequence = DOTween.Sequence();

        // 1) 심지가 타는 동안 발사대를 떨게 한다.
        if (fuseShakeTarget != null)
            _sequence.Append(fuseShakeTarget.DOShakePosition(fuseDuration, fuseShakeStrength, 20));
        else
            _sequence.AppendInterval(fuseDuration);

        // 2) 로켓이 고드름 밑동까지 날아간다.
        _sequence.AppendCallback(() =>
        {
            if (fuseParticle != null) fuseParticle.Stop();
        });
        _sequence.Append(rocket.DOMove(hitPoint, rocketDuration).SetEase(rocketEase));

        // 3) 명중: 시퀀스가 끝나는 시점에 로켓을 숨기고 폭발 효과 후 고드름을 떨어뜨린다.
        _sequence.OnComplete(() =>
        {
            rocket.gameObject.SetActive(false);
            if (burstParticle != null)
            {
                burstParticle.transform.position = hitPoint;
                burstParticle.Play();
            }
            targetIcicle.OnHit();
        });
    }

    // 고드름이 착지하면 발사대가 그 아래에 깔려 부서진다.
    // 이후 고드름이 영구 발판이 되므로 발사대의 모양과 콜라이더를 모두 꺼서 겹침/오작동을 막는다.
    private void OnIcicleLanded()
    {
        _crushTween?.Kill();
        _crushTween = transform.DOScaleY(0f, crushDuration).OnComplete(() =>
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        });
    }

    [Button("점화 테스트")]
    private void TestIgnite()
    {
        if (!Application.isPlaying) return;
        OnIgnited();
    }
}
