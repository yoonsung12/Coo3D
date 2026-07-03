using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// PlayerHealth의 체력 변화 이벤트를 구독해 하트 아이콘 여러 개로 체력을 표시한다.
// 하트 1개가 담당하는 체력은 (최대 체력 / 하트 개수)이며, 현재 체력이 그 구간을 넘었는지에 따라
// 각 하트의 스프라이트를 Heart_full/Heart_empty로 교체한다 (반 칸짜리 하트는 지원하지 않는다).
// [ExecuteAlways]로 Play Mode에 들어가지 않아도 에디터에서 바로 하트가 보이게 한다.
[ExecuteAlways]
public class PlayerHealthUI : MonoBehaviour
{
    // MaxHPItem 등 다른 시스템이 GameObject.Find 없이 하트 UI에 접근하기 위한 참조다.
    // 씬에 체력 UI가 하나뿐이라는 전제하에 자기 자신을 등록하는 방식으로만 제한적으로 사용한다.
    public static PlayerHealthUI Instance { get; private set; }

    [Title("연결")]
    [SerializeField, LabelText("Player Health")]
    private PlayerHealth playerHealth;
    // Inspector에서 Player의 PlayerHealth 컴포넌트를 연결한다.

    [SerializeField, LabelText("하트가 배치될 부모 (HorizontalLayoutGroup)")]
    private RectTransform heartContainer;
    // 하트 여러 개를 가로로 나열할 부모다. HorizontalLayoutGroup을 붙여 자동 정렬한다.

    [Title("하트 스프라이트")]
    [SerializeField, LabelText("가득 찬 하트")]
    private Sprite heartFullSprite;

    [SerializeField, LabelText("빈 하트")]
    private Sprite heartEmptySprite;

    [Title("하트 설정")]
    [SerializeField, LabelText("하트 개수")]
    private int heartCount = 5;
    // 체력을 하트 heartCount개로 나눠서 표시한다. 하트 1개 = 최대체력 / heartCount.

    [SerializeField, LabelText("하트 크기")]
    private Vector2 heartSize = new Vector2(64f, 64f);

    [Title("DOTween 연출 설정")]
    [SerializeField, LabelText("하트 사라짐/채워짐 펀치 강도")]
    private float punchScale = 0.35f;
    // 하트가 채워지거나 비워지는 순간 살짝 커졌다 돌아오는 강도다.

    [SerializeField, LabelText("펀치 지속 시간")]
    private float punchDuration = 0.25f;

    private Image[] _hearts;
    private Tween[] _heartTweens;
    private bool[] _wasFull;

    private float _lastCurrentHealth;
    private float _lastMaxHealth;
    // AddHeart()로 하트를 새로 만든 뒤, 마지막으로 받은 체력 값을 기준으로 다시 채움/빔 상태를 맞추기 위해 기억해 둔다.

    private void Awake()
    {
        Instance = this;
        BuildHearts();
    }

    // heartContainer 아래에 이미 있던 하트를 지우고, heartCount만큼 새로 생성한다.
    // 에디터에서 Awake가 다시 호출되거나(재컴파일 등) Play Mode에 들어갈 때 하트가 중복 생성되지 않도록
    // 항상 먼저 비운 뒤 다시 만든다.
    private void BuildHearts()
    {
        if (heartContainer == null || heartFullSprite == null) return;

        ClearHearts();

        _hearts = new Image[heartCount];
        _heartTweens = new Tween[heartCount];
        _wasFull = new bool[heartCount];

        for (int i = 0; i < heartCount; i++)
        {
            var heartGo = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            heartGo.transform.SetParent(heartContainer, false);

            var rect = heartGo.GetComponent<RectTransform>();
            rect.sizeDelta = heartSize;

            var img = heartGo.GetComponent<Image>();
            img.sprite = heartFullSprite;

            _hearts[i] = img;
            _wasFull[i] = true;
        }
    }

    // heartContainer 아래에 남아 있는 이전 하트 오브젝트를 모두 제거한다.
    // 에디터 모드에서는 Destroy가 다음 프레임까지 지연되므로 DestroyImmediate를 사용한다.
    private void ClearHearts()
    {
        for (int i = heartContainer.childCount - 1; i >= 0; i--)
        {
            var child = heartContainer.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_heartTweens == null) return;
        foreach (var tween in _heartTweens)
            tween?.Kill();
    }

    // 하트 개수를 1개 늘리고, 새로 만들어진 하트들도 마지막 체력 값 기준으로 다시 채움/빔 상태를 맞춘다.
    // MaxHPItem 등 최대 체력이 늘어나는 곳에서 호출한다.
    public void AddHeart()
    {
        heartCount++;
        BuildHearts();
        HandleHealthChanged(_lastCurrentHealth, _lastMaxHealth);
    }

    // 체력이 바뀔 때마다 PlayerHealth가 호출한다.
    // 각 하트가 담당하는 체력 구간을 현재 체력이 넘었는지에 따라 가득 찬/빈 스프라이트로 교체한다.
    private void HandleHealthChanged(float current, float max)
    {
        _lastCurrentHealth = current;
        _lastMaxHealth = max;

        if (_hearts == null || heartCount <= 0) return;

        float perHeart = max / heartCount;

        for (int i = 0; i < heartCount; i++)
        {
            float heartThreshold = perHeart * (i + 1);
            bool isFull = current >= heartThreshold - 0.001f;

            if (isFull == _wasFull[i]) continue;
            // 상태가 바뀐 하트만 스프라이트를 교체하고 펀치 연출을 재생한다.

            _wasFull[i] = isFull;
            _hearts[i].sprite = isFull ? heartFullSprite : heartEmptySprite;

            _heartTweens[i]?.Kill();
            _hearts[i].transform.localScale = Vector3.one;
            _heartTweens[i] = _hearts[i].transform.DOPunchScale(Vector3.one * punchScale, punchDuration, vibrato: 8, elasticity: 0.6f);
        }
    }
}
