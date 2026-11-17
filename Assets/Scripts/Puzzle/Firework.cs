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

    [Title("사운드")]
    [SerializeField, LabelText("심지 치이익")]
    private AudioClip fuseClip;
    // 심지가 타는 동안 반복 재생된다. 심지 시간이 클립 길이와 달라도 끊기지 않게 loop로 돌린다.

    [SerializeField, LabelText("로켓 비행")]
    private AudioClip flyClip;

    [SerializeField, LabelText("얼음 명중")]
    private AudioClip hitIceClip;

    [SerializeField, LabelText("고드름 박힘")]
    private AudioClip icicleStabClip;

    [SerializeField, Range(0f, 1f), LabelText("효과음 볼륨")]
    private float sfxVolume = 1f;

    [SerializeField, LabelText("심지 소리 AudioSource (비우면 자동 생성)")]
    private AudioSource fuseSource;
    // 심지 소리는 중간에 멈춰야 하므로 원샷(SfxPlayer) 대신 전용 AudioSource를 쓴다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("점화됨")]
    private bool _ignited;

    private Sequence _sequence;
    private Tween _crushTween;

    private void Awake()
    {
        // 사운드 설정(SFX) 볼륨이 적용되도록 믹서 그룹을 지정한 심지 전용 소스를 준비한다.
        if (fuseSource == null) fuseSource = gameObject.AddComponent<AudioSource>();
        fuseSource.playOnAwake = false;
        fuseSource.loop = true;
        fuseSource.spatialBlend = 1f;
        fuseSource.outputAudioMixerGroup = SoundSettings.MixerGroup(SoundCategory.SFX);
    }

    private void OnEnable()
    {
        if (targetIcicle != null)
            targetIcicle.Landed += OnIcicleLanded;
    }

    private void OnDisable()
    {
        if (targetIcicle != null)
            targetIcicle.Landed -= OnIcicleLanded;
        StopFuseSound();
    }

    private void OnDestroy()
    {
        StopFuseSound();
        _sequence?.Kill();
        _crushTween?.Kill();
    }

    // 횃불이 닿았을 때 한 번만 반응한다.
    public void OnIgnited()
    {
        if (_ignited || targetIcicle == null || rocket == null) return;
        _ignited = true;

        if (fuseParticle != null) fuseParticle.Play();
        PlayFuseSound();

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
            StopFuseSound();
            SfxPlayer.PlayAt(flyClip, rocket.position, sfxVolume);
        });
        _sequence.Append(rocket.DOMove(hitPoint, rocketDuration).SetEase(rocketEase));

        // 3) 명중: 시퀀스가 끝나는 시점에 로켓을 숨기고 폭발 효과 후 고드름을 떨어뜨린다.
        _sequence.OnComplete(() =>
        {
            rocket.gameObject.SetActive(false);
            SfxPlayer.PlayAt(hitIceClip, hitPoint, sfxVolume);
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
        // 다른 고드름에서는 나지 않도록, 이 퍼즐 고드름의 Landed 이벤트(이미 구독 중)에서만 박히는 소리를 낸다.
        SfxPlayer.PlayAt(icicleStabClip, targetIcicle.transform.position, sfxVolume);

        _crushTween?.Kill();
        _crushTween = transform.DOScaleY(0f, crushDuration).OnComplete(() =>
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        });
    }

    private void PlayFuseSound()
    {
        if (fuseClip == null || fuseSource == null) return;
        fuseSource.clip = fuseClip;
        fuseSource.volume = sfxVolume;
        fuseSource.Play();
    }

    private void StopFuseSound()
    {
        if (fuseSource != null && fuseSource.isPlaying) fuseSource.Stop();
    }

    [Button("사운드 테스트 (심지)")]
    private void TestFuseSound()
    {
        if (!Application.isPlaying) return;
        PlayFuseSound();
    }

    [Button("사운드 테스트 (비행)")]
    private void TestFlySound()
    {
        if (!Application.isPlaying) return;
        SfxPlayer.PlayAt(flyClip, transform.position, sfxVolume);
    }

    [Button("사운드 테스트 (얼음 명중)")]
    private void TestHitIceSound()
    {
        if (!Application.isPlaying) return;
        SfxPlayer.PlayAt(hitIceClip, transform.position, sfxVolume);
    }

    [Button("사운드 테스트 (고드름 박힘)")]
    private void TestIcicleStabSound()
    {
        if (!Application.isPlaying) return;
        SfxPlayer.PlayAt(icicleStabClip, transform.position, sfxVolume);
    }

    [Button("점화 테스트")]
    private void TestIgnite()
    {
        if (!Application.isPlaying) return;
        OnIgnited();
    }
}
