using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 실제 "저장 지점" 역할을 하는 체크포인트다. 플레이어가 닿으면
// 리스폰 지점 갱신 + ES3 저장 + 체력 전체 회복을 한 번에 처리한다.
// RegionCheckpoint보다 드물게 배치되는 것을 전제로 한다.
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Title("체크포인트 설정")]
    [SerializeField, LabelText("리스폰 위치 (비워두면 이 오브젝트 위치 사용)")]
    private Transform respawnPoint;

    [SerializeField, LabelText("한 번만 활성화")]
    private bool activateOnce = true;
    // 켜두면 한 번 활성화된 체크포인트는 다시 지나가도 저장을 반복하지 않는다.

    [ReadOnly, ShowInInspector, LabelText("활성화 여부")]
    private bool _activated;

    [Title("활성화 연출")]
    [SerializeField, LabelText("연출 대상 (비워두면 연출 생략)")]
    private Transform visualEffect;

    [SerializeField, LabelText("펀치 스케일 강도")]
    private float punchScale = 0.3f;

    [SerializeField, LabelText("펀치 지속 시간")]
    private float punchDuration = 0.4f;

    private Tween _punchTween;

    private void OnDestroy()
    {
        _punchTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activateOnce && _activated) return;
        if (other.GetComponent<PlayerController>() == null) return;

        Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
        CheckpointManager.SetCheckpoint(position, gameObject.scene.name);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveCheckpoint();

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(float.MaxValue);
        // Heal은 내부에서 최대 체력을 넘지 않도록 제한하므로, 아주 큰 값을 넘겨 사실상 전체 회복시킨다.

        _activated = true;
        PlayActivateEffect();
    }

    private void PlayActivateEffect()
    {
        if (visualEffect == null) return;

        _punchTween?.Kill();
        visualEffect.localScale = Vector3.one;
        _punchTween = visualEffect.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 8, elasticity: 0.6f);
    }
}
