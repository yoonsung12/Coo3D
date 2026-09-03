using System;
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 여름 패턴의 레이저 빔이다. 보스가 상공에서 맵 중앙(x=0)을 향해 쏘는 레이저를
// 세로 밴드 형태로 표현한다 — 원형 폭발(BossLandingBlast)과 달리 하늘에서 지면까지 수직으로
// 훑는 빔이라 Y와 무관하게 X 거리만으로 판정한다(OverlapSphere 대신 OverlapBox 사용).
// 단색 박스 하나로는 레이저처럼 보이지 않는다는 피드백을 반영해, 얇고 밝은 코어(Core)와
// 그보다 넓은 파란 글로우(Glow) 두 겹을 겹쳐서 "하얀 심지 + 파란 후광" 느낌을 낸다.
// 경고가 끝나는 순간에는 단순히 색만 바뀌는 대신, Core/Glow가 위쪽(strikeTopLocalY)에 고정된
// 채로 아래로 빠르게 자라나 지면까지 내려찍히는 것처럼 보이게 한다("쿠콰광" 슬램 연출).
// BossSummerPattern이 Instantiate 후 Activate()로 경고→판정을 시작하고, 대포알이 보스에
// 명중하면 BossSummerPattern.InterruptLaser()가 Interrupt()를 호출해 중간에 멈출 수 있다.
public class BossLaserBeam : MonoBehaviour
{
    [Title("판정 설정")]
    [SerializeField, LabelText("판정 폭 (좌우 절반)")]
    private float bandHalfWidth = 2f;
    // 이 오브젝트의 X 좌표(맵 중앙) 기준 좌우 이 거리 안에 있으면 지속 피해를 받는다.

    [SerializeField, LabelText("초당 데미지")]
    private float damagePerSecond = 8f;

    [SerializeField, LabelText("데미지 판정 간격")]
    private float tickInterval = 0.5f;
    // 이 간격마다 한 번씩 데미지를 계산해서 적용한다(매 프레임 계산은 너무 잦아서 나눠서 처리한다).

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player가 속한 레이어(Default)를 지정한다.

    [Title("시각 연출 연결")]
    [SerializeField, LabelText("코어(얇고 밝은 심지)")]
    private MeshRenderer coreRenderer;
    // Inspector에서 자식의 얇은 Core 메시를 연결한다. 실제 판정 폭보다 좁게 만들어 "심지"처럼 보이게 한다.

    [SerializeField, LabelText("글로우(넓은 후광)")]
    private MeshRenderer glowRenderer;
    // Inspector에서 자식의 넓은 Glow 메시를 연결한다(판정 폭과 비슷하거나 살짝 넓게).

    [SerializeField, LabelText("착지 흙먼지 파티클")]
    private ParticleSystem dustBurst;
    // 경고가 끝나고 빔이 실제로 지면에 내려찍히는 순간(슬램 애니메이션 완료 시점) 1회 재생된다.
    // 비워두면 이펙트 없이 판정만 동작한다.

    [Title("사운드 연결")]
    [SerializeField, LabelText("오디오 소스")]
    private AudioSource audioSource;

    [SerializeField, LabelText("차징 소리")]
    private AudioClip chargingClip;
    // 경고(충전) 단계 시작과 동시에 재생되고, 슬램이 시작되면 즉시 멈춘다.

    [SerializeField, LabelText("발사 소리")]
    private AudioClip fireClip;
    // 슬램(내려찍기)이 시작되는 순간 1회 재생된다.

    [Title("경고 연출 설정")]
    [SerializeField, LabelText("경고 지속시간")]
    private float warningDuration = 1.5f;
    // 레이저가 실제 판정을 시작하기 전, 빨간색으로 깜빡이며 경고하는 시간이다. 값이 너무 짧으면
    // (과거 0.3초) 피할 틈도 없이 바로 데미지 판정이 시작된다는 피드백을 받아 충분히 늘렸다.

    [SerializeField, LabelText("경고 색상")]
    private Color warningColor = new Color(1f, 0.1f, 0.1f, 0.15f);
    // 알파를 낮게 잡아 거의 투명한 빨간색으로 보이게 한다. Core/Glow 머티리얼이 Transparent
    // Surface Type이어야 알파가 실제로 반영된다(BossLaserCore.mat/BossLaserGlow.mat 참고).

    [SerializeField, LabelText("경고 깜빡임 주기")]
    private float warningBlinkInterval = 0.15f;

    [Title("발사 색상 설정")]
    [SerializeField, LabelText("코어 색상 (밝은 심지)")]
    private Color coreColor = new Color(0.85f, 0.98f, 1f);

    [SerializeField, LabelText("글로우 색상 (파란 후광)")]
    private Color glowColor = new Color(0.15f, 0.6f, 1f);

    [Title("낙하(슬램) 연출 설정")]
    [SerializeField, LabelText("슬램 소요 시간")]
    private float strikeDuration = 0.15f;
    // 경고가 끝난 뒤 빔이 위에서 아래로 순식간에 자라나 내려찍히는 데 걸리는 시간이다.
    // 값이 클수록 천천히 떨어지는 것처럼 보인다.

    [SerializeField, LabelText("빔 상단 고정 높이 (로컬 Y)")]
    private float strikeTopLocalY = 20f;
    // Core/Glow의 윗변이 이 높이에 고정된 채로, 아랫변만 지면(0)까지 자라나며 내려온다.

    [SerializeField, LabelText("카메라 흔들림 세기")]
    private float cameraShakeStrength = 0.3f;

    [SerializeField, LabelText("카메라 흔들림 시간")]
    private float cameraShakeDuration = 0.2f;

    [SerializeField, LabelText("소멸 연출 시간")]
    private float dissolveDuration = 0.3f;

    private SideViewCamera _camera;
    private Tween _warningTween;
    private Tween _strikeTween;
    private Tween _dissolveTween;
    private bool _interrupted;

    // 판정이 끝났을 때(제한시간 만료든 중단이든) 호출된다. 인자는 "중단됐는지" 여부다.
    // BossSummerPattern이 이 이벤트로 결과를 받아 다음 단계를 결정한다.
    public event Action<bool> OnResolved;

    private void Awake()
    {
        // 슬램 순간 카메라 흔들림에 사용한다. 씬에 카메라가 하나뿐이라 미리 찾아둔다.
        _camera = FindFirstObjectByType<SideViewCamera>();
    }

    private void OnDestroy()
    {
        _warningTween?.Kill();
        _strikeTween?.Kill();
        _dissolveTween?.Kill();
    }

    // BossSummerPattern이 생성 직후 호출해 지속시간을 지정하고 경고→판정을 시작한다.
    public void Activate(float duration)
    {
        StartCoroutine(BeamRoutine(duration));
    }

    // 대포알이 보스에 명중했을 때 BossSummerPattern이 호출한다. 다음 프레임에 즉시 반영된다.
    public void Interrupt()
    {
        _interrupted = true;
    }

    private IEnumerator BeamRoutine(float duration)
    {
        // 경고 단계: 코어/글로우 둘 다 빨간색으로 깜빡인다. DOColor의 Yoyo 반복으로 밝기를
        // 오가게 해서 "위험하다"는 느낌을 준다. 위치/크기는 건드리지 않고(항상 전체 높이로 표시)
        // 색상만 바뀐다 — 실제로 내려찍히는 움직임은 경고가 끝난 뒤에만 재생된다.
        SetBeamColor(warningColor);
        if (coreRenderer != null)
        {
            _warningTween = coreRenderer.material
                .DOColor(warningColor * 0.5f, "_BaseColor", warningBlinkInterval)
                .SetLoops(-1, LoopType.Yoyo);
        }

        PlayChargingSound();

        yield return new WaitForSeconds(warningDuration);

        _warningTween?.Kill();
        StopChargingSound();
        yield return PlayStrikeDownRoutine();

        float elapsed = 0f;
        float tickTimer = 0f;

        while (elapsed < duration && !_interrupted)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;
                ApplyTickDamage();
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        bool wasInterrupted = _interrupted;
        OnResolved?.Invoke(wasInterrupted);
        PlayDissolveAndDestroy();
    }

    // 경고 색이 서서히 바뀌는 대신, 빨간색이 "뚝" 끊기자마자 파란 코어/글로우가 위쪽 고정 높이에서
    // 지면까지 순식간에 자라나며 내려찍힌다. 다 내려온 순간 흙먼지 파티클과 카메라 흔들림을 재생해
    // "쿠콰광" 하고 박히는 충격을 준다.
    private IEnumerator PlayStrikeDownRoutine()
    {
        SetBeamColor(coreColor, glowColor);
        PlayFireSound();

        float progress = 0f;
        bool done = false;

        _strikeTween = DOTween.To(() => progress, x => progress = x, 1f, strikeDuration)
            .SetEase(Ease.InQuad)
            .OnUpdate(() =>
            {
                ApplyStrikeHeight(coreRenderer, progress);
                ApplyStrikeHeight(glowRenderer, progress);
            })
            .OnComplete(() => done = true);

        yield return new WaitUntil(() => done);

        dustBurst?.Play();
        _camera?.Shake(cameraShakeDuration, cameraShakeStrength);
    }

    // renderer의 윗변을 strikeTopLocalY에 고정한 채, t(0~1)에 비례해 아랫변만 지면(로컬 Y=0)까지
    // 늘려서 "위에서 아래로 자라나며 내려찍히는" 모양을 만든다.
    private void ApplyStrikeHeight(MeshRenderer renderer, float t)
    {
        if (renderer == null) return;

        Transform tr = renderer.transform;
        float height = Mathf.Max(strikeTopLocalY * t, 0.05f); // 완전히 0이면 메시가 아예 안 보여서 최소값을 둔다.

        Vector3 scale = tr.localScale;
        scale.y = height;
        tr.localScale = scale;

        Vector3 pos = tr.localPosition;
        pos.y = strikeTopLocalY - height * 0.5f;
        tr.localPosition = pos;
    }

    // 경고(충전) 단계 시작과 동시에 차징 소리를 재생한다.
    private void PlayChargingSound()
    {
        if (audioSource == null || chargingClip == null) return;

        audioSource.clip = chargingClip;
        audioSource.loop = false;
        audioSource.Play();
    }

    // 슬램이 시작되면 차징 소리를 즉시 멈춰서 발사 소리와 겹치지 않게 한다.
    private void StopChargingSound()
    {
        if (audioSource == null) return;
        audioSource.Stop();
    }

    // 슬램(내려찍기)이 시작되는 순간 발사 소리를 1회 재생한다. PlayOneShot이라
    // 위 StopChargingSound()의 영향을 받지 않는다(그 이후에 호출되므로).
    private void PlayFireSound()
    {
        if (audioSource == null || fireClip == null) return;
        audioSource.PlayOneShot(fireClip);
    }

    // 코어/글로우 둘 다 같은 색으로 맞출 때 쓰는 편의 오버로드다(경고 단계용).
    private void SetBeamColor(Color color) => SetBeamColor(color, color);

    private void SetBeamColor(Color core, Color glow)
    {
        // URP Lit 머티리얼은 색상 프로퍼티 이름이 "_BaseColor"라, FlammableObject/BossLandingBlast와
        // 동일하게 SetColor에도 프로퍼티 이름을 명시해야 실제로 화면에 반영된다.
        if (coreRenderer != null) coreRenderer.material.SetColor("_BaseColor", core);
        if (glowRenderer != null) glowRenderer.material.SetColor("_BaseColor", glow);
    }

    // 빔의 X 좌표를 중심으로 좌우 bandHalfWidth, 상하로는 충분히 넓은 박스(50유닛)를 검사해서
    // 지면부터 상공까지 전부를 커버하는 세로 밴드 판정을 만든다. 시각적 메시 크기와 무관하게
    // 항상 이 고정된 범위로 판정한다.
    private void ApplyTickDamage()
    {
        Collider[] cols = Physics.OverlapBox(
            transform.position,
            new Vector3(bandHalfWidth, 50f, 50f),
            Quaternion.identity,
            hitLayers);

        float damage = damagePerSecond * tickInterval;

        foreach (Collider col in cols)
        {
            if (col.TryGetComponent<CharacterBase>(out var target))
                target.TakeDamage(damage);
        }
    }

    private void PlayDissolveAndDestroy()
    {
        _dissolveTween?.Kill();
        _dissolveTween = transform.DOScale(Vector3.zero, dissolveDuration).SetEase(Ease.InQuad);

        // 발사 소리가 아직 재생 중일 때 Destroy()로 오브젝트가 갑자기 사라지면 소리도 뚝 끊겨서
        // 어색하게 들린다. 사라지는 연출과 같은 시간 동안 볼륨을 서서히 낮춰서 자연스럽게 마무리한다.
        if (audioSource != null)
            audioSource.DOFade(0f, dissolveDuration);

        Destroy(gameObject, dissolveDuration);
    }

    [Button("강제 5초 테스트")]
    private void TestActivate()
    {
        if (Application.isPlaying) Activate(5f);
    }
}
