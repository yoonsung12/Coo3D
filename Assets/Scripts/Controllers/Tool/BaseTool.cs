using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 선풍기, 우산, 횃불 등 모든 도구의 공통 기반 클래스다.
// 도구 비주얼 활성화/비활성화 연출과 장착 사운드를 공통으로 처리한다.
public abstract class BaseTool : MonoBehaviour
{
    [Title("비주얼 / 사운드")]
    [SerializeField, LabelText("도구 비주얼 오브젝트")]
    private GameObject toolVisual;
    // Inspector에서 손에 들릴 3D 오브젝트를 연결한다.
    // 해당 오브젝트는 시작 시 비활성화 상태로 둬야 한다.

    [SerializeField, LabelText("장착 사운드")]
    private AudioClip equipSound;
    // 도구를 장착할 때 재생할 사운드 클립이다. 나중에 연결한다.

    [Title("장착 연출 설정")]
    [SerializeField, LabelText("등장 시간")]
    private float equipDuration = 0.2f;
    // 도구가 손에 들어오는 애니메이션 재생 시간이다.

    [SerializeField, LabelText("사라짐 시간")]
    private float unequipDuration = 0.15f;
    // 도구가 손에서 빠져나가는 애니메이션 재생 시간이다.

    private Tween _equipTween;
    // 장착/해제 연출 Tween을 저장해 중복 실행을 방지한다.

    private Vector3 _visualBaseScale = Vector3.one;
    // 씬에 배치된 도구 비주얼의 원래 크기다.
    // FBX 모델은 스케일이 100처럼 1이 아닐 수 있어서, 장착 연출이 이 크기까지 키워야 제 크기로 보인다.

    protected virtual void Awake()
    {
        if (toolVisual == null) return;

        _visualBaseScale = toolVisual.transform.localScale;
        toolVisual.SetActive(false);
        // 원래 크기를 저장한 뒤 숨긴다. 씬에서 켜진 채 저장돼도 시작할 때는 손에 아무것도 들지 않은 상태가 된다.
    }

    private void OnDestroy()
    {
        _equipTween?.Kill();
        // 오브젝트가 파괴될 때 진행 중인 Tween을 정리해 오류를 방지한다.
    }

    // 도구를 장착했을 때 한 번 호출된다.
    // 비주얼은 장착만으로는 보이지 않고, 사용 버튼을 누를 때 ShowVisual()로 나타난다.
    public virtual void OnEquip()
    {
        if (equipSound != null)
            AudioSource.PlayClipAtPoint(equipSound, transform.position);
        // 장착 사운드를 재생한다. 클립이 연결되지 않으면 무시한다.
    }

    // 도구를 해제했을 때 한 번 호출된다.
    public virtual void OnUnequip()
    {
        HideVisual();
    }

    // 도구 비주얼을 손에 쑥 들어오는 연출과 함께 보여준다. ToolManager가 사용 버튼을 누를 때 호출한다.
    public virtual void ShowVisual()
    {
        if (toolVisual == null) return;

        toolVisual.SetActive(true);

        _equipTween?.Kill();
        toolVisual.transform.localScale = Vector3.zero;
        _equipTween = toolVisual.transform
            .DOScale(_visualBaseScale, equipDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
        // SetUpdate(true)로 WeaponWheel 사용 중(Time.timeScale=0)에도 애니메이션이 재생된다.
        // OutBack은 손에 쑥 들어오는 탄성 있는 느낌을 준다.
    }

    // 도구 비주얼을 손에서 빠져나가는 연출 후 숨긴다. 사용 버튼을 떼거나 도구를 해제할 때 호출된다.
    public virtual void HideVisual()
    {
        if (toolVisual == null || !toolVisual.activeSelf) return;
        // 이미 숨겨져 있으면 연출을 다시 재생하지 않는다.

        _equipTween?.Kill();
        _equipTween = toolVisual.transform
            .DOScale(Vector3.zero, unequipDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() => toolVisual.SetActive(false));
        // 손에서 빠져나가는 연출 후 비활성화한다.
        // InBack은 살짝 당겼다 빠르게 사라지는 느낌을 준다.
    }

    // 사용 버튼을 처음 눌렀을 때 한 번 호출된다.
    public virtual void OnUsePerformed() { }

    // 사용 버튼을 누르는 동안 매 프레임 호출된다.
    public virtual void OnUseFrame() { }

    // 사용 버튼을 뗐을 때 한 번 호출된다.
    public virtual void OnUseRelease() { }

    // 도구 사용을 즉시 중단할 때 호출된다.
    public virtual void StopUsing() { }
}
