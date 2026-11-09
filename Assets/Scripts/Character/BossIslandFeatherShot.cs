using System;
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 "비둘킹" 섬 아레나 평범한 공격 3: 깃털 3갈래.
// 준비 동작 -> 은은한 직선 예고 3줄 -> 깃털 3발 발사 -> 짧은 경직.
// 깃털은 피해만 주고 기절은 없다. 바위에 막히며, 사거리를 넘으면 사라진다.
public class BossIslandFeatherShot : MonoBehaviour
{
    [FoldoutGroup("연결")]
    [SerializeField, LabelText("보스 시각 오브젝트")]
    private Transform bossVisual;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("레인 예고 프리팹")]
    private GroundTelegraph laneTelegraphPrefab;

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("깃털 프리팹")]
    private BossProjectile projectilePrefab;
    // 사이드뷰 아레나와 공유하는 프리팹이므로 수정하지 않고 그대로 쓴다.

    [FoldoutGroup("연결")]
    [SerializeField, LabelText("발사 위치 (높이 기준)")]
    private Transform projectileSpawnPoint;
    // 높이(Y)만 가져다 쓴다. 수평 위치는 보스 앞쪽으로 직접 계산한다(방향마다 달라서).

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("준비 시간")]
    private float windupDuration = 0.6f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("예고 시간")]
    private float telegraphDuration = 0.5f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("경직(회복) 시간")]
    private float recoveryDuration = 0.6f;

    [TabGroup("설정", "타이밍")]
    [SerializeField, LabelText("Ease")]
    private Ease poseEase = Ease.OutQuad;

    [TabGroup("설정", "발사")]
    [SerializeField, LabelText("갈래 각도")]
    private float spreadAngle = 25f;
    // 가운데 기준 좌우로 벌어지는 각도다.

    [TabGroup("설정", "발사")]
    [SerializeField, LabelText("사거리")]
    private float range = 16f;

    [TabGroup("설정", "발사")]
    [SerializeField, LabelText("깃털 속도")]
    private float projectileSpeed = 12f;

    [TabGroup("설정", "발사")]
    [SerializeField, LabelText("피해량")]
    private float damage = 10f;

    [TabGroup("설정", "발사")]
    [SerializeField, LabelText("보스 앞 발사 거리")]
    private float spawnForward = 1.6f;
    // 보스 자신의 콜라이더에 겹쳐 생성되지 않도록 앞으로 띄운다.

    [TabGroup("설정", "예고")]
    [SerializeField, LabelText("예고 폭")]
    private float laneWidth = 0.6f;

    [TabGroup("설정", "예고")]
    [SerializeField, LabelText("은은한 예고 색")]
    private Color laneColor = new Color(1f, 0.3f, 0.2f, 0.25f);

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("준비 자세 키우는 배율")]
    private Vector3 windupScaleMul = new Vector3(1f, 1.2f, 1f);

    [TabGroup("설정", "연출")]
    [SerializeField, LabelText("준비 자세 젖힘 각도 (X)")]
    private float windupTiltX = -15f;

    private Coroutine _routine;
    private Sequence _poseSequence;
    private readonly GroundTelegraph[] _lanes = new GroundTelegraph[3];
    private Collider[] _bossColliders;
    private Vector3 _startLocalPosition;
    private Quaternion _startLocalRotation;
    private Vector3 _startLocalScale;

    private void Awake()
    {
        _bossColliders = GetComponentsInChildren<Collider>();

        if (bossVisual != null)
        {
            _startLocalPosition = bossVisual.localPosition;
            _startLocalRotation = bossVisual.localRotation;
            _startLocalScale = bossVisual.localScale;
        }
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
        _routine = StartCoroutine(ShotRoutine(flatDirection, onFinished));
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
        DestroyLanes();
        ResetPose();
    }

    private IEnumerator ShotRoutine(Vector3 flatDirection, Action onFinished)
    {
        // 방향은 예고 시작 때 고정한다.
        Vector3 dir = new Vector3(flatDirection.x, 0f, flatDirection.z);
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // 준비: 몸을 키우고 뒤로 젖혀 깃털을 세우는 느낌을 준다.
        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOScale(Vector3.Scale(_startLocalScale, windupScaleMul), windupDuration).SetEase(poseEase))
                .Join(bossVisual.DOLocalRotate(_startLocalRotation.eulerAngles + new Vector3(windupTiltX, 0f, 0f), windupDuration).SetEase(poseEase))
                .SetLink(gameObject);
        }
        yield return new WaitForSeconds(windupDuration);

        // 예고: 3갈래 직선을 은은하게 보여준다.
        Vector3 origin = new Vector3(transform.position.x, 0f, transform.position.z);
        float[] angles = { -spreadAngle, 0f, spreadAngle };
        if (laneTelegraphPrefab != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = Quaternion.Euler(0f, angles[i], 0f) * dir;
                _lanes[i] = Instantiate(laneTelegraphPrefab);
                _lanes[i].SetWarningColor(laneColor);
                _lanes[i].ShowLane(origin, origin + d * range, laneWidth);
            }
        }
        yield return new WaitForSeconds(telegraphDuration);

        // 발사: 예고를 거두고 깃털 3발을 쏜다.
        DestroyLanes();
        Fire(dir, angles);

        if (bossVisual != null)
        {
            _poseSequence?.Kill();
            _poseSequence = DOTween.Sequence()
                .Append(bossVisual.DOPunchScale(Vector3.one * 0.15f, recoveryDuration * 0.5f, 6, 0.6f))
                .SetLink(gameObject);
        }
        yield return new WaitForSeconds(recoveryDuration);

        _poseSequence?.Kill();
        ResetPose();
        _routine = null;
        onFinished?.Invoke();
    }

    private void Fire(Vector3 dir, float[] angles)
    {
        if (projectilePrefab == null) return;

        float y = projectileSpawnPoint != null ? projectileSpawnPoint.position.y : transform.position.y;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 d = Quaternion.Euler(0f, angles[i], 0f) * dir;
            Vector3 pos = new Vector3(transform.position.x, y, transform.position.z) + d * spawnForward;

            BossProjectile p = Instantiate(projectilePrefab, pos, Quaternion.LookRotation(d, Vector3.up));
            p.Launch(d, projectileSpeed, damage);
            Destroy(p.gameObject, range / projectileSpeed); // 사거리에 도달하면 사라지게 한다.

            // 프리팹의 타격/장애물 레이어가 Default라 보스 자신에게 맞고 사라지는 일을 막는다.
            Collider pc = p.GetComponent<Collider>();
            foreach (Collider bc in _bossColliders)
            {
                if (bc != null && pc != null) Physics.IgnoreCollision(pc, bc);
            }
        }
    }

    private void DestroyLanes()
    {
        for (int i = 0; i < _lanes.Length; i++)
        {
            if (_lanes[i] != null) Destroy(_lanes[i].gameObject);
            _lanes[i] = null;
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
