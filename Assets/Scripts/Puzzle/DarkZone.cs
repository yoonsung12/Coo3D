using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

// 플레이어가 이 구역 안에 있는 동안 하늘 조명(Directional Light)과 환경광을 어둡게 만들어,
// 횃불(TorchTool의 Point Light)을 켜야 주변이 잘 보이게 하는 트리거다.
// 구역을 벗어나면 원래 밝기로 서서히 돌아온다. ViewModeZone과 같은 방식으로 PlayerController가
// 들어오고 나가는 것만 감지하며, 횃불 쪽 코드는 건드리지 않는다.
[RequireComponent(typeof(Collider))]
public class DarkZone : MonoBehaviour
{
    [InfoBox("BoxCollider의 Is Trigger를 켜고, 어둡게 만들 공간 전체를 덮도록 크기를 맞춘다.\n" +
             "하늘 조명은 씬 전체에 영향을 주므로 구역 안에 있는 동안엔 화면에 보이는 구역 밖도 함께 어두워진다.\n" +
             "BossAutumnPattern도 같은 하늘 조명을 조절하므로 보스맵에는 배치하지 않는다.")]
    [Title("조명 연결")]
    [SerializeField, LabelText("하늘 조명(Directional Light)")]
    private Light skyLight;
    // Inspector에서 씬의 Directional Light를 연결한다. 비워두면 환경광만 어두워진다.

    [Title("어둡기 설정")]
    [SerializeField, LabelText("어두울 때 하늘 조명 밝기")]
    private float darkLightIntensity = 0.1f;
    // 구역 안에서의 Directional Light 밝기다. 0에 가까울수록 횃불 없이는 거의 보이지 않는다.

    [SerializeField, LabelText("어두울 때 환경광 비율"), Range(0f, 1f)]
    private float darkAmbientScale = 0.1f;
    // 원래 환경광(그림자 진 면까지 은은하게 비추는 빛)·하늘 배경 밝기·반사광에 함께 곱할 비율이다.
    // 1이면 그대로, 0이면 완전히 사라져 빛이 닿지 않는 면과 하늘이 새까맣게 보인다.
    // 하늘 배경(Skybox)을 같이 어둡게 하지 않으면 주변 물체만 어둡고 하늘은 밝아서 어둑한 느낌이 나지 않는다.

    [Title("플레이어 밝기 설정")]
    [InfoBox("플레이어 몸(char1)은 조명 대신 자체발광(Emission)으로 그려져서 조명을 어둡게 해도 밝게 남고,\n" +
             "횃불 빛도 받지 못한다. 그래서 구역 안에서는 발광 세기를 직접 줄이고, 횃불을 켜면 다시 올려준다.")]
    [SerializeField, LabelText("플레이어 몸 렌더러")]
    private Renderer playerBodyRenderer;
    // Inspector에서 Player 자식의 char1(SkinnedMeshRenderer)을 연결한다. 비워두면 플레이어 밝기는 건드리지 않는다.

    [SerializeField, LabelText("어두울 때 플레이어 밝기"), Range(0f, 1f)]
    private float darkPlayerBrightness = 0.3f;
    // 횃불 없이 구역 안에 있을 때 원래 발광 세기에 곱할 비율이다. 낮을수록 플레이어가 어둠에 묻힌다.

    [SerializeField, LabelText("횃불 켰을 때 플레이어 밝기"), Range(0f, 1f)]
    private float torchPlayerBrightness = 0.8f;
    // 구역 안에서 횃불이 켜져 있을 때의 비율이다. 1이면 구역 밖과 똑같이 밝다.

    [SerializeField, LabelText("플레이어 밝기 변화 시간")]
    private float playerFadeDuration = 0.3f;
    // 횃불을 켜고 끌 때 플레이어 밝기가 바뀌는 데 걸리는 시간이다. 횃불 라이트의 켜짐/꺼짐 속도와 비슷하게 맞춘다.

    [Title("전환 연출 (DOTween)")]
    [SerializeField, LabelText("어두워지는 시간")]
    private float darkenDuration = 0.8f;

    [SerializeField, LabelText("밝아지는 시간")]
    private float brightenDuration = 0.6f;

    [SerializeField, LabelText("전환 Ease")]
    private Ease fadeEase = Ease.InOutSine;
    // 들어가고 나갈 때 밝기가 변하는 느낌이다. InOutSine은 시작과 끝이 부드럽다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("어두운 상태")]
    private bool _isDark;

    [ReadOnly, ShowInInspector, LabelText("현재 환경광 비율")]
    private float _currentAmbientScale = 1f;

    [ReadOnly, ShowInInspector, LabelText("현재 플레이어 밝기")]
    private float _playerBrightness = 1f;

    private Collider _zoneCollider;
    private Collider _playerCollider;
    // 발끝(transform.position)은 바닥 높이와 거의 같아 구역 아래 경계에 걸리기 쉬우므로,
    // 플레이어 콜라이더의 중심(허리 높이)으로 구역 안에 있는지 판정한다.
    private bool _playerInTrigger;
    // OnTriggerEnter~OnTriggerExit 사이인지 여부다. 이 동안만 Update에서 범위 확인을 한다.

    private MaterialPropertyBlock _playerPropertyBlock;
    private Color _originalEmissionColor;
    private float _playerTargetBrightness = 1f;
    private Tween _playerTween;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    // 머티리얼 에셋을 직접 바꾸지 않고 이 렌더러에만 발광 색을 덮어쓰기 위해 MaterialPropertyBlock을 쓴다.
    // 그래서 구역 밖이나 다른 씬의 플레이어 모습에는 영향이 없다.
    private float _originalLightIntensity;
    private SphericalHarmonicsL2 _originalAmbientProbe;
    // 환경광은 RenderSettings.ambientProbe(구면 조화 함수 형태의 주변광 데이터)에 담겨 있다.
    // 씬 시작 시 원본을 저장해두고, 여기에 비율을 곱해 넣는 방식으로 어둡게/밝게 만든다.
    // ambientIntensity 값을 바꾸는 방식은 Skybox 환경광 모드에서 실행 중에 바로 반영되지 않기 때문이다.

    private float _originalReflectionIntensity;
    // 매끈한 바닥이 밝은 하늘을 반사해 혼자 환하게 보이지 않도록 반사광 세기도 함께 줄인다.

    private Material _originalSkybox;
    private Material _runtimeSkybox;
    private float _originalSkyboxExposure;
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    // 하늘 배경은 Skybox 머티리얼의 _Exposure(노출) 값으로 어둡게 만든다.
    // 원본 머티리얼 에셋을 직접 바꾸면 Play Mode를 끝내도 값이 남으므로, 실행 중에만 쓰는 복사본을 만들어 바꾼다.

    private Sequence _fadeSequence;
    // 들락날락할 때 이전 전환 연출을 끊고 새로 시작할 수 있도록 저장해둔다.

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider>();
        _zoneCollider.isTrigger = true;
        // 플레이어가 그냥 통과해야 하므로 트리거로 강제한다.

        if (skyLight != null) _originalLightIntensity = skyLight.intensity;
        _originalAmbientProbe = RenderSettings.ambientProbe;
        _originalReflectionIntensity = RenderSettings.reflectionIntensity;

        _originalSkybox = RenderSettings.skybox;
        if (_originalSkybox != null && _originalSkybox.HasProperty(ExposureId))
        {
            _originalSkyboxExposure = _originalSkybox.GetFloat(ExposureId);
            _runtimeSkybox = new Material(_originalSkybox);
        }
        // _Exposure가 없는 Skybox(또는 Skybox 없음)면 하늘 배경은 건드리지 않고 조명/환경광만 어둡게 한다.

        if (playerBodyRenderer != null && playerBodyRenderer.sharedMaterial.HasProperty(EmissionColorId))
        {
            _playerPropertyBlock = new MaterialPropertyBlock();
            _originalEmissionColor = playerBodyRenderer.sharedMaterial.GetColor(EmissionColorId);
        }
    }

    private void OnDestroy()
    {
        // 실행 중에 만든 Skybox 복사본은 씬이 바뀌어도 자동으로 지워지지 않으므로 직접 정리한다.
        if (_runtimeSkybox != null) Destroy(_runtimeSkybox);
    }

    // Play 중 Inspector에서 어둡기 값을 바꾸면 이미 어두운 상태에도 바로 반영해, 값을 고르기 쉽게 한다.
    private void OnValidate()
    {
        if (!Application.isPlaying || !_isDark) return;

        FadeTo(darkLightIntensity, darkAmbientScale, 0.2f);
        _playerTargetBrightness = -1f;
        // 목표값을 일부러 어긋나게 만들어, 다음 Update에서 플레이어 밝기도 새 값으로 다시 맞추게 한다.
    }

    private void Update()
    {
        UpdateDarkness();
        UpdatePlayerBrightness();
    }

    // 트리거에 걸쳐 있는 동안 플레이어 중심이 실제로 구역 범위 안에 있는지 직접 확인해 밝기를 정한다.
    // PlayerHealth.Respawn()은 CharacterController를 끈 채 순간이동시키므로 OnTriggerExit가 오지 않는데,
    // 이렇게 하면 구역 밖으로 리스폰돼도 밝기가 되돌아오고, 다시 걸어 들어오면 다시 어두워진다.
    private void UpdateDarkness()
    {
        if (!_playerInTrigger || _playerCollider == null) return;

        if (_zoneCollider.bounds.Contains(_playerCollider.bounds.center))
            Darken();
        else
            Brighten();
    }

    // 구역 밖이면 원래 밝기(1), 구역 안이면 횃불이 켜졌는지(TorchTool.IsTorchLit)에 따라 플레이어 밝기 목표를 정한다.
    // 목표가 바뀐 순간에만 Tween을 새로 시작하므로 매 프레임 Tween이 생기지 않는다.
    private void UpdatePlayerBrightness()
    {
        if (_playerPropertyBlock == null) return;

        float target = !_isDark ? 1f : (TorchTool.IsTorchLit ? torchPlayerBrightness : darkPlayerBrightness);
        if (Mathf.Approximately(target, _playerTargetBrightness)) return;

        _playerTargetBrightness = target;
        _playerTween?.Kill();
        _playerTween = DOTween.To(() => _playerBrightness, SetPlayerBrightness, target, playerFadeDuration)
            .SetEase(fadeEase);
    }

    private void OnDisable()
    {
        // 구역이 꺼지거나 씬이 바뀔 때 어두운 상태로 남지 않도록 Tween을 정리하고 원래 밝기로 즉시 복구한다.
        _fadeSequence?.Kill();
        if (_isDark || _currentAmbientScale < 1f)
        {
            if (skyLight != null) skyLight.intensity = _originalLightIntensity;
            SetAmbientScale(1f);
            _isDark = false;
        }

        _playerTween?.Kill();
        if (_playerPropertyBlock != null && _playerBrightness < 1f) SetPlayerBrightness(1f);
        _playerTargetBrightness = 1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerCollider = other;
        _playerInTrigger = true;
        // 실제로 어두워지는 시점은 Update에서 플레이어 중심이 구역 안으로 들어왔는지 보고 정한다.
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        _playerInTrigger = false;
        Brighten();
    }

    // 하늘 조명과 환경광을 어두운 값으로 서서히 바꾼다.
    private void Darken()
    {
        if (_isDark) return;

        _isDark = true;
        FadeTo(darkLightIntensity, darkAmbientScale, darkenDuration);
    }

    // 하늘 조명과 환경광을 씬 시작 때의 원래 값으로 서서히 되돌린다.
    private void Brighten()
    {
        if (!_isDark) return;

        _isDark = false;
        FadeTo(_originalLightIntensity, 1f, brightenDuration);
    }

    // 하늘 조명 밝기와 환경광 비율을 동시에 목표값까지 바꾸는 DOTween 연출이다.
    // 이전 연출이 진행 중이면 끊고 현재 값에서부터 새로 시작하므로, 빠르게 들락날락해도 값이 튀지 않는다.
    private void FadeTo(float lightIntensity, float ambientScale, float duration)
    {
        _fadeSequence?.Kill();
        _fadeSequence = DOTween.Sequence();

        if (skyLight != null)
            _fadeSequence.Join(skyLight.DOIntensity(lightIntensity, duration).SetEase(fadeEase));

        _fadeSequence.Join(
            DOTween.To(() => _currentAmbientScale, SetAmbientScale, ambientScale, duration).SetEase(fadeEase));
    }

    // 원본 환경광·반사광·하늘 배경 밝기에 같은 비율을 곱해 적용한다.
    // URP는 매 프레임 RenderSettings 값을 읽어 셰이더에 넘기므로 바꾸는 즉시 화면에 반영된다.
    private void SetAmbientScale(float scale)
    {
        _currentAmbientScale = scale;
        RenderSettings.ambientProbe = _originalAmbientProbe * scale;
        RenderSettings.reflectionIntensity = _originalReflectionIntensity * scale;

        if (_runtimeSkybox == null) return;

        if (scale >= 1f)
        {
            // 완전히 밝아졌으면 원본 Skybox로 되돌려, 구역 밖에서는 원래 상태와 똑같이 유지한다.
            RenderSettings.skybox = _originalSkybox;
        }
        else
        {
            _runtimeSkybox.SetFloat(ExposureId, _originalSkyboxExposure * scale);
            RenderSettings.skybox = _runtimeSkybox;
        }
    }

    // 플레이어 몸의 발광 색에 비율을 곱해 덮어쓴다.
    private void SetPlayerBrightness(float brightness)
    {
        _playerBrightness = brightness;
        if (playerBodyRenderer == null) return;

        if (brightness >= 1f)
        {
            // 원래 밝기로 돌아왔으면 덮어쓴 값을 지워 머티리얼 원래 상태(SRP Batcher 포함)로 되돌린다.
            playerBodyRenderer.SetPropertyBlock(null);
            return;
        }

        playerBodyRenderer.GetPropertyBlock(_playerPropertyBlock);
        _playerPropertyBlock.SetColor(EmissionColorId, _originalEmissionColor * brightness);
        playerBodyRenderer.SetPropertyBlock(_playerPropertyBlock);
    }

    [Button("어둡게 테스트")]
    private void TestDarken()
    {
        // 에디터(비 Play Mode)에서 누르면 씬의 조명 설정 자체가 바뀌어 저장될 수 있으므로 Play Mode에서만 동작한다.
        if (!Application.isPlaying) { Debug.LogWarning("[DarkZone] Play Mode에서만 테스트할 수 있습니다."); return; }
        Darken();
    }

    [Button("밝게 테스트")]
    private void TestBrighten()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[DarkZone] Play Mode에서만 테스트할 수 있습니다."); return; }
        Brighten();
    }
}
