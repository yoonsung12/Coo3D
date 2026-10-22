using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 가을 퍼즐용 흔들리는 은행나무다. 줄기 판정용 트리거 콜라이더와 같은 오브젝트에 붙인다.
// 배게검(SwordHitbox)이 그 콜라이더에서 IHittable을 찾아 OnHit()를 호출하므로,
// 휘두를 때마다 나무가 흔들리고 수관에서 은행열매가 떨어진다(우산으로 막는 로직은 GinkgoFruit에 이미 있다).
// 정해진 횟수만큼 맞으면 가지에 걸려 있던 나무 상자가 떨어진다.
[RequireComponent(typeof(Collider))]
public class ShakeableTree : MonoBehaviour, IHittable
{
    [Title("나무 모델")]
    [SerializeField, LabelText("흔들 대상(나무 모델)")]
    private Transform treeVisual;
    // 흔들리는 연출을 줄 나무 모델이다. 이 컴포넌트가 붙은 판정용 오브젝트를 흔들면
    // 콜라이더까지 같이 움직이므로, 모델 Transform만 따로 연결한다.

    [Title("타격 설정")]
    [SerializeField, LabelText("필요한 타격 수")]
    private int requiredHits = 3;
    // 이 횟수째 타격에 상자가 떨어진다. 값이 클수록 더 많이 쳐야 한다.

    [SerializeField, LabelText("타격 쿨타임(초)")]
    private float hitCooldown = 0.6f;
    // 쿨타임 중의 OnHit은 무시한다. 한 번 휘두를 때 연속 판정되거나 연타하는 것을 막는다.

    [Title("은행열매 설정")]
    [SerializeField, LabelText("은행열매 프리팹")]
    private GameObject fruitPrefab;
    // GinkgoFruit 컴포넌트가 붙은 프리팹을 연결한다.

    [SerializeField, LabelText("타격당 은행 개수")]
    private int fruitsPerHit = 4;

    [SerializeField, LabelText("은행 생성 간격(초)")]
    private float fruitSpawnDelay = 0.12f;
    // 한 번 맞을 때 은행이 한꺼번에 나오지 않고 이 간격으로 하나씩 우수수 떨어진다.

    [SerializeField, LabelText("은행 생성 중심(수관)")]
    private Transform fruitSpawnCenter;
    // 수관 한가운데쯤에 빈 오브젝트를 두고 연결한다. 없으면 이 오브젝트 위치를 쓴다.

    [SerializeField, LabelText("은행 생성 범위(절반 크기)")]
    private Vector3 fruitSpawnHalfSize = new Vector3(2.2f, 0.4f, 0.3f);
    // 중심에서 각 축 방향으로 이 값만큼 떨어진 박스 안에서 랜덤 위치에 생성된다.
    // Scene 뷰에서 오브젝트를 선택하면 노란 박스로 범위가 보인다.

    [SerializeField, LabelText("착지 후 소멸 시간(초)")]
    private float fruitLandedLifetime = 4f;
    // GinkgoFruit.SetLandedLifetime에 전달한다. 0이면 착지 후 영구히 남는다.

    [Title("나무 상자")]
    [SerializeField, LabelText("걸려 있는 상자")]
    private Rigidbody heldBox;
    // 가지에 걸려 있는 나무 상자. 시작할 때 kinematic으로 고정했다가, 필요한 타격 수를 채우면 푼다.

    [Title("흔들림 연출 (DOTween)")]
    [SerializeField, LabelText("흔들림 시간(초)")]
    private float shakeDuration = 0.5f;

    [SerializeField, LabelText("흔들림 세기(도)")]
    private Vector3 shakeStrength = new Vector3(4f, 0f, 4f);
    // 각 축으로 흔들리는 최대 회전 각도다. 사이드뷰라 앞뒤(X)/좌우 기울임(Z) 위주로 흔든다.

    [SerializeField, LabelText("진동 횟수")]
    private int shakeVibrato = 10;
    // 값이 클수록 더 잘게 떨린다.

    [SerializeField, LabelText("마지막 타격 세기 배수")]
    private float finalShakeMultiplier = 1.6f;
    // 상자가 떨어지는 마지막 타격은 일반 타격보다 크게 흔들어 "툭 떨어질 만큼 세게 쳤다"는 느낌을 준다.

    [Title("사운드 (선택)")]
    [SerializeField, LabelText("타격 사운드")]
    private AudioClip hitSound;
    // 비워두면 소리 없이 동작한다.

    [Title("런타임 상태")]
    [ShowInInspector, ReadOnly, LabelText("현재 타격 수")]
    private int _hitCount;

    [ShowInInspector, ReadOnly, LabelText("상자 떨어짐 여부")]
    private bool _isCleared;

    // 상자가 풀렸을 때 호출된다. 나중에 문/퍼즐 연동 등에 연결하기 위한 최소한의 이벤트다.
    public event System.Action BoxReleased;

    private Quaternion _originalLocalRotation;
    private Tween _shakeTween;
    private float _lastHitTime = float.NegativeInfinity;
    // 마지막으로 타격이 인정된 시각. 쿨타임 계산에 쓴다.

    private void Awake()
    {
        // 씬에서 Kinematic이 꺼져 있어도 상자가 미리 떨어지지 않도록 확실히 고정한다.
        if (heldBox != null)
            heldBox.isKinematic = true;

        // 흔들기 전/후에 항상 같은 자세로 돌아오기 위해 원래 회전값을 기억한다.
        if (treeVisual != null)
            _originalLocalRotation = treeVisual.localRotation;
    }

    private void OnDestroy()
    {
        // 코루틴은 오브젝트가 파괴되면 자동으로 멈추므로 Tween만 정리한다.
        _shakeTween?.Kill();
    }

    // 검에 맞았을 때 SwordHitbox가 호출한다.
    public void OnHit()
    {
        if (Time.time - _lastHitTime < hitCooldown) return;
        _lastHitTime = Time.time;

        // 이번 타격이 상자를 떨어뜨리는 마지막 타격인지 미리 계산해서 흔들림 세기에 반영한다.
        bool isFinalHit = !_isCleared && _hitCount + 1 >= requiredHits;
        Shake(isFinalHit);

        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position);

        // 상자가 이미 떨어진 뒤에는 흔들리기만 하고 은행은 더 떨어지지 않는다.
        if (_isCleared) return;

        _hitCount++;
        StartCoroutine(SpawnFruitsRoutine());

        if (_hitCount >= requiredHits)
            ReleaseBox();
    }

    // 나무 모델을 DOTween으로 흔든다. 연타 시 이전 흔들림이 남아 자세가 틀어지지 않도록 먼저 초기화한다.
    private void Shake(bool isFinalHit)
    {
        if (treeVisual == null) return;

        _shakeTween?.Kill();
        treeVisual.localRotation = _originalLocalRotation;

        Vector3 strength = isFinalHit ? shakeStrength * finalShakeMultiplier : shakeStrength;
        _shakeTween = treeVisual
            .DOShakeRotation(shakeDuration, strength, shakeVibrato)
            .OnComplete(() => treeVisual.localRotation = _originalLocalRotation);
    }

    // 은행열매를 간격을 두고 하나씩 생성한다.
    private IEnumerator SpawnFruitsRoutine()
    {
        if (fruitPrefab == null) yield break;

        WaitForSeconds wait = new WaitForSeconds(fruitSpawnDelay);
        for (int i = 0; i < fruitsPerHit; i++)
        {
            SpawnFruit();
            yield return wait;
        }
    }

    // 생성 범위 박스 안의 랜덤 위치에 은행 하나를 만든다.
    private void SpawnFruit()
    {
        Vector3 center = fruitSpawnCenter != null ? fruitSpawnCenter.position : transform.position;
        Vector3 spawnPos = center + new Vector3(
            Random.Range(-fruitSpawnHalfSize.x, fruitSpawnHalfSize.x),
            Random.Range(-fruitSpawnHalfSize.y, fruitSpawnHalfSize.y),
            Random.Range(-fruitSpawnHalfSize.z, fruitSpawnHalfSize.z));

        GameObject fruit = Instantiate(fruitPrefab, spawnPos, Quaternion.identity);

        if (fruit.TryGetComponent(out GinkgoFruit ginkgoFruit))
            ginkgoFruit.SetLandedLifetime(fruitLandedLifetime);
    }

    // 걸려 있던 상자의 고정을 풀어 중력으로 떨어지게 한다.
    private void ReleaseBox()
    {
        _isCleared = true;

        if (heldBox != null)
        {
            heldBox.isKinematic = false;
            heldBox.WakeUp(); // 잠들어 있으면 풀어도 안 움직일 수 있어 깨워 준다.
        }

        BoxReleased?.Invoke();
    }

    [Button("타격 테스트")]
    private void TestHit()
    {
        // Play 모드에서만 의미가 있다(Tween/코루틴/물리 사용).
        OnHit();
    }

    [Button("상자 떨어뜨리기 테스트")]
    private void TestReleaseBox()
    {
        // Play 모드에서만 의미가 있다.
        ReleaseBox();
    }

    // 은행 생성 범위를 Scene 뷰에 노란 박스로 표시한다.
    private void OnDrawGizmosSelected()
    {
        Vector3 center = fruitSpawnCenter != null ? fruitSpawnCenter.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, fruitSpawnHalfSize * 2f);
    }
}
