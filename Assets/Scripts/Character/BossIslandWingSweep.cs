using System;
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹" 섬 아레나 평범한 공격 1: 날개 쓸기.
// 준비 동작 -> 보스 주변 원형 예고 -> 날개를 휘둘러 범위 안 플레이어를 밀쳐낸다 -> 날개를 편 채 경직.
// 스케줄러(BossIslandBasicPattern)가 Execute/Abort를 호출한다.
public class BossIslandWingSweep : MonoBehaviour
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;
    // 연출은 루트(Rigidbody)가 아니라 자식 시각 오브젝트에만 걸어 물리와 충돌하지 않게 한다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("원 예고 프리팹")]
    private GroundTelegraph circleTelegraphPrefab;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("준비 시간")]
    private float windupDuration = 1f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("예고 시간")]
    private float telegraphDuration = 0.6f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("휘두르는 시간")]
    private float sweepDuration = 0.2f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("경직(회복) 시간")]
    private float recoveryDuration = 1f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("Ease")]
    private Ease poseEase = Ease.OutQuad;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("쓸기 반지름")]
    private float sweepRadius = 3.5f;
    // 보스 위치 기준 평면 거리가 이 값 이하면 맞는다. 스케줄러의 밀치기 발동 거리와 맞춰 둔다.

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("피해량")]
    private float damage = 20f;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("밀려나는 속도")]
    private float knockbackSpeed = 12f;
    // 수평 밀침 속도다. 값이 클수록 멀리 밀려난다(약 6m 목표로 튜닝).

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("튕겨 오르는 속도")]
    private float knockbackUp = 4f;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("준비 자세 내려가는 높이")]
    private float windupDropY = 0.3f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("준비 자세 눌림 배율")]
    private Vector3 windupScaleMul = new Vector3(1.1f, 0.85f, 1.1f);

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("날개 편 자세 배율")]
    private Vector3 wingsOpenScaleMul = new Vector3(1.3f, 1f, 1.3f);

    [Title("사운드")]
    [SerializeField, LabelText("AudioSource (비우면 같은 오브젝트)")]
    private AudioSource audioSource;
    // 보스에 붙은 효과음(SFX 믹서) AudioSource를 같이 쓴다. 비워두면 Awake에서 같은 오브젝트에서 찾는다.

    [SerializeField, LabelText("준비 소리 (선택)")]
    private AudioClip windupClip;
    // 몸을 낮추며 힘을 모을 때(날개 젖힘) 한 번 재생된다.

    [SerializeField, LabelText("준비 소리 볼륨"), Range(0f, 1f)]
    private float windupVolume = 0.8f;

    [SerializeField, LabelText("휘두르는 소리 (선택)")]
    private AudioClip sweepClip;
    // 날개를 휘둘러 판정이 나는 순간 재생된다.

    [SerializeField, LabelText("휘두르는 소리 볼륨"), Range(0f, 1f)]
    private float sweepVolume = 1f;

    private Coroutine _routine;
    private Sequence _poseSequence;
    private GroundTelegraph _telegraph;
    private Vector3 _startLocalPosition;
    private Quaternion _startLocalRotation;
    private Vector3 _startLocalScale;
    private BossAnimationController bossAnim;
    private void Awake()
    {
        if (bossVisual != null)
        {
            // 계절 패턴도 같은 시각 오브젝트를 쓰므로, 끝나면 반드시 이 원래 값으로 정확히 돌려놓는다.
            _startLocalPosition = bossVisual.localPosition;
            _startLocalRotation = bossVisual.localRotation;
            _startLocalScale = bossVisual.localScale;
        }
        bossAnim = GetComponent<BossAnimationController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    // 소리가 연결돼 있을 때만 재생한다. 비어 있으면 아무 일도 없다.
    private void PlaySfx(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    private void OnDisable()
    {
        Abort();
    }

    private void OnDestroy()
    {
        _poseSequence?.Kill();
    }

    // flatDirection은 이 공격에서는 판정에 쓰지 않지만 스케줄러 공통 시그니처를 맞추기 위해 받는다.
    public void Execute(Vector3 flatDirection, Action onFinished)
    {
        Abort();
        _routine = StartCoroutine(SweepRoutine(onFinished));
    }

    // 진행 중인 공격을 즉시 끊고 예고와 자세를 정리한다.
    public void Abort()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        _poseSequence?.Kill();
        _poseSequence = null;
        DestroyTelegraph();
        ResetPose();
    }

    private IEnumerator SweepRoutine(Action onFinished)
    {
        // 준비: 몸을 낮추고 눌러 힘을 모으는 느낌을 준다.
        PlaySfx(windupClip, windupVolume);
        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOLocalMoveY(_startLocalPosition.y - windupDropY, windupDuration).SetEase(poseEase))
                .Join(bossVisual.DOScale(Vector3.Scale(_startLocalScale, windupScaleMul), windupDuration).SetEase(poseEase))
                .SetLink(gameObject);
        }
        yield return new WaitForSeconds(windupDuration);

        // 예고: 보스 발밑에 쓸기 범위를 보여준 뒤 고정(Lock)한다.
        Vector3 center = GetBossFlat();
        if (circleTelegraphPrefab != null)
        {
            _telegraph = Instantiate(circleTelegraphPrefab);
            _telegraph.ShowCircle(center, sweepRadius);
        }
        yield return new WaitForSeconds(telegraphDuration * 0.6f);
        if (_telegraph != null) _telegraph.Lock();
        yield return new WaitForSeconds(telegraphDuration * 0.4f);

        // 휘두르기: 한 바퀴 돌며 날개를 편다. 판정은 이 순간 한 번만 한다.
        bossAnim?.PlayWing();
        PlaySfx(sweepClip, sweepVolume);
        DestroyTelegraph();
        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOLocalRotate(_startLocalRotation.eulerAngles + new Vector3(0f, 360f, 0f), sweepDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad))
                .Join(bossVisual.DOLocalMoveY(_startLocalPosition.y, sweepDuration).SetEase(Ease.OutQuad))
                .Join(bossVisual.DOScale(Vector3.Scale(_startLocalScale, wingsOpenScaleMul), sweepDuration).SetEase(Ease.OutBack))
                .SetLink(gameObject);
        }
        HitPlayer(center);
        yield return new WaitForSeconds(sweepDuration);

        // 경직: 날개를 편 채 멈춰 있어 플레이어가 반격할 틈이 된다.
        yield return new WaitForSeconds(recoveryDuration);

        _poseSequence?.Kill();
        ResetPose();
        _routine = null;
        onFinished?.Invoke();
    }

    private void HitPlayer(Vector3 center)
    {
        if (playerController == null || IsPlayerInWater()) return;

        Vector3 p = playerController.transform.position;
        Vector3 toPlayer = new Vector3(p.x - center.x, 0f, p.z - center.z);
        if (toPlayer.magnitude > sweepRadius) return;

        PlayerHealth.Instance?.TakeDamage(damage);
        Vector3 away = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
        playerController.SetBlast(away * knockbackSpeed + Vector3.up * knockbackUp);
    }

    private Vector3 GetBossFlat()
    {
        Vector3 p = transform.position;
        return new Vector3(p.x, 0f, p.z);
    }

    private bool IsPlayerInWater()
    {
        CharacterController cc = playerController.GetComponent<CharacterController>();
        float feetY = cc != null ? cc.bounds.min.y : playerController.transform.position.y;
        return feetY < fallY;
    }

    private void DestroyTelegraph()
    {
        if (_telegraph != null)
        {
            Destroy(_telegraph.gameObject);
            _telegraph = null;
        }
    }

    // 캐시해 둔 원래 값으로 정확히 되돌린다(다른 패턴의 기준 자세를 오염시키지 않기 위해).
    private void ResetPose()
    {
        if (bossVisual == null) return;
        bossVisual.localPosition = _startLocalPosition;
        bossVisual.localRotation = _startLocalRotation;
        bossVisual.localScale = _startLocalScale;
    }
}
