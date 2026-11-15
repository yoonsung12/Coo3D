using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

// 선풍기 바람의 시각 이펙트를 담당한다.
// ParticleSystem을 통해 일반 바람, 차징, 블라스트 연출을 재생한다.
// 물리 힘·반동·판정은 FanTool이 처리하고, 이 클래스는 보이는 연출만 맡는다.
public class FanWindEffect : MonoBehaviour
{
    [Title("파티클 연결")]
    [SerializeField, LabelText("일반 바람 파티클")]
    private ParticleSystem windParticles;
    // Inspector에서 바람이 부는 동안 재생할 ParticleSystem을 연결한다. 자식 파티클(먼지 등)도 함께 재생된다.

    [SerializeField, LabelText("블라스트 파티클")]
    private ParticleSystem blastParticles;
    // Inspector에서 블라스트 발동 시 한 번 터지는 ParticleSystem을 연결한다. 자식(충격파 고리·섬광 등)도 함께 터진다.

    [SerializeField, LabelText("차징 빨려드는 바람 (선택)")]
    private ParticleSystem chargeSuctionParticles;
    // 차징하는 동안 선풍기 앞으로 빨려 들어오는 선을 뿌리는 ParticleSystem이다. 비워 두면 차징 연출 없이 동작한다.

    [SerializeField, LabelText("차징 고리 (선택)")]
    private ParticleSystem chargeRingParticles;
    // 차징하는 동안 선풍기 앞에 떠 있는 고리 ParticleSystem이다. 단계가 오를수록 커지고 색이 바뀐다.

    [Title("단계별 색상 (차징·블라스트 공통)")]
    [SerializeField, LabelText("1단계 색상")]
    private Color blastColorLevel1 = Color.white;

    [SerializeField, LabelText("2단계 색상")]
    private Color blastColorLevel2 = Color.cyan;

    [SerializeField, LabelText("3단계 색상")]
    private Color blastColorLevel3 = new Color(0.2f, 0.5f, 1f);

    [Title("차징 연출")]
    [SerializeField, LabelText("단계별 고리 크기")]
    private Vector3 chargeRingSizeByLevel = new Vector3(0.6f, 0.9f, 1.25f);
    // x = 1단계, y = 2단계, z = 3단계 고리 지름(m)이다. 클수록 고리가 크게 보인다.

    [SerializeField, LabelText("단계별 빨려드는 선 개수(초당)")]
    private Vector3 chargeSuctionRateByLevel = new Vector3(10f, 20f, 36f);
    // x/y/z = 1/2/3단계에서 1초에 생기는 선 개수다. 단계가 오를수록 많아져 힘이 모이는 느낌을 준다.

    [SerializeField, LabelText("단계 상승 펀치 세기")]
    private float chargePunchStrength = 0.35f;
    // 단계가 오르는 순간 고리가 커졌다 돌아오는 정도(배율)다. 0이면 펀치 없이 바로 바뀐다.

    [SerializeField, LabelText("단계 상승 펀치 시간")]
    private float chargePunchDuration = 0.25f;

    [Title("블라스트 연출")]
    [SerializeField, LabelText("단계별 크기 배율")]
    private Vector3 blastScaleByLevel = new Vector3(0.75f, 1f, 1.4f);
    // x/y/z = 1/2/3단계 블라스트 파티클 크기 배율이다. 클수록 줄기·고리·섬광이 크게 터진다.

    [SerializeField, LabelText("단계 색을 적용하지 않을 파티클")]
    private ParticleSystem[] keepOwnColorSystems;
    // 블라스트 자식 중 원래 색을 유지할 파티클(발밑 먼지 등)을 넣는다. 크기 배율은 그대로 적용된다.

    private Tween _chargePunchTween;
    // 단계 상승 펀치 연출이다. 연달아 단계가 오르면 이전 펀치를 끊고 새로 시작한다.

    private ParticleSystem[] _blastSystems;
    private ParticleSystem.MinMaxCurve[] _blastBaseSizes;
    // 블라스트 파티클(자식 포함)의 원래 크기다. 단계 배율을 매번 원래 크기 기준으로 곱하기 위해 저장해 둔다.

    private void Awake()
    {
        if (blastParticles != null)
        {
            _blastSystems = blastParticles.GetComponentsInChildren<ParticleSystem>(true);
            _blastBaseSizes = new ParticleSystem.MinMaxCurve[_blastSystems.Length];
            for (int i = 0; i < _blastSystems.Length; i++)
                _blastBaseSizes[i] = _blastSystems[i].main.startSize;
        }
    }

    private void OnDisable()
    {
        _chargePunchTween?.Kill();
    }

    private void OnDestroy()
    {
        _chargePunchTween?.Kill();
    }

    // 일반 바람 이펙트 재생을 시작한다.
    // isPlaying이 아니라 isEmitting으로 확인한다. Stop() 뒤에도 이미 나간 파티클이 남아 있는 동안은 isPlaying이 true라서,
    // 그 사이(약 0.5~0.7초)에 다시 쏘면 Play()가 건너뛰어져 바람이 안 나오는 문제가 있었다.
    public void Play()
    {
        if (windParticles != null && !windParticles.isEmitting)
            windParticles.Play();
    }

    // 일반 바람 이펙트를 정지한다. 이미 나간 바람 줄기는 끝까지 흘러가며 사라진다.
    public void Stop()
    {
        if (windParticles != null && windParticles.isEmitting)
            windParticles.Stop();
    }

    // 차징 단계가 바뀔 때마다 FanTool이 호출한다(1~3). 처음 호출되면 차징 연출을 시작한다.
    public void PlayCharge(int level)
    {
        Color color = GetLevelColor(level);
        float ringSize = GetByLevel(chargeRingSizeByLevel, level);
        float suctionRate = GetByLevel(chargeSuctionRateByLevel, level);

        if (chargeSuctionParticles != null)
        {
            var main = chargeSuctionParticles.main;
            main.startColor = color;
            var emission = chargeSuctionParticles.emission;
            emission.rateOverTime = suctionRate;
            if (!chargeSuctionParticles.isPlaying) chargeSuctionParticles.Play();
        }

        if (chargeRingParticles != null)
        {
            // 고리는 오래 사는 파티클 1개라서, 이미 떠 있는 파티클의 색·크기도 직접 바꿔야 단계가 바로 보인다.
            var main = chargeRingParticles.main;
            main.startColor = color;
            main.startSize = ringSize;
            if (!chargeRingParticles.isPlaying) chargeRingParticles.Play();
            ApplyToAliveParticles(chargeRingParticles, color, ringSize);

            // 단계가 오르는 순간 고리가 톡 커졌다 돌아오게 한다. 파티클 크기는 Hierarchy 스케일을 따르므로 Transform에 펀치를 준다.
            if (chargePunchStrength > 0f)
            {
                _chargePunchTween?.Kill();
                Transform ringTransform = chargeRingParticles.transform;
                ringTransform.localScale = Vector3.one;
                _chargePunchTween = ringTransform
                    .DOPunchScale(Vector3.one * chargePunchStrength, chargePunchDuration, 6, 0.5f)
                    .SetLink(gameObject);
            }
        }
    }

    // 차징이 끝나면(블라스트 발사·취소) FanTool이 호출한다. 고리는 바로 지우고, 빨려드는 선은 남은 것만 마저 사라지게 한다.
    public void StopCharge()
    {
        _chargePunchTween?.Kill();

        if (chargeSuctionParticles != null)
            chargeSuctionParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (chargeRingParticles != null)
        {
            chargeRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            chargeRingParticles.transform.localScale = Vector3.one;
        }
    }

    // 블라스트 발동 시 단발 버스트 이펙트를 재생한다.
    public void PlayBlastBurst(Vector3 windDirection, int level)
    {
        if (blastParticles == null) return;

        // 충전 단계에 따라 색과 크기를 바꿔 단계를 구분한다. 자식(충격파 고리·섬광 등)도 같은 규칙을 적용한다.
        Color color = GetLevelColor(level);
        float scale = GetByLevel(blastScaleByLevel, level);
        if (_blastSystems != null)
        {
            for (int i = 0; i < _blastSystems.Length; i++)
            {
                var main = _blastSystems[i].main;
                main.startSize = ScaleSize(_blastBaseSizes[i], scale);
                if (keepOwnColorSystems != null && System.Array.IndexOf(keepOwnColorSystems, _blastSystems[i]) >= 0)
                    continue;

                // 원래 색의 알파(투명도)는 유지하고 RGB만 단계 색으로 바꾼다. 섬광처럼 반투명하게 만든 파티클이 불투명해지지 않게 하기 위해서다.
                Color baseColor = main.startColor.color;
                main.startColor = new Color(color.r, color.g, color.b, baseColor.a);
            }
        }

        // 파티클이 바람 방향으로 발사되도록 회전을 맞춘다.
        if (windDirection.sqrMagnitude > 0.0001f)
            blastParticles.transform.forward = windDirection;
        blastParticles.Play(true);
    }

    private Color GetLevelColor(int level)
    {
        return level switch
        {
            1 => blastColorLevel1,
            2 => blastColorLevel2,
            _ => blastColorLevel3
        };
    }

    // 시작 크기에 배율을 곱한다. "두 값 사이 랜덤"으로 설정한 파티클은 최솟값·최댓값을 모두 곱해야 크기 범위가 같이 커진다.
    // (startSizeMultiplier는 이 모드에서 최댓값만 바꾸기 때문에 직접 계산한다)
    private static ParticleSystem.MinMaxCurve ScaleSize(ParticleSystem.MinMaxCurve baseSize, float scale)
    {
        if (baseSize.mode == ParticleSystemCurveMode.TwoConstants)
            return new ParticleSystem.MinMaxCurve(baseSize.constantMin * scale, baseSize.constantMax * scale);
        if (baseSize.mode == ParticleSystemCurveMode.Constant)
            return new ParticleSystem.MinMaxCurve(baseSize.constant * scale);

        // 커브 모드는 배율(curveMultiplier)만 바꾼다.
        baseSize.curveMultiplier *= scale;
        return baseSize;
    }

    private static float GetByLevel(Vector3 values, int level)
    {
        return level <= 1 ? values.x : level == 2 ? values.y : values.z;
    }

    // 이미 살아 있는 파티클의 색과 크기를 바꾼다. 오래 사는 고리 파티클에 단계 변화를 바로 반영하기 위해 쓴다.
    private static void ApplyToAliveParticles(ParticleSystem system, Color color, float size)
    {
        int count = system.particleCount;
        if (count == 0) return;

        var particles = new ParticleSystem.Particle[count];
        count = system.GetParticles(particles);
        for (int i = 0; i < count; i++)
        {
            particles[i].startColor = color;
            particles[i].startSize = size;
        }
        system.SetParticles(particles, count);
    }

    [Button("일반 바람 테스트")]
    private void TestPlay() => Play();

    [Button("바람 정지 테스트")]
    private void TestStop() => Stop();

    [Button("차징 단계 테스트")]
    private void TestCharge(int level = 3) => PlayCharge(level);

    [Button("차징 정지 테스트")]
    private void TestStopCharge() => StopCharge();

    [Button("블라스트 3단계 테스트")]
    private void TestBlastBurst() => PlayBlastBurst(transform.forward, 3);
}
