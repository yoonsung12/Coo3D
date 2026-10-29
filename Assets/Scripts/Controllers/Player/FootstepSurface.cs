using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// 발소리 바닥 종류다. Default는 PlayerFootstep에 원래 연결된 기본 발소리를 그대로 쓴다.
public enum FootstepSurfaceType
{
    Default,
    Ice,
    Snow,
    Grass
}

// 재질만으로 판정되지 않는 특별한 바닥에 붙여 발소리 종류를 직접 지정한다.
// - 기본: 이 오브젝트(와 자식)의 콜라이더를 밟으면 이 종류로 판정한다.
// - 영역으로 판정: 콜라이더가 없는 장식(바닥 위에 얹은 눈 덮개 등)에 쓴다.
//   이 오브젝트의 렌더러가 차지하는 범위 안에 발이 있으면, 실제로 밟은 콜라이더와 상관없이 이 종류로 판정한다.
public class FootstepSurface : MonoBehaviour
{
    [SerializeField, LabelText("바닥 종류")]
    private FootstepSurfaceType surfaceType = FootstepSurfaceType.Default;
    // 이 오브젝트를 밟았을 때 낼 발소리 종류다.

    [SerializeField, LabelText("영역으로 판정")]
    private bool useRendererArea;
    // 콜라이더 없이 바닥 위에 얹힌 장식에 켠다. 켜면 렌더러 범위(위아래로 조금 넓힌 상자)로 판정한다.

    [SerializeField, LabelText("위아래 여유 (m)"), ShowIf(nameof(useRendererArea))]
    private float verticalMargin = 0.6f;
    // 얇은 덮개도 발 위치를 놓치지 않도록 범위를 위아래로 넓히는 값이다.

    public FootstepSurfaceType SurfaceType => surfaceType;

    // 영역 판정을 켠 바닥 목록이다. PlayerFootstep이 GameObject.Find 없이 바로 검사할 수 있도록 켜질 때 등록한다.
    private static readonly List<FootstepSurface> AreaSurfaces = new();

    private Bounds _area;
    // 시작할 때 계산해 둔 판정 범위다. 눈 덮개처럼 움직이지 않는 장식에 쓰는 것을 전제로 한다.

    private void OnEnable()
    {
        if (!useRendererArea) return;
        if (!TryCalculateArea(out _area)) return;
        AreaSurfaces.Add(this);
    }

    private void OnDisable()
    {
        AreaSurfaces.Remove(this);
    }

    // 자신과 자식 렌더러를 모두 합친 범위를 구하고 위아래로 여유를 준다.
    private bool TryCalculateArea(out Bounds area)
    {
        area = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        area = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            area.Encapsulate(renderers[i].bounds);
        area.Expand(new Vector3(0f, verticalMargin * 2f, 0f));
        return true;
    }

    // 발 위치가 영역 판정 바닥 안에 있으면 그 종류를 돌려준다.
    public static bool TryGetAreaSurface(Vector3 footPosition, out FootstepSurfaceType type)
    {
        foreach (FootstepSurface surface in AreaSurfaces)
        {
            if (surface._area.Contains(footPosition))
            {
                type = surface.surfaceType;
                return true;
            }
        }
        type = FootstepSurfaceType.Default;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!useRendererArea) return;
        if (!TryCalculateArea(out Bounds area)) return;
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
