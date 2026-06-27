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

    private void OnDestroy()
    {
        _equipTween?.Kill();
        // 오브젝트가 파괴될 때 진행 중인 Tween을 정리해 오류를 방지한다.
    }

    // 도구를 장착했을 때 한 번 호출된다.
    public virtual void OnEquip()
    {
        if (toolVisual != null)
        {
            toolVisual.SetActive(true);

            _equipTween?.Kill();
            toolVisual.transform.localScale = Vector3.zero;
            _equipTween = toolVisual.transform
                .DOScale(Vector3.one, equipDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            // SetUpdate(true)로 WeaponWheel 사용 중(Time.timeScale=0)에도 애니메이션이 재생된다.
            // OutBack은 손에 쑥 들어오는 탄성 있는 느낌을 준다.
        }

        if (equipSound != null)
            AudioSource.PlayClipAtPoint(equipSound, transform.position);
        // 장착 사운드를 재생한다. 클립이 연결되지 않으면 무시한다.
    }

    // 도구를 해제했을 때 한 번 호출된다.
    public virtual void OnUnequip()
    {
        if (toolVisual == null) return;

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
