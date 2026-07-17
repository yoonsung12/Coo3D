using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 봄 구역 첫 퍼즐 오브젝트다. 선풍기로 조준해 날린 꽃가루(Pollen)를 흡수해서 목표 개수를 채우면
// 개화 연출과 함께 아이템(기본: WoodenBox)을 하나 만들어낸다.
// PollenSpawner의 스폰 범위 밖에 배치해서, 자연 낙하로는 채워지지 않고 선풍기로 조준해야만 채워지게 한다.
[RequireComponent(typeof(Collider))]
public class FlowerBud : MonoBehaviour
{
    [Title("개화 조건")]
    [SerializeField, LabelText("필요한 꽃가루 개수")]
    private int requiredPollenCount = 5;
    // PollenSpawner의 한 번 우수수 개수(기본 8개)보다 적게 잡아, 우수수 한 번으로 채울 수 있지만
    // 다 못 맞히면 다음 우수수를 기다려야 하는 정도로 난이도를 맞춘다.

    [Title("개화 결과물")]
    [SerializeField, LabelText("개화 시 생성할 아이템 프리팹")]
    private GameObject bloomItemPrefab;
    // Inspector에서 WoodenBox 프리팹을 연결한다.

    [SerializeField, LabelText("아이템 생성 위치 오프셋")]
    private Vector3 itemSpawnOffset;
    // 자기 위치 기준 오프셋이다. 상자가 땅 위에 자연스럽게 놓이도록 조정한다.

    [Title("개화 연출 설정")]
    [SerializeField, LabelText("개화 연출 시간")]
    private float bloomDuration = 0.6f;

    [SerializeField, LabelText("개화 Ease")]
    private Ease bloomEase = Ease.OutBack;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("현재 흡수한 꽃가루 수")]
    private int _currentPollenCount;

    [ReadOnly, ShowInInspector, LabelText("개화 완료 여부")]
    private bool _isBloomed;

    private Vector3 _originalScale;
    private Tween _bloomTween;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        // 꽃가루를 감지만 하면 되는 흡수용 트리거라 물리 충돌은 필요 없다.

        _originalScale = transform.localScale;
    }

    private void OnDestroy()
    {
        _bloomTween?.Kill();
    }

    // 선풍기로 날아온 꽃가루가 이 범위에 들어오면 흡수한다.
    private void OnTriggerEnter(Collider other)
    {
        if (_isBloomed) return;

        Pollen pollen = other.GetComponent<Pollen>();
        if (pollen == null) return;

        Destroy(other.gameObject);
        _currentPollenCount++;

        if (_currentPollenCount >= requiredPollenCount)
            Bloom();
    }

    private void Bloom()
    {
        _isBloomed = true;

        _bloomTween?.Kill();
        _bloomTween = transform
            .DOScale(_originalScale * 1.15f, bloomDuration)
            .SetEase(bloomEase)
            .OnComplete(SpawnBloomItem);
    }

    private void SpawnBloomItem()
    {
        if (bloomItemPrefab == null) return;
        Instantiate(bloomItemPrefab, transform.position + itemSpawnOffset, Quaternion.identity);
    }

    [Button("강제 개화 테스트")]
    private void TestBloom()
    {
        if (!_isBloomed) Bloom();
        // Play Mode에서 꽃가루를 직접 모으지 않고도 개화 연출과 아이템 생성만 빠르게 확인하기 위한 버튼이다.
    }
}
