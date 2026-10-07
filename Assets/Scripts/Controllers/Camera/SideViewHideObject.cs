using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

// 사이드뷰 카메라와 플레이어 사이에 끼어 화면을 가리는 오브젝트(집 등)를 사이드뷰일 때만 숨기는 컴포넌트다.
// 탑다운에서는 그대로 보이고, 콜라이더는 건드리지 않으므로 충돌은 계속 유지된다.
// 같은 씬에 사이드뷰 라인이 여러 개일 수 있으므로, 플레이어 Z가 지정한 범위 안에 있을 때만 숨긴다.
public class SideViewHideObject : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("플레이어")]
    private PlayerController player;
    // Inspector에서 씬의 Player를 연결한다. 시점 모드와 Z 위치를 읽는 데 사용한다.

    [Title("숨길 조건")]
    [SerializeField, LabelText("플레이어 Z 최소")]
    private float minPlayerZ = 26f;

    [SerializeField, LabelText("플레이어 Z 최대")]
    private float maxPlayerZ = 33f;
    // 사이드뷰인 플레이어의 Z가 이 범위 안일 때만 숨긴다.
    // 이 오브젝트가 가리는 사이드뷰 라인을 감싸도록 넣는다(다른 라인을 걸을 땐 배경으로 계속 보이게 하기 위해).

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("숨김 중")]
    private bool _isHidden;

    [ReadOnly, ShowInInspector, LabelText("테스트 강제 상태 사용 중")]
    private bool _useTestOverride;
    private bool _testHidden;
    // 테스트 버튼으로 정한 상태다. 켜져 있는 동안은 Update의 조건 판정보다 우선한다("조건대로 되돌리기" 버튼으로 해제).

    private Renderer[] _renderers;
    private ShadowCastingMode[] _originalShadowModes;
    // 숨길 때 렌더러를 끄는 대신 그림자만 남기기(ShadowsOnly)로 바꾼다 — 오브젝트는 안 보여도 그림자는 남아
    // 길 위 명암이 갑자기 바뀌지 않는다. 다시 보일 때 원래 그림자 설정으로 되돌리기 위해 저장해 둔다.

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalShadowModes = new ShadowCastingMode[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalShadowModes[i] = _renderers[i].shadowCastingMode;
    }

    private void Update()
    {
        if (player == null) return;

        // 시점 전환 이벤트만 쓰면, 사이드뷰 상태 그대로 리스폰으로 다른 라인에 옮겨졌을 때 반응하지 못한다.
        // 그래서 매 프레임 조건만 확인하고, 실제 렌더러 변경은 상태가 바뀔 때만 한다.
        float z = player.transform.position.z;
        bool shouldHide = player.CurrentViewMode == ViewMode.SideView && z >= minPlayerZ && z <= maxPlayerZ;
        if (_useTestOverride)
            shouldHide = _testHidden;

        if (shouldHide != _isHidden)
            SetHidden(shouldHide);
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼지거나 씬이 바뀔 때 숨긴 상태로 남지 않게 원래대로 돌려놓는다.
        if (_isHidden)
            SetHidden(false);
    }

    private void SetHidden(bool hidden)
    {
        _isHidden = hidden;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : _originalShadowModes[i];
        }
    }

    [Title("테스트")]
    [Button("숨기기 테스트")]
    private void TestHide()
    {
        _useTestOverride = true;
        _testHidden = true;
    }

    [Button("보이기 테스트")]
    private void TestShow()
    {
        _useTestOverride = true;
        _testHidden = false;
    }

    [Button("조건대로 되돌리기")]
    private void TestClearOverride()
    {
        _useTestOverride = false;
    }
}
