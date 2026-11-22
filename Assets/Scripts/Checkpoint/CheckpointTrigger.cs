using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 실제 "저장 지점" 역할을 하는 체크포인트다. 플레이어가 처음 닿으면
// 리스폰 지점 갱신 + ES3 저장 + "체크포인트에 저장되었습니다" 안내를 한 번에 처리한다.
// 체력은 회복하지 않는다(맵을 넘어갈 때만 전부 회복된다).
// RegionCheckpoint보다 드물게 배치되는 것을 전제로 한다.
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Title("체크포인트 설정")]
    [SerializeField, LabelText("리스폰 위치 (비워두면 이 오브젝트 위치 사용)")]
    private Transform respawnPoint;

    [SerializeField, LabelText("한 번만 활성화")]
    private bool activateOnce = true;
    // 켜두면 한 번 활성화된 체크포인트는 다시 지나가도 저장·안내를 하지 않는다.

    [ReadOnly, ShowInInspector, LabelText("활성화 여부")]
    private bool _activated;

    [Title("활성화 연출")]
    [SerializeField, LabelText("연출 대상 (비워두면 연출 생략)")]
    private Transform visualEffect;

    [SerializeField, LabelText("펀치 스케일 강도")]
    private float punchScale = 0.3f;

    [SerializeField, LabelText("펀치 지속 시간")]
    private float punchDuration = 0.4f;

    [Title("저장 안내 (DOTween)")]
    [SerializeField, LabelText("저장 안내 패널")]
    private CanvasGroup saveNoticePanel;
    // GameCanvas 아래의 CheckpointSaveNotice 패널을 연결한다. 비워 두면 안내 없이 저장만 한다.
    // 한 씬의 체크포인트들이 같은 패널 하나를 함께 쓴다.

    [SerializeField, LabelText("나타나는 시간")]
    private float noticeFadeInDuration = 0.25f;

    [SerializeField, LabelText("유지 시간")]
    private float noticeHoldDuration = 1.5f;
    // 안내가 완전히 보인 채 머무는 시간이다. 길수록 오래 보인다.

    [SerializeField, LabelText("사라지는 시간")]
    private float noticeFadeOutDuration = 0.3f;

    [SerializeField, LabelText("내려오는 거리(px)")]
    private float noticeSlideDistance = 20f;
    // 나타날 때 이 거리만큼 위에서 내려오며 제자리로 온다. 0이면 제자리에서 페이드만 한다.

    [SerializeField, LabelText("Ease")]
    private Ease noticeEase = Ease.OutQuad;

    private Tween _punchTween;
    private Sequence _noticeSequence;
    private RectTransform _noticeRect;
    private Vector2 _noticeRestPosition;
    // 패널의 원래(제자리) 위치다. 연출이 중간에 끊겨도 항상 이 위치를 기준으로 다시 시작한다.

    private void Awake()
    {
        if (saveNoticePanel == null) return;

        _noticeRect = saveNoticePanel.GetComponent<RectTransform>();
        _noticeRestPosition = _noticeRect.anchoredPosition;
        saveNoticePanel.alpha = 0f;
        saveNoticePanel.blocksRaycasts = false;
        // 평소에는 투명하게 두고, 화면 클릭/조작을 막지 않게 한다.
    }

    private void OnDestroy()
    {
        _punchTween?.Kill();
        _noticeSequence?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activateOnce && _activated) return;
        if (other.GetComponent<PlayerController>() == null) return;

        // SaveManager가 없으면(에디터 단독 실행) 이 오브젝트의 _activated로 첫 진입을 판단한다.
        // 불러오기 후 씬이 다시 로드돼도 SaveManager가 기억한 ID로 "이미 지나간 체크포인트"를 구분한다.
        bool firstEntry = SaveManager.Instance != null
            ? SaveManager.Instance.MarkCheckpointActivated(SaveManager.GetObjectId(gameObject))
            : !_activated;

        if (firstEntry)
        {
            Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
            CheckpointManager.SetCheckpoint(position, gameObject.scene.name);

            if (SaveManager.Instance != null)
                SaveManager.Instance.SaveCheckpoint();

            PlayActivateEffect();
            ShowSaveNotice();
        }

        _activated = true;
    }

    // "체크포인트에 저장되었습니다" 안내를 위에서 살짝 내려오며 나타났다가 사라지게 한다.
    // 같은 패널을 다른 체크포인트도 쓰므로, 패널에 걸린 Tween을 모두 정리한 뒤 처음부터 다시 재생한다.
    private void ShowSaveNotice()
    {
        if (saveNoticePanel == null || _noticeRect == null) return;

        DOTween.Kill(saveNoticePanel);
        _noticeSequence?.Kill();

        saveNoticePanel.alpha = 0f;
        _noticeRect.anchoredPosition = _noticeRestPosition + Vector2.up * noticeSlideDistance;

        _noticeSequence = DOTween.Sequence()
            .Append(saveNoticePanel.DOFade(1f, noticeFadeInDuration).SetEase(noticeEase))
            .Join(_noticeRect.DOAnchorPos(_noticeRestPosition, noticeFadeInDuration).SetEase(noticeEase))
            .AppendInterval(noticeHoldDuration)
            .Append(saveNoticePanel.DOFade(0f, noticeFadeOutDuration).SetEase(noticeEase))
            .SetTarget(saveNoticePanel)
            .SetLink(saveNoticePanel.gameObject);
        // SetTarget: 다른 체크포인트가 DOTween.Kill(패널)로 이 연출을 멈출 수 있게 패널을 대상으로 묶는다.
        // SetLink: 씬 전환 등으로 패널이 사라지면 연출도 함께 정리된다.
    }

    [Button("저장 안내 테스트")]
    private void TestSaveNotice()
    {
        if (!Application.isPlaying) return;
        ShowSaveNotice();
    }

    private void PlayActivateEffect()
    {
        if (visualEffect == null) return;

        _punchTween?.Kill();
        visualEffect.localScale = Vector3.one;
        _punchTween = visualEffect.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 8, elasticity: 0.6f);
    }
}
