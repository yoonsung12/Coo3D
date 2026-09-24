using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

// 선풍기 도구를 처리한다.
// 일반 바람(Blowing)과 충전 후 블라스트(Charging→Blast) 두 가지 모드로 작동한다.
public class FanTool : BaseTool
{
    private enum FanState { Idle, Blowing, Charging }

    [Title("일반 바람 설정")]
    [SerializeField, LabelText("바람 힘")]
    private float windForce = 8f;
    // 값이 클수록 오브젝트를 더 강하게 민다.

    [SerializeField, LabelText("바람 사거리")]
    private float windRange = 5f;
    // 바람이 닿는 최대 거리다.

    [SerializeField, LabelText("바람 박스 크기 (너비/높이)")]
    private Vector2 windBoxSize = new Vector2(1f, 1.2f);
    // BoxCast의 XY 크기다. 너비가 클수록 더 넓은 범위에 바람이 닿는다.

    [SerializeField, LabelText("플레이어 반동 속도")]
    private float recoilForce = 2f;
    // 선풍기를 쏠 때 플레이어가 반대 방향으로 밀리는 힘이다.

    [Title("충전 블라스트 설정")]
    [SerializeField, LabelText("최대 충전 시간")]
    private float maxChargeTime = 2f;
    // 이 시간까지 충전하면 3단계 블라스트가 발동된다.

    [SerializeField, LabelText("충전 중 이동속도 배율")]
    private float chargeSlowMultiplier = 0.3f;
    // 충전 중 플레이어 이동속도를 줄여 신중하게 조준하도록 유도한다.

    [BoxGroup("블라스트 오브젝트 힘")]
    [SerializeField, LabelText("1단계")] private float blastForceLevel1 = 15f;
    [BoxGroup("블라스트 오브젝트 힘")]
    [SerializeField, LabelText("2단계")] private float blastForceLevel2 = 25f;
    [BoxGroup("블라스트 오브젝트 힘")]
    [SerializeField, LabelText("3단계")] private float blastForceLevel3 = 40f;

    [BoxGroup("차징 중 플레이어 반동")]
    [SerializeField, LabelText("1단계")] private float chargeRecoilLevel1 = 1.5f;
    [BoxGroup("차징 중 플레이어 반동")]
    [SerializeField, LabelText("2단계")] private float chargeRecoilLevel2 = 3f;
    [BoxGroup("차징 중 플레이어 반동")]
    [SerializeField, LabelText("3단계")] private float chargeRecoilLevel3 = 5f;
    // 차징 버튼을 누르고 있는 동안 매 프레임 플레이어를 뒤로 미는 속도다(2D 버전과 동일한 역할).
    // 단계가 오를수록 더 세게 밀려 "힘을 모으고 있다"는 느낌을 준다.

    [BoxGroup("블라스트 플레이어 밀림")]
    [SerializeField, LabelText("최소 밀림 속도")] private float minBlastRecoil = 10f;
    [BoxGroup("블라스트 플레이어 밀림")]
    [SerializeField, LabelText("최대 밀림 속도")] private float maxBlastRecoil = 40f;
    // 블라스트를 쏜 순간 플레이어가 뒤로 튕겨나가는 시작 속도다. 충전 진행도에 따라 최소~최대 사이로 정해진다.
    // PlayerController의 blastDecay(기본 4)로 지수 감속하므로 실제 밀리는 거리는 대략 "속도 ÷ blastDecay"다
    // (최대 40이면 약 10칸). 값이 클수록 멀리 밀린다.

    [Title("바람 사운드")]
    [SerializeField, LabelText("바람 소리 클립")]
    private AudioClip blowClip;
    // 일반 바람(차징 아님)을 쏘는 동안 반복 재생할 바람 소리다.

    [SerializeField, LabelText("바람 소리 볼륨"), Range(0f, 1f)]
    private float blowVolume = 1f;
    // 반복 바람 소리의 강도다.

    [SerializeField, LabelText("바람 소리 페이드아웃 시간"), Range(0.05f, 1f)]
    private float blowFadeOutDuration = 0.3f;
    // 바람을 멈출 때 소리가 서서히 사라지는 시간(초)이다. 값이 클수록 천천히 사라진다.

    [SerializeField, LabelText("바람 소리 전용 오디오 소스")]
    private AudioSource blowAudioSource;
    // Inspector에서 반복 바람 소리 전용 AudioSource(Loop, 2D 사운드)를 연결한다.
    // 효과음용 audioSource와 분리하는 이유: AudioSource.Stop()과 볼륨 페이드는 그 소스에서 재생 중인
    // PlayOneShot 소리까지 같이 끊거나 줄이므로, 같은 소스를 쓰면 차징 소리가 바람 소리 페이드에 묻혀버린다.

    [Title("차징 사운드")]
    [SerializeField, LabelText("차징 1단계 소리")]
    private AudioClip chargeSound1;
    // 차징 1단계에 들어서는 순간 한 번 재생된다.

    [SerializeField, LabelText("차징 2단계 소리")]
    private AudioClip chargeSound2;
    // 차징 2단계에 들어서는 순간 한 번 재생된다.

    [SerializeField, LabelText("차징 3단계 소리")]
    private AudioClip chargeSound3;
    // 차징 3단계(최대 충전)에 들어서는 순간 한 번 재생된다.

    [SerializeField, LabelText("차징 사운드 볼륨"), Range(0f, 1f)]
    private float chargeSoundVolume = 0.8f;
    // 차징 단계 소리의 강도다. 값이 클수록 크게 들린다.

    [Title("블라스트 사운드")]
    [SerializeField, LabelText("블라스트 1단계 바람 소리")]
    private AudioClip blastSound1;
    // 1단계까지 차징하고 버튼을 뗐을 때 재생된다.

    [SerializeField, LabelText("블라스트 2단계 바람 소리")]
    private AudioClip blastSound2;
    // 2단계까지 차징하고 버튼을 뗐을 때 재생된다.

    [SerializeField, LabelText("블라스트 3단계 바람 소리")]
    private AudioClip blastSound3;
    // 3단계까지 차징하고 버튼을 뗐을 때 재생된다.

    [SerializeField, LabelText("블라스트 사운드 볼륨"), Range(0f, 1f)]
    private float blastSoundVolume = 1f;
    // 블라스트 바람 소리의 강도다. 값이 클수록 크게 들린다.

    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;
    // Inspector에서 플레이어의 AudioSource(Spatial Blend 0 = 2D 사운드)를 연결한다. 비워두면 소리 없이 동작한다.
    // PlayClipAtPoint는 3D 위치 사운드라 카메라와 떨어진 거리만큼 작게 들리므로, 문/버튼처럼 PlayOneShot을 쓴다.

    [Title("레이어 설정")]
    [SerializeField, LabelText("바람 감지 레이어")]
    private LayerMask blowableLayer;
    // Inspector에서 바람에 반응할 오브젝트들의 레이어를 설정한다.

    [Title("연결 컴포넌트")]
    [SerializeField, LabelText("바람 이펙트")]
    private FanWindEffect windEffect;
    // Inspector에서 FanWindEffect 컴포넌트를 연결한다. 없으면 이펙트 없이 동작한다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 상태")]
    private FanState _state = FanState.Idle;

    [ReadOnly, ShowInInspector, LabelText("충전 진행도")]
    [ProgressBar(0, 1)]
    private float _chargeRatio;

    private float _chargeTime;
    private PlayerController _player;
    private int _lastChargeLevel;
    private Tween _blowFadeTween;
    // 바람 소리 페이드아웃이 중복 실행되지 않도록 현재 Tween을 저장한다.
    // 직전 프레임의 차징 단계다. 단계가 바뀌는 순간에만 차징 소리를 한 번 재생하기 위해 저장한다(0 = 차징 안 함).

    private void Start()
    {
        // Start()에서 탐색해야 모든 컴포넌트의 Awake()가 완료된 뒤 안전하게 참조할 수 있다.
        // FanTool은 플레이어의 자식이거나 같은 오브젝트에 붙어 있어야 한다.
        _player = GetComponentInParent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();
        Debug.Log($"[FanTool] Start() 호출됨. _player={_player}");
    }

    private void FixedUpdate()
    {
        // 일반 바람은 FixedUpdate에서 물리 힘을 적용한다.
        if (_state == FanState.Blowing)
            BlowWind();
    }

    // ToolManager에서 바람 버튼을 누르는 동안 Update마다 호출한다.
    public void OnBlowFrame()
    {
        if (_state == FanState.Charging) return;

        if (_state == FanState.Idle)
        {
            _state = FanState.Blowing;
            windEffect?.Play();
            PlayBlowSound();
        }
    }

    // ToolManager에서 충전 버튼(Shift+Attack)을 누르는 동안 Update마다 호출한다.
    public void OnChargeFrame()
    {
        if (_state != FanState.Charging)
        {
            _state = FanState.Charging;
            windEffect?.Stop();
            StopBlowSound();
            // 일반 바람을 쏘다가 차징으로 넘어가면 반복 바람 소리는 서서히 끄고 차징 소리로 넘어간다.
            _player?.SetSpeedMultiplier(chargeSlowMultiplier);
        }

        _chargeTime += Time.deltaTime;
        _chargeTime = Mathf.Min(_chargeTime, maxChargeTime);
        _chargeRatio = _chargeTime / maxChargeTime;

        // 단계가 바뀐 프레임에만 해당 단계 소리를 한 번 재생한다(매 프레임 재생하면 소리가 겹쳐 뭉개진다).
        int level = GetFanLevel();
        if (level != _lastChargeLevel)
        {
            _lastChargeLevel = level;
            PlaySound(level == 1 ? chargeSound1 : level == 2 ? chargeSound2 : chargeSound3, chargeSoundVolume);
        }

        // 차징하는 동안 단계별 세기로 뒤로 계속 밀어낸다. SetRecoil은 매 프레임 덮어쓰이므로
        // 단계가 바뀌는 순간 바로 세기가 달라지고, 버튼을 떼면 recoilDecay로 자연스럽게 멈춘다.
        _player?.SetRecoil(new Vector3(-GetFacingSignX() * GetChargeRecoilByLevel(), 0f, 0f));
    }

    // ToolManager에서 바람 버튼을 뗄 때 호출한다.
    public void OnBlowRelease()
    {
        if (_state == FanState.Charging)
            Blast();

        StopUsing();
    }

    public override void StopUsing()
    {
        _state = FanState.Idle;
        _chargeTime = 0f;
        _chargeRatio = 0f;
        _lastChargeLevel = 0;
        _player?.SetSpeedMultiplier(1f);
        windEffect?.Stop();
        StopBlowSound();
    }

    private void OnDisable()
    {
        // 도구가 비활성화되거나 씬이 바뀔 때 남은 페이드 Tween을 정리해 파괴된 AudioSource 접근 오류를 막는다.
        _blowFadeTween?.Kill();
    }

    private void BlowWind()
    {
        if (_player == null) return;

        Vector3 windDir = _player.FacingDirection;
        // 플레이어 중심에서 약간 앞쪽을 BoxCast 시작점으로 사용한다.
        Vector3 origin = _player.transform.position + Vector3.up * 0.5f;

        // 플레이어가 바라보는 방향으로 BoxCast를 해 IBlowable 오브젝트를 감지한다.
        // 바람 방향에 수직인 축의 회전을 계산해 BoxCast가 이동 방향을 정면으로 향하게 한다.
        Quaternion rotation = windDir != Vector3.zero ? Quaternion.LookRotation(windDir) : Quaternion.identity;
        Vector3 halfExtents = new Vector3(windBoxSize.x * 0.5f, windBoxSize.y * 0.5f, 0.1f);

        RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, windDir, rotation, windRange, blowableLayer);

        foreach (RaycastHit hit in hits)
        {
            IBlowable blowable = hit.collider.GetComponent<IBlowable>();
            blowable?.OnBlown(windDir, windForce, false);
        }

        // 선풍기를 쏠 때 플레이어는 반대 방향으로 약하게 밀린다.
        _player.SetRecoil(-windDir * recoilForce);
    }

    private void Blast()
    {
        if (_player == null) return;

        int level = GetFanLevel();
        Vector3 windDir = _player.FacingDirection;
        Vector3 origin = _player.transform.position + Vector3.up * 0.5f;

        // 블라스트 단계에 따라 힘과 반동을 결정한다.
        float blastForce = level switch
        {
            1 => blastForceLevel1,
            2 => blastForceLevel2,
            _ => blastForceLevel3
        };
        // 2D 버전과 같은 방식으로 충전 진행도(0~1)에 비례해 튕겨나가는 속도를 정한다.
        float recoilAmount = Mathf.Lerp(minBlastRecoil, maxBlastRecoil, _chargeTime / maxChargeTime);

        // OverlapBox로 전방의 오브젝트를 감지해 즉각적인 충격(Impulse)을 가한다.
        Quaternion rotation = windDir != Vector3.zero ? Quaternion.LookRotation(windDir) : Quaternion.identity;
        Vector3 blastCenter = origin + windDir * (windRange * 0.5f);
        Vector3 halfExtents = new Vector3(windBoxSize.x * 0.5f, windBoxSize.y * 0.5f, windRange * 0.5f);

        Collider[] cols = Physics.OverlapBox(blastCenter, halfExtents, rotation, blowableLayer);
        foreach (Collider col in cols)
        {
            IBlowable blowable = col.GetComponent<IBlowable>();
            blowable?.OnBlown(windDir, blastForce, true);
        }

        // 플레이어는 바라보는 방향의 반대로 강하게 튕겨난다. 바람 방향(windDir)은 마우스 조준이라
        // 위아래 성분이 섞여 있어 그대로 쓰면 조준 높이에 따라 밀리는 거리가 줄어들므로, 좌우 방향만 사용한다.
        _player.SetBlast(new Vector3(-GetFacingSignX() * recoilAmount, 0f, 0f));

        PlaySound(level == 1 ? blastSound1 : level == 2 ? blastSound2 : blastSound3, blastSoundVolume);

        windEffect?.PlayBlastBurst(windDir, level);
    }

    // 일반 바람을 쏘는 동안 반복 바람 소리를 재생한다. 페이드아웃 도중 다시 쏘면 볼륨을 즉시 복구한다.
    private void PlayBlowSound()
    {
        if (blowAudioSource == null || blowClip == null) return;

        _blowFadeTween?.Kill();
        blowAudioSource.volume = blowVolume;

        if (blowAudioSource.isPlaying) return;

        blowAudioSource.clip = blowClip;
        blowAudioSource.loop = true;
        blowAudioSource.Play();
    }

    // 반복 바람 소리를 DOTween으로 서서히 줄인 뒤 멈춘다. 뚝 끊기지 않게 하기 위함이다.
    private void StopBlowSound()
    {
        if (blowAudioSource == null || !blowAudioSource.isPlaying) return;
        if (_blowFadeTween != null && _blowFadeTween.IsActive()) return;
        // 이미 페이드 중이면 중복 실행하지 않는다.

        _blowFadeTween = blowAudioSource.DOFade(0f, blowFadeOutDuration)
            .OnComplete(() =>
            {
                blowAudioSource.Stop();
                blowAudioSource.volume = blowVolume;
                // 다음 재생을 위해 볼륨을 복구한다.
            });
    }

    // 짧은 1회성 효과음을 재생한다. PlayOneShot은 이전 소리를 끊지 않고 겹쳐 재생되므로
    // 차징 3단계 소리가 끝나기 전에 블라스트해도 두 소리가 자연스럽게 이어진다.
    private void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    // 차징 단계에 맞는 "차징 중 반동" 속도를 반환한다.
    private float GetChargeRecoilByLevel()
    {
        int level = GetFanLevel();
        if (level == 1) return chargeRecoilLevel1;
        if (level == 2) return chargeRecoilLevel2;
        return chargeRecoilLevel3;
    }

    // 플레이어 몸통이 바라보는 좌우 방향(+1 오른쪽, -1 왼쪽)이다. 몸통은 항상 ±90°로만 회전하므로
    // transform.forward.x의 부호가 곧 바라보는 방향이다.
    private float GetFacingSignX()
    {
        return _player.transform.forward.x >= 0f ? 1f : -1f;
    }

    private int GetFanLevel()
    {
        // 충전 진행도를 0.33/0.66 기준으로 1/2/3단계로 나눈다.
        float ratio = _chargeTime / maxChargeTime;
        if (ratio < 0.33f) return 1;
        if (ratio < 0.66f) return 2;
        return 3;
    }

    [Button("블라스트 3단계 테스트")]
    private void TestBlast()
    {
        _chargeTime = maxChargeTime;
        Blast();
        StopUsing();
    }
}
