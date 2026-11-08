using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 최종보스 가을 패턴("되튕기는 발판")에서 땅을 뚫고 솟았다가 바로 가라앉는 뿌리 하나다.
// 충돌체가 없는 연출용 오브젝트이며, 피해 판정은 패턴(BossAutumnIslandPattern)이 IsUp을 보고 직접 한다.
// 판정에 "이번 회차에 이미 맞았는지" 같은 패턴 상태가 필요하므로 판정 로직을 여기에 두지 않았다.
public class AutumnRoot : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("보이는 부분")]
    private Transform visual;
    // 뿌리 메시를 담은 자식이다. 이 오브젝트를 위아래로 움직여 솟고 가라앉는 연출을 만든다.

    [Title("DOTween 설정")]
    [SerializeField, LabelText("땅속 깊이(Y)")]
    private float hiddenLocalY = -1.8f;
    // 값이 작을수록 더 깊이 숨어 있다. 뿌리 높이보다 크게 잡아야 시작할 때 보이지 않는다.

    [SerializeField, LabelText("솟는 시간")]
    private float riseDuration = 0.1f;

    [SerializeField, LabelText("가라앉는 시간")]
    private float sinkDuration = 0.15f;

    // 솟아서 지면 위에 나와 있는 동안만 true다. 패턴이 피해 판정에 쓴다.
    public bool IsUp { get; private set; }

    private Sequence _sequence;
    // 중간에 파괴돼도 Tween이 남지 않도록 저장해 둔다.

    private void Awake()
    {
        if (visual != null) visual.localPosition = new Vector3(0f, hiddenLocalY, 0f);
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    // upDuration은 솟기 시작해서 가라앉기 시작하기 전까지의 총 시간이다. 끝나면 스스로 파괴된다.
    public void Play(float upDuration)
    {
        _sequence?.Kill();
        IsUp = false;

        if (visual == null)
        {
            Destroy(gameObject);
            return;
        }

        float hold = Mathf.Max(0f, upDuration - riseDuration);
        visual.localPosition = new Vector3(0f, hiddenLocalY, 0f);

        _sequence = DOTween.Sequence()
            .Append(visual.DOLocalMoveY(0f, riseDuration).SetEase(Ease.OutBack))
            .AppendCallback(() => IsUp = true)
            .AppendInterval(hold)
            .AppendCallback(() => IsUp = false)
            .Append(visual.DOLocalMoveY(hiddenLocalY, sinkDuration).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(gameObject))
            .SetLink(gameObject);
    }
}
