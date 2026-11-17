using Sirenix.OdinInspector;
using UnityEngine;

// 비둘기 날개 투사체(Wings)의 비행/명중 이펙트 컴포넌트다. BossProjectile과 같은 오브젝트에 붙인다.
// 투사체 로직(이동/충돌)은 건드리지 않고, BossProjectile.OnImpact 이벤트만 구독해 명중 이펙트를 낸다.
public class ProjectileEffect : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("비행 깃털 부스러기")]
    private ParticleSystem flightFeathers;
    // 날아가는 동안 뒤에 떨어지는 깃털 파티클이다(World 좌표, rateOverDistance). 명중 시 투사체에서 떼어 내 자연스럽게 사라지게 한다.

    [SerializeField, LabelText("비행 트레일")]
    private TrailRenderer flightTrail;
    // 날개 뒤로 남는 반투명 잔상 선이다. 명중 시 투사체에서 떼어 내 서서히 사라지게 한다.

    [SerializeField, LabelText("명중 이펙트 프리팹")]
    private ParticleSystem impactPrefab;
    // 명중 지점에 생성하는 "깃털 펑" 프리팹이다. 프리팹의 Stop Action이 Destroy라 스스로 정리된다.

    private BossProjectile _projectile;
    // OnImpact 이벤트를 구독할 대상이다.

    private void Awake()
    {
        _projectile = GetComponent<BossProjectile>();
        if (_projectile != null) _projectile.OnImpact += HandleImpact;
    }

    private void OnDestroy()
    {
        if (_projectile != null) _projectile.OnImpact -= HandleImpact;
    }

    private void HandleImpact(Vector3 position, bool hitTarget)
    {
        if (impactPrefab != null)
            Instantiate(impactPrefab, position, Quaternion.identity);

        // 투사체가 Destroy되면 자식 이펙트도 같이 뚝 끊기므로, 부모에서 떼어 내 남은 입자가 자연스럽게 사라지게 한다.
        DetachFeathers();
        DetachTrail();
    }

    private void DetachFeathers()
    {
        if (flightFeathers == null) return;
        flightFeathers.transform.SetParent(null, true);
        var main = flightFeathers.main;
        main.stopAction = ParticleSystemStopAction.Destroy; // 입자가 다 사라지면 스스로 삭제한다.
        flightFeathers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        flightFeathers = null;
    }

    private void DetachTrail()
    {
        if (flightTrail == null) return;
        flightTrail.transform.SetParent(null, true);
        flightTrail.emitting = false;
        flightTrail.autodestruct = true; // 잔상이 다 사라지면 스스로 삭제한다.
        flightTrail = null;
    }
}
