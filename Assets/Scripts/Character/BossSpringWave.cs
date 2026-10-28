using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 비둘킹 봄 패턴(BossSpringIslandPattern)이 발판에 착지할 때 만드는 "물 벽 파도"다.
// 섬 가장자리에서 섬 중앙 방향으로 일직선으로 밀려가며, 플레이어가 맞으면 데미지 대신
// "휩쓸림" 상태가 되어 파도 진행 방향으로 계속 밀려난다(PlayerController.SetWindZone 재사용).
// 물에 빠지면(Y가 임계값 아래) 기존 FallZone/FallDeathHandler가 데미지+리스폰을 처리하므로
// 이 클래스는 낙사 처리를 하지 않는다.
// 판정은 매 프레임 Physics.OverlapBox로 직접 한다 — 트리거 콜백이 CharacterController와 불안정해서다.
// 파도 오브젝트는 콜라이더가 없는 시각 전용이며 Transform 이동만 사용한다(물리 충돌 대상이 아님).
public class BossSpringWave : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("시각 오브젝트")]
    private Transform visual;
    // 큐브 등 파도 모양 자식 오브젝트. 크기는 아래 설정값으로 런타임에 덮어쓴다.

    [SerializeField, LabelText("시각 렌더러")]
    private Renderer visualRenderer;
    // 페이드 아웃용. BI_Wave 머티리얼의 _BaseColor 알파를 MaterialPropertyBlock으로 바꾼다.

    [SerializeField, LabelText("물보라 파티클 (선택)")]
    private ParticleSystem foamParticles;
    // 파도 루트(스케일 안 걸린 곳)에 둔 물보라. 파도가 사라지기 시작하면 새 입자 생성을 멈춘다.

    [Title("파도 크기/이동")]
    [SerializeField, LabelText("길이(진행 방향의 수직)")]
    private float length = 32f;
    // 섬 전체 폭을 덮을 만큼 길게 잡는다.

    [SerializeField, LabelText("두께(진행 방향)")]
    private float thickness = 1f;

    [SerializeField, LabelText("높이")]
    private float height = 1f;
    // 플레이어 발이 이 높이 위에 있으면(점프 중) 맞지 않는다.

    [SerializeField, LabelText("이동 속도(m/s)")]
    private float speed = 4.5f;
    // 값이 작을수록 파도가 천천히 다가와 피할 시간이 늘어난다.

    [SerializeField, LabelText("이동 거리(m)")]
    private float travelDistance = 30f;
    // 이 거리를 이동하면 페이드 아웃 후 사라진다. 섬을 완전히 지나갈 만큼 잡는다.
    // Initialize에서 travelDistanceOverride를 받으면 그 값이 대신 쓰인다.

    [Title("휩쓸림 설정")]
    [SerializeField, LabelText("휩쓸림 시간")]
    private float pushDuration = 1f;
    // 맞은 플레이어를 이 시간만큼만 밀고 놓아준다. 길수록 멀리 밀려난다.

    [SerializeField, LabelText("휩쓸림 속도(m/s)")]
    private float pushSpeed = 5f;
    // 밀리는 동안의 속도. pushDuration x pushSpeed 가 대략적인 밀림 거리다.

    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;
    // 플레이어 발(CharacterController 아래쪽)이 이 값보다 낮으면 물에 빠진 것으로 보고 밀림을 해제한다(이후는 FallDeathHandler 담당).
    // 섬/발판 윗면(y=0)보다 조금 낮게 잡는다. 물속 FallZone(윗면 약 -1.1)보다 낮게 잡으면
    // 복귀 지점으로 순간이동된 뒤에도 밀림이 남아 플레이어를 다시 물로 밀어버린다.

    [SerializeField, LabelText("바위 그림자 검사 높이")]
    private float shadowRayHeight = 0.5f;

    [Title("DOTween 설정")]
    [SerializeField, LabelText("나타나는 시간")]
    private float appearDuration = 0.25f;

    [SerializeField, LabelText("사라지는 시간")]
    private float disappearDuration = 0.4f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("플레이어 휩쓸림 중")]
    private bool _caught;

    private PlayerController _player;
    private CharacterController _playerCC;
    private IReadOnlyList<IslandRock> _rocks;
    private Vector3 _spawnPosition;
    private Vector3 _direction;
    private Quaternion _rotation;
    private float _travelled;
    private float _activeTravelDistance; // 실제로 쓰는 이동 거리(override가 있으면 override)
    private float _caughtTimer;
    private bool _hasCaught; // 한 파도당 한 번만 휩쓸리게 하기 위한 표시
    private bool _initialized;
    private bool _visualEnded;
    private bool _fadeDone;
    private float _visualAlpha = 1f;
    private Color _baseColor = Color.white;
    private MaterialPropertyBlock _block;
    private Tween _appearTween;
    private Tween _fadeTween;
    private readonly Collider[] _overlapBuffer = new Collider[16];

    // 패턴이 파도를 만든 직후 호출한다. spawnPosition은 파도 중심선의 시작점(지면 높이), travelDir은 XZ 진행 방향이다.
    public void Initialize(Vector3 spawnPosition, Vector3 travelDir, PlayerController player, IReadOnlyList<IslandRock> rocks, float travelDistanceOverride = -1f)
    {
        // 섬 가장자리까지만 가도록 패턴이 거리를 계산해 넘겨주면 그 값을 쓴다(0 이하면 기존 travelDistance).
        _activeTravelDistance = travelDistanceOverride > 0f ? travelDistanceOverride : travelDistance;
        _spawnPosition = spawnPosition;
        _direction = new Vector3(travelDir.x, 0f, travelDir.z).normalized;
        _rotation = Quaternion.LookRotation(_direction, Vector3.up);
        _player = player;
        _playerCC = player != null ? player.GetComponent<CharacterController>() : null;
        _rocks = rocks;

        transform.SetPositionAndRotation(spawnPosition, _rotation);

        _block = new MaterialPropertyBlock();
        if (visualRenderer != null && visualRenderer.sharedMaterial != null
            && visualRenderer.sharedMaterial.HasProperty("_BaseColor"))
            _baseColor = visualRenderer.sharedMaterial.GetColor("_BaseColor");

        // 시각 오브젝트는 Y 스케일 0에서 시작해 위로 솟아오른다. 아래쪽 면이 지면에 붙도록 위치는 높이의 절반이다.
        _visualAlpha = 1f;
        ApplyVisualAlpha();
        visual.localScale = new Vector3(length, 0.01f, thickness);
        visual.localPosition = new Vector3(0f, 0.005f, 0f);
        _appearTween = DOTween.To(() => 0f, SetVisualHeight01, 1f, appearDuration).SetEase(Ease.OutQuad);

        _initialized = true;
    }

    // 진행 중인 파도를 즉시 정리한다(패턴 중단 시). 휩쓸림 상태도 해제한다.
    public void Cancel()
    {
        ReleasePlayer();
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!_initialized) return;

        if (!_visualEnded)
        {
            float step = speed * Time.deltaTime;
            transform.position += _direction * step;
            _travelled += step;

            if (!_caught && !_hasCaught) CheckHit();

            if (_travelled >= _activeTravelDistance) BeginDisappear();
        }

        if (_caught) TickCaught();

        // 연출이 끝났고 휩쓸림도 끝났을 때만 파괴한다(휩쓸림은 파도 시각이 사라진 뒤에도 이어져야 하므로).
        if (_fadeDone && !_caught) Destroy(gameObject);
    }

    private void OnDisable()
    {
        ReleasePlayer();
        if (_appearTween != null) _appearTween.Kill();
        if (_fadeTween != null) _fadeTween.Kill();
    }

    private void OnDestroy()
    {
        ReleasePlayer();
        if (_appearTween != null) _appearTween.Kill();
        if (_fadeTween != null) _fadeTween.Kill();
    }

    private void SetVisualHeight01(float t)
    {
        float h = Mathf.Max(height * t, 0.01f);
        visual.localScale = new Vector3(length, h, thickness);
        visual.localPosition = new Vector3(0f, h * 0.5f, 0f);
    }

    // 파도 부피와 플레이어 CharacterController가 겹치는지 검사한다.
    private void CheckHit()
    {
        if (_playerCC == null || !_playerCC.enabled) return;

        // 발이 파도 윗면 위에 있으면(점프로 넘는 중) 맞지 않는다.
        float waveTop = transform.position.y + height;
        if (_playerCC.bounds.min.y >= waveTop - 0.05f) return;

        Vector3 center = transform.position + Vector3.up * (height * 0.5f);
        Vector3 halfExtents = new Vector3(length * 0.5f, height * 0.5f, thickness * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, _rotation,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (_overlapBuffer[i] != _playerCC) continue;

            if (IsPlayerInRockShadow()) return;

            _caught = true;
            _hasCaught = true;
            _caughtTimer = 0f;
            return;
        }
    }

    // 플레이어 뒤쪽(파도가 오는 쪽)에 솟아오른 바위가 있으면 파도를 막아준다.
    private bool IsPlayerInRockShadow()
    {
        Vector3 playerPos = _player.transform.position;
        Vector3 origin = new Vector3(playerPos.x, transform.position.y + shadowRayHeight, playerPos.z);

        // 파도 시작선에서 플레이어까지의 진행 방향 거리만큼만 뒤로 쏜다.
        float backDistance = Vector3.Dot(origin - _spawnPosition, _direction);
        if (backDistance <= 0f) return false;

        RaycastHit[] hits = Physics.RaycastAll(origin, -_direction, backDistance, Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            IslandRock rock = hit.collider.GetComponentInParent<IslandRock>();
            if (rock != null && rock.IsRisen && !rock.IsBroken) return true;
        }
        return false;
    }

    // 휩쓸린 동안 매 프레임 진행 방향 바람을 걸어 밀어낸다.
    private void TickCaught()
    {
        _player.SetWindZone(_direction * pushSpeed);
        _caughtTimer += Time.deltaTime;

        float feetY = _playerCC != null ? _playerCC.bounds.min.y : _player.transform.position.y;
        if (feetY < fallY || _caughtTimer >= pushDuration)
            ReleasePlayer();
    }

    private void ReleasePlayer()
    {
        if (!_caught) return;
        _caught = false;
        if (_player != null) _player.ClearWindZone();
    }

    private void BeginDisappear()
    {
        _visualEnded = true;
        if (foamParticles != null) foamParticles.Stop(); // 이미 나온 입자는 자연스럽게 사라지게 둔다.
        if (_appearTween != null) _appearTween.Kill();

        // 투명해지면서 납작해진다.
        float startHeight = visual.localScale.y;
        _fadeTween = DOTween.To(() => 1f, v =>
        {
            _visualAlpha = v;
            ApplyVisualAlpha();
            float h = Mathf.Max(startHeight * v, 0.01f);
            visual.localScale = new Vector3(length, h, thickness);
            visual.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }, 0f, disappearDuration).OnComplete(() =>
        {
            _fadeDone = true;
            visual.gameObject.SetActive(false);
        });
    }

    private void ApplyVisualAlpha()
    {
        if (visualRenderer == null) return;
        Color c = _baseColor;
        c.a *= _visualAlpha;
        _block.SetColor("_BaseColor", c);
        visualRenderer.SetPropertyBlock(_block);
    }
}
