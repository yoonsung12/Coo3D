using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 발판 하단에 매달린 겨울 패턴 파훼용 고드름이다. 검(SwordHitbox)에 맞으면 흔들린 뒤 떨어지고,
// 떨어진 자리에 보스가 있으면(겨울 패턴으로 유인되어 정지한 상태) 데미지를 주고 패턴을 파훼시킨다.
// BossWinterPattern이 겨울 패턴을 새로 시작하거나 실패 후 재도전을 준비할 때 ResetIcicle()로
// 원래 상태로 되돌린다.
// SwordHitbox와의 트리거 감지가 성립하려면(Unity 트리거 규칙상 최소 한쪽에 Rigidbody 필요)
// Rigidbody가 있어야 한다 — 물리 낙하가 아니라 순수 연출용 이동(DOTween)이라 Kinematic으로 둔다.
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class Icicle : MonoBehaviour, IHittable
{
    [Title("연결")]
    [SerializeField, LabelText("보스")]
    private Boss boss;
    // Inspector에서 씬의 Boss 오브젝트를 연결한다. 낙하 후 보스가 근처에 있는지 판정하는 데 쓴다.

    [Title("타격 설정")]
    [SerializeField, LabelText("낙하에 필요한 타격 횟수")]
    private int requiredHits = 1;
    // 1보다 크게 설정하면 여러 번 쳐야 떨어지도록 난이도를 조절할 수 있다.

    [Title("낙하 연출 설정")]
    [SerializeField, LabelText("타격 시 흔들림 세기")]
    private float shakeStrength = 0.15f;

    [SerializeField, LabelText("타격 시 흔들림 시간")]
    private float shakeDuration = 0.15f;

    [SerializeField, LabelText("낙하 거리")]
    private float fallDistance = 3f;
    // 발판 하단에서 지면(보스가 서 있는 높이)까지 떨어지는 거리다.

    [SerializeField, LabelText("낙하 시간")]
    private float fallDuration = 0.35f;

    [SerializeField, LabelText("낙하 Ease")]
    private Ease fallEase = Ease.InQuad;
    // 중력처럼 갈수록 빨라지는 느낌을 주기 위한 기본값이다.

    [Title("파훼 판정 설정")]
    [SerializeField, LabelText("명중 판정 반경")]
    private float hitCheckRadius = 1.5f;
    // 낙하 지점과 보스 사이 거리가 이 값 이하면 명중으로 처리한다.

    [Title("생성 연출 설정")]
    [SerializeField, LabelText("생성 시작 크기 배율")]
    private float spawnStartScaleRatio = 0.15f;
    // 겨울 패턴 착지("쾅" + 냉기 파동) 순간, 이 배율의 작은 크기에서 시작해 원래 크기로
    // 자라나며 나타난다. 값이 작을수록 "갑자기 얼어붙는" 느낌이 강해진다.

    [SerializeField, LabelText("생성 연출 시간")]
    private float spawnDuration = 0.35f;

    [SerializeField, LabelText("생성 연출 Ease")]
    private Ease spawnEase = Ease.OutBack;
    // OutBack은 커지다가 살짝 튕기는 느낌을 줘서 "탁" 하고 얼음이 맺히는 연출에 어울린다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 타격 수")]
    private int _hitCount;

    [ReadOnly, ShowInInspector, LabelText("낙하함")]
    private bool _hasFallen;

    private Collider _collider;
    private Rigidbody _rb;
    private Renderer _renderer;
    private Vector3 _originalPosition;
    private Vector3 _originalScale;
    private Tween _tween;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _rb = GetComponent<Rigidbody>();
        _renderer = GetComponentInChildren<Renderer>();
        // 실제 3D 모델(Stylized_Ice_Spike)은 회전 보정을 위해 자식 오브젝트(Visual)에 있어서
        // 자식까지 포함해 찾는다.
        _rb.isKinematic = true; // 물리 낙하가 아니라 DOTween 연출로만 움직이므로 물리 영향을 받지 않게 한다.
        _originalPosition = transform.position;
        _originalScale = transform.localScale;

        // 겨울 패턴이 발동해 보스가 "쾅" 착지하기 전까지는 아직 얼지 않은 상태이므로 숨겨둔다.
        ResetIcicle(hide: true);
    }

    private void OnDestroy()
    {
        _tween?.Kill();
    }

    // SwordHitbox가 검 스윙 중 이 오브젝트와 부딪히면 호출된다.
    public void OnHit()
    {
        if (_hasFallen) return;

        _hitCount++;
        if (_hitCount >= requiredHits)
            Fall();
        else
            PlayShake();
    }

    private void PlayShake()
    {
        _tween?.Kill();
        _tween = transform.DOShakePosition(shakeDuration, shakeStrength);
    }

    private void Fall()
    {
        _hasFallen = true;
        _collider.enabled = false; // 낙하 중에는 추가 타격이나 다른 충돌 판정에 걸리지 않게 한다.

        _tween?.Kill();
        Vector3 target = transform.position + Vector3.down * fallDistance;
        _tween = transform.DOMove(target, fallDuration)
            .SetEase(fallEase)
            .OnComplete(HandleLanded);
    }

    // 낙하가 끝난 지점에 보스가 있으면(겨울 패턴으로 유인되어 정지한 상태) 패턴을 파훼시킨다.
    // 데미지는 주지 않는다 — 파훼 직후 Boss.cs의 무방비 시간 동안 일반 공격으로 때려야 실제 피해가 들어간다.
    private void HandleLanded()
    {
        if (boss != null && Vector3.Distance(transform.position, boss.transform.position) <= hitCheckRadius)
        {
            boss.NotifyPatternSolved();
        }
    }

    // BossWinterPattern이 겨울 패턴을 새로 시작하거나, 실패해서 재도전을 준비할 때 호출한다.
    // hide가 true면(겨울 패턴 시작 전/종료 시) 얼기 전 상태로 완전히 숨기고, false면(온기존이 꺼져
    // 재도전 준비할 때) 이미 생성된 상태 그대로 위치만 원위치로 되돌린다.
    public void ResetIcicle(bool hide = false)
    {
        _tween?.Kill();
        _hitCount = 0;
        _hasFallen = false;
        transform.position = _originalPosition;
        transform.localScale = _originalScale;

        if (hide)
        {
            if (_renderer != null) _renderer.enabled = false;
            _collider.enabled = false;
        }
        else
        {
            if (_renderer != null) _renderer.enabled = true;
            _collider.enabled = true;
        }
    }

    // BossWinterPattern이 인트로의 "쾅" 착지+냉기 파동 순간에 호출해, 발판 하단에 얼음이
    // 맺히듯 작은 크기에서 원래 크기로 자라나며 나타나게 한다.
    public void Spawn()
    {
        _tween?.Kill();
        if (_renderer != null) _renderer.enabled = true;
        _collider.enabled = true;

        transform.localScale = _originalScale * spawnStartScaleRatio;
        _tween = transform.DOScale(_originalScale, spawnDuration).SetEase(spawnEase);
    }

    [Button("즉시 낙하 테스트")]
    private void TestFall() => Fall();

    [Button("생성 연출 테스트")]
    private void TestSpawn() => Spawn();

    [Button("초기화 테스트")]
    private void TestReset() => ResetIcicle();
}
