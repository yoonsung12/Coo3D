using System;
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹" 섬 아레나 평범한 공격 2: 연속 쪼기.
// 준비 동작 -> 직선 예고 -> 두 번 연달아 쪼기(두 번째가 더 멀리 닿는다) -> 고개를 숙인 채 경직.
// 방향은 예고 시작 때 고정되므로 플레이어가 옆으로 피할 수 있다.
public class BossIslandDoublePeck : MonoBehaviour
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("Player Controller")]
    private PlayerController playerController;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("레인 예고 프리팹")]
    private GroundTelegraph laneTelegraphPrefab;
    // 원 예고와 같은 GroundTelegraph 프리팹을 쓴다(ShowLane으로 직사각형을 그린다).

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("섬 판정용 스케줄러 (비우면 같은 오브젝트)")]
    private BossIslandBasicPattern scheduler;
    // 전진할 때 섬 밖으로 나가지 않는지 확인하는 데 쓴다.

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("준비 시간")]
    private float windupDuration = 0.5f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("예고 시간")]
    private float telegraphDuration = 0.4f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("쪼기 사이 간격")]
    private float peckInterval = 0.2f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("경직(회복) 시간")]
    private float recoveryDuration = 0.8f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("Ease")]
    private Ease poseEase = Ease.OutQuad;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("폭")]
    private float peckWidth = 1.5f;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("첫 번째 쪼기 길이")]
    private float firstLength = 3f;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("두 번째 쪼기 추가 길이")]
    private float extraLength = 1f;
    // 두 번째 쪼기는 첫 번째보다 이만큼 더 멀리 닿는다.

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("쪼기당 전진 거리")]
    private float peckStep = 0.5f;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("쪼기당 피해량")]
    private float damage = 10f;

    [TabGroup("설정", "판정")]
    [SerializeField, LabelText("낙하 판정 Y")]
    private float fallY = -0.5f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("준비 자세 뒤로 젖히는 각도 (X)")]
    private float windupTiltX = -25f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("쪼기 숙이는 각도 (X)")]
    private float peckTiltX = 35f;

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("쪼기 시 앞으로 내미는 거리")]
    private float jabForward = 0.5f;

    [Title("사운드")]
    [SerializeField, LabelText("AudioSource (비우면 같은 오브젝트)")]
    private AudioSource audioSource;
    // 보스에 붙은 효과음(SFX 믹서) AudioSource를 같이 쓴다. 비워두면 Awake에서 같은 오브젝트에서 찾는다.

    [SerializeField, LabelText("쪼기 소리 (선택)")]
    private AudioClip peckClip;
    // 쪼기 한 번마다 재생된다(두 번 쪼므로 두 번 난다).

    [SerializeField, LabelText("쪼기 소리 볼륨"), Range(0f, 1f)]
    private float peckVolume = 0.8f;

    [SerializeField, LabelText("두 번째 쪼기 볼륨 배율")]
    private float secondPeckVolumeMul = 1.25f;
    // 두 번째 쪼기가 더 멀리 닿는 강한 공격이라 소리도 조금 더 크게 낸다. 1이면 같은 크기다.

    private Coroutine _routine;
    private Sequence _poseSequence;
    private GroundTelegraph _telegraph;
    private Rigidbody _rb;
    private Vector3 _startLocalPosition;
    private Quaternion _startLocalRotation;
    private Vector3 _startLocalScale;
    private BossAnimationController bossAnim;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (scheduler == null) scheduler = GetComponent<BossIslandBasicPattern>();

        if (bossVisual != null)
        {
            _startLocalPosition = bossVisual.localPosition;
            _startLocalRotation = bossVisual.localRotation;
            _startLocalScale = bossVisual.localScale;
        }
        bossAnim = GetComponent<BossAnimationController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    // 소리가 연결돼 있을 때만 재생한다. PlayOneShot 볼륨은 1을 넘으면 잘리므로 1로 제한한다.
    private void PlaySfx(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, Mathf.Min(volume, 1f));
    }

    private void OnDisable()
    {
        Abort();
    }

    private void OnDestroy()
    {
        _poseSequence?.Kill();
    }

    public void Execute(Vector3 flatDirection, Action onFinished)
    {
        Abort();
        _routine = StartCoroutine(PeckRoutine(flatDirection, onFinished));
    }

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

    private IEnumerator PeckRoutine(Vector3 flatDirection, Action onFinished)
    {
        // 방향은 여기서 고정한다. 이후 플레이어가 움직여도 쪼는 방향은 바뀌지 않는다.
        Vector3 dir = new Vector3(flatDirection.x, 0f, flatDirection.z);
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // 준비: 고개를 뒤로 젖힌다.
        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOLocalRotate(_startLocalRotation.eulerAngles + new Vector3(windupTiltX, 0f, 0f), windupDuration).SetEase(poseEase))
                .SetLink(gameObject);
        }
        yield return new WaitForSeconds(windupDuration);

        // 예고: 두 번째 쪼기가 전진한 만큼까지 덮는 하나의 레인을 보여준다.
        Vector3 origin = GetBossFlat();
        if (laneTelegraphPrefab != null)
        {
            _telegraph = Instantiate(laneTelegraphPrefab);
            _telegraph.ShowLane(origin, origin + dir * (firstLength + extraLength + peckStep), peckWidth);
        }
        yield return new WaitForSeconds(telegraphDuration * 0.7f);
        if (_telegraph != null) _telegraph.Lock();
        yield return new WaitForSeconds(telegraphDuration * 0.3f);

        // 쪼기 두 번. 각 쪼기는 한 번씩만 맞는다.
        bossAnim?.PlayPeck();
        PlaySfx(peckClip, peckVolume);
        yield return PeckOnce(dir, firstLength);
        yield return new WaitForSeconds(peckInterval);
        PlaySfx(peckClip, peckVolume * secondPeckVolumeMul);
        yield return PeckOnce(dir, firstLength + extraLength);

        DestroyTelegraph();

        // 경직: 고개를 숙인 채 멈춘다.
        yield return new WaitForSeconds(recoveryDuration);

        _poseSequence?.Kill();
        ResetPose();
        _routine = null;
        onFinished?.Invoke();
    }

    private IEnumerator PeckOnce(Vector3 dir, float length)
    {
        // 시각 연출: 고개를 숙이며 앞으로 내민다.
        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            float t = peckInterval * 0.5f;
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOLocalRotate(_startLocalRotation.eulerAngles + new Vector3(peckTiltX, 0f, 0f), t).SetEase(Ease.OutQuad))
                .Join(bossVisual.DOLocalMoveZ(_startLocalPosition.z + jabForward, t).SetEase(Ease.OutQuad))
                .SetLink(gameObject);
        }

        // 루트 전진: 섬 안일 때만 물리 API(MovePosition)로 움직여 충돌을 유지한다.
        Vector3 nextFlat = GetBossFlat() + dir * peckStep;
        if (_rb != null && !_rb.isKinematic && (scheduler == null || scheduler.IsInsideWalkArea(nextFlat)))
            _rb.MovePosition(_rb.position + dir * peckStep);

        HitCheck(dir, length);
        yield return null;
    }

    // 보스 기준 앞쪽(dir) 길이 length, 폭 peckWidth의 직사각형 안에 플레이어가 있으면 피해를 준다.
    private void HitCheck(Vector3 dir, float length)
    {
        if (playerController == null || IsPlayerInWater()) return;

        Vector3 rel = playerController.transform.position - transform.position;
        rel.y = 0f;
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        float along = Vector3.Dot(rel, dir);
        float side = Vector3.Dot(rel, right);

        if (along < 0f || along > length || Mathf.Abs(side) > peckWidth * 0.5f) return;

        PlayerHealth.Instance?.TakeDamage(damage);
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

    private void ResetPose()
    {
        if (bossVisual == null) return;
        bossVisual.localPosition = _startLocalPosition;
        bossVisual.localRotation = _startLocalRotation;
        bossVisual.localScale = _startLocalScale;
    }
}
