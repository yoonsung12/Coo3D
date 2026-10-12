using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

// 사이드뷰 카메라와 플레이어 사이를 가리는 지형/건물 메쉬를,
// 플레이어가 이 구역 안에 있을 때만 숨긴다.
// 페이드(반투명) 방식은 공유 URP 머티리얼을 Transparent로 바꿔야 해서,
// 같은 돌 머티리얼을 쓰는 다른 바닥까지 영향받으므로 쓰지 않고 렌더러 단위로 숨긴다.
[RequireComponent(typeof(Collider))]
public class OccluderHideZone : MonoBehaviour
{
    [SerializeField, LabelText("숨길 렌더러")]
    [InfoBox("카메라 앞을 가리는 메쉬의 Renderer를 연결한다.\n" +
             "콜라이더는 그대로라 물리/충돌에는 영향이 없고, 그림자는 유지된다.")]
    private Renderer[] hideRenderers;
    // 플레이어가 구역 안에 있는 동안 안 보이게 만들 렌더러 목록이다.
    // 건물처럼 자식 메쉬가 여러 개면 자식 렌더러를 모두 넣어야 통째로 사라진다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("숨김 상태")]
    private bool _isHidden;

    private ShadowCastingMode[] _originalModes;
    // 복구할 때 쓰려고 Awake에서 저장해 둔 원래 그림자 모드다.

    private Collider _zoneCollider;
    // 리스폰 순간이동 때 OnTriggerExit가 안 오는 경우를 대비해 위치로 직접 검사하는 데 쓴다.

    private Transform _playerTransform;

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider>();

        _originalModes = new ShadowCastingMode[hideRenderers.Length];
        for (int i = 0; i < hideRenderers.Length; i++)
        {
            if (hideRenderers[i] != null)
                _originalModes[i] = hideRenderers[i].shadowCastingMode;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 실제 숨김/복구는 Update에서 플레이어 중심 위치로 판단한다.
        // 캡슐 가장자리만 닿은 순간(중심은 아직 밖)에 바로 숨기면, 다음 프레임에 복구된 뒤 Enter가 다시 오지 않아 안 숨겨지기 때문이다.
        _playerTransform = player.transform;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        _playerTransform = null;
        Restore();
    }

    private void Update()
    {
        // PlayerHealth.Respawn()은 CharacterController를 끈 채 옮겨서 OnTriggerExit가 안 올 수 있다.
        // 그래서 트리거에 닿아 있는 동안은 매 프레임 Bounds 검사 한 번으로 중심 위치가 안이면 숨기고 밖이면 복구한다.
        if (_playerTransform == null) return;

        if (_zoneCollider.bounds.Contains(_playerTransform.position))
            Hide();
        else
            Restore();
    }

    private void OnDisable()
    {
        // 구역이 꺼지거나 씬이 언로드될 때 메쉬가 숨겨진 채로 남지 않게 한다.
        Restore();
    }

    // 메쉬는 안 보이게 하되 그림자는 남겨서, 구역 안 조명이 갑자기 달라지지 않게 한다.
    private void Hide()
    {
        if (_isHidden) return;
        _isHidden = true;

        foreach (Renderer r in hideRenderers)
        {
            if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private void Restore()
    {
        if (!_isHidden) return;
        _isHidden = false;

        if (_originalModes == null) return;
        for (int i = 0; i < hideRenderers.Length; i++)
        {
            if (hideRenderers[i] != null) hideRenderers[i].shadowCastingMode = _originalModes[i];
        }
    }

    [Button("숨기기 테스트")]
    private void TestHide()
    {
        // 에디터 테스트용: Awake가 안 돈 상태면 원래 모드를 먼저 저장한다.
        if (_originalModes == null) Awake();
        Hide();
    }

    [Button("복구 테스트")]
    private void TestRestore()
    {
        Restore();
    }
}
