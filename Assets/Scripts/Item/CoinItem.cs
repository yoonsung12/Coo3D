using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 먹으면 누적 점수와 자판기용 코인을 함께 지급하는 아이템이다.
public class CoinItem : BasePickupItem
{
    [Title("보상")]
    [SerializeField, LabelText("지급 점수")]
    private int scoreAmount = 10;

    [SerializeField, LabelText("지급 코인")]
    private int coinAmount = 1;

    [Title("사운드")]
    [SerializeField, LabelText("먹을 때 소리")]
    private AudioClip pickupClip;
    // "Casual & Mobile Music and Sounds Pack - Vol 1/SFX/Coin 3-1.wav"를 연결한다. 비워 두면 소리 없이 코인만 오른다.

    [SerializeField, LabelText("소리 볼륨"), Range(0f, 1f)]
    private float pickupVolume = 1f;
    // 코인을 연달아 먹으면 소리가 겹쳐 커지므로, 너무 크면 이 값을 낮춘다.

    [Title("저장")]
    [SerializeField, LabelText("먹은 기록 저장 (씬 배치 코인만)")]
    private bool persistCollected;
    // 맵에 직접 배치한 코인만 켠다. 켜면 먹은 기록이 세이브에 남아, 체크포인트 저장 뒤 다시 불러와도 코인이 다시 생기지 않는다.
    // 적이 떨어뜨리는 코인(프리팹 원본)은 반드시 끈 채로 둔다. 실행 중에 생긴 복제본은 이름이 모두
    // "CoinItem(Clone)"으로 같아서, 켜 두면 드랍 코인 하나를 먹는 순간 이후 드랍 코인이 전부 사라진다.
    // ID가 "씬/부모경로/이름"이므로, 출시 뒤에는 배치 코인의 이름이나 부모를 바꾸지 않는다.

    [Title("회전 연출")]
    [SerializeField, LabelText("한 바퀴 도는 시간")]
    private float spinDuration = 1.5f;
    // 코인이 Y축으로 계속 제자리 회전하면서 입체감 있게 보이도록 만든다.
    // 값이 작을수록 더 빠르게 돈다.

    private Tween _spinTween;

    // base.Start()를 반드시 호출해야 BasePickupItem의 스폰 팝인 연출/자동 소멸 타이머가 정상 동작한다.
    // override 없이 그냥 Start()만 새로 선언하면 Unity가 부모 쪽 Start()를 아예 호출하지 않아
    // 코인이 크기 0에서 벗어나지 못해 화면에 보이지 않는 문제가 생긴다.
    protected override void Start()
    {
        // 저장 이전에 이미 먹은 배치 코인이면 팝인 연출을 시작하기 전에 바로 없앤다.
        // Enemy가 처치 기록을 보고 시작할 때 스스로 꺼지는 것과 같은 방식이다.
        if (persistCollected && SaveManager.Instance != null &&
            SaveManager.Instance.IsCoinCollected(SaveManager.GetObjectId(gameObject)))
        {
            Destroy(gameObject);
            return;
        }

        base.Start();

        // DOTween으로 Y축 기준 360도 회전을 무한 반복시켜 코인이 빙글빙글 도는 느낌을 낸다.
        _spinTween = transform
            .DORotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _spinTween?.Kill();
        // 오브젝트가 파괴될 때 남아 있는 Tween을 정리해 오류를 방지한다.
    }

    protected override void ApplyEffect()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreAmount);

        if (CoinManager.Instance != null)
            CoinManager.Instance.AddCoin(coinAmount);

        // 코인 오브젝트는 먹자마자 파괴되므로 SfxPlayer로 따로 재생해야 소리가 끝까지 들리고, 효과음 볼륨 설정도 적용된다.
        SfxPlayer.PlayAt(pickupClip, transform.position, pickupVolume);

        // 배치 코인은 먹은 기록을 남긴다. 파일에는 다음 체크포인트 저장 때 코인 개수와 함께 기록된다.
        if (persistCollected && SaveManager.Instance != null)
            SaveManager.Instance.MarkCoinCollected(SaveManager.GetObjectId(gameObject));
    }
}
