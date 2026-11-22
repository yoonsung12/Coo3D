using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 자판기 위에 띄우는 "체력 증가" 네온 간판이다.
// 평소에는 꺼져 있고 하트만 은은하게 숨쉬듯 깜빡여, 어두운 곳에서도 "뭔가 있다"는 걸 알려준다.
// 플레이어가 자판기 범위에 들어오면(VendingMachine이 SetLit(true) 호출) 네온이 지직거리며 켜지고,
// 나가면 다시 꺼진다. 구매 로직은 VendingMachine이 그대로 담당하고 이 스크립트는 연출만 한다.
// 간판은 World Space Canvas(UI)라 조명의 영향을 받지 않아서, 어두운 구역 안에서도 스스로 밝게 보인다.
public class VendingMachineSign : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("간판 전체 (Canvas Group)")]
    private CanvasGroup signGroup;
    // 판·글자·테두리를 모두 담은 CanvasGroup이다. 꺼졌을 때의 어두운 정도를 alpha로 조절한다.

    [SerializeField, LabelText("네온 부분 (Canvas Group)")]
    private CanvasGroup neonGroup;
    // 네온 테두리·밝은 글자만 담은 CanvasGroup이다. 켜질 때 이 alpha를 깜빡여 "지직" 느낌을 낸다.

    [SerializeField, LabelText("움직일 간판 Transform")]
    private RectTransform signBody;
    // 팝(커졌다 복귀)과 둥실 연출을 적용할 대상이다. 보통 Canvas 바로 아래 루트를 연결한다.

    [SerializeField, LabelText("하트")]
    private RectTransform heart;
    // 평소 숨쉬기, 켜진 동안 박동 연출을 적용할 하트 UI다.

    [SerializeField, LabelText("하트 그래픽")]
    private Graphic heartGraphic;
    // 꺼져 있을 때 하트만 은은하게 빛나도록 색 alpha를 조절할 그래픽(Text 또는 Image)이다.

    [SerializeField, LabelText("반짝이들")]
    private RectTransform[] sparkles;
    // 켜진 동안 번갈아 반짝일 작은 별 UI들이다. 비워두면 반짝이 연출을 생략한다.

    [SerializeField, LabelText("가격 글자")]
    private Text priceText;
    // "MAX HP +1 · 15" 처럼 가격을 표시할 Text다. 시작할 때 자판기의 실제 코인 가격으로 채운다.

    [SerializeField, LabelText("자판기")]
    private VendingMachine vendingMachine;
    // 가격(코인 개수)을 읽어올 자판기다. 가격을 바꿔도 간판 숫자가 자동으로 맞춰진다.

    [SerializeField, LabelText("간판 조명 (Point Light)")]
    private Light signLight;
    // 간판이 켜질 때 벽·바닥에 분홍빛을 번지게 할 Point Light다. 비워두면 조명 연출을 생략한다.

    [Title("방향")]
    [SerializeField, LabelText("카메라 쪽으로 돌리기")]
    private bool faceCamera = true;
    // 켜면 간판이 항상 카메라를 향하도록 좌우(Y축)로만 돈다. 자판기가 비스듬히 놓여도 글자가 잘 읽힌다.

    [Title("꺼져 있을 때")]
    [SerializeField, LabelText("꺼진 간판 밝기"), Range(0f, 1f)]
    private float offAlpha = 0.35f;
    // 꺼져 있을 때 간판 전체의 alpha다. 낮을수록 어둠에 묻히고, 높을수록 멀리서도 형태가 보인다.

    [SerializeField, LabelText("하트 숨쉬기 주기(초)")]
    private float idleBreathDuration = 1.5f;
    // 꺼져 있는 동안 하트가 밝아졌다 어두워지는 한 번의 시간이다. 클수록 느긋하게 깜빡인다.

    [Title("켜질 때 (DOTween)")]
    [SerializeField, LabelText("깜빡임 횟수")]
    private int flickerCount = 3;
    // 완전히 켜지기 전에 네온이 지직거리는 횟수다.

    [SerializeField, LabelText("깜빡임 한 번 시간")]
    private float flickerInterval = 0.06f;

    [SerializeField, LabelText("팝 크기")]
    private float popScale = 0.15f;
    // 켜지는 순간 간판이 원래 크기보다 얼마나 커졌다 돌아올지(비율)다. 0이면 생략한다.

    [SerializeField, LabelText("팝 시간")]
    private float popDuration = 0.35f;

    [SerializeField, LabelText("조명 밝기")]
    private float lightIntensity = 4f;
    // 켜졌을 때 Point Light의 Intensity다. 클수록 주변 벽·바닥이 더 분홍빛으로 물든다.

    [Title("켜져 있는 동안 (DOTween)")]
    [SerializeField, LabelText("둥실 높이")]
    private float floatHeight = 6f;
    // 간판이 위아래로 움직이는 폭(Canvas 단위)이다. 0이면 둥실 연출을 생략한다.

    [SerializeField, LabelText("둥실 주기(초)")]
    private float floatDuration = 1.2f;

    [SerializeField, LabelText("하트 박동 크기")]
    private float heartBeatScale = 1.2f;

    [SerializeField, LabelText("하트 박동 주기(초)")]
    private float heartBeatDuration = 0.45f;

    [Title("꺼질 때 (DOTween)")]
    [SerializeField, LabelText("꺼지는 시간")]
    private float turnOffDuration = 0.3f;

    [SerializeField, LabelText("전환 Ease")]
    private Ease fadeEase = Ease.OutQuad;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("켜짐")]
    private bool _isLit;

    private Sequence _stateSequence;
    // 켜기/끄기 연출 전체다. 들락날락할 때 이전 연출을 끊고 새로 시작하기 위해 저장한다.
    private Tween _idleTween;
    // 꺼져 있는 동안의 하트 숨쉬기 루프다.
    private Tween _floatTween;
    private Tween _heartBeatTween;
    private Sequence _sparkleSequence;
    // 켜져 있는 동안 반복되는 루프 연출들이다. 꺼질 때 모두 정리한다.

    private Vector2 _bodyBasePos;
    private Vector3 _bodyBaseScale;
    private Color _heartBaseColor;
    private Camera _camera;

    private void Awake()
    {
        _bodyBasePos = signBody.anchoredPosition;
        _bodyBaseScale = signBody.localScale;
        if (heartGraphic != null) _heartBaseColor = heartGraphic.color;
        _camera = Camera.main;

        UpdatePriceText();
        ApplyOffStateImmediate();
    }

    private void OnEnable()
    {
        if (!_isLit) StartIdle();
    }

    private void OnDisable()
    {
        // 비활성화될 때 남은 Tween이 계속 돌지 않도록 모두 정리한다.
        KillAll();
    }

    private void OnDestroy()
    {
        KillAll();
    }

    // 카메라를 향하도록 Y축으로만 돌린다. 위아래로는 기울이지 않아 간판이 똑바로 서 있게 한다.
    // 카메라가 움직인 뒤 방향을 맞춰야 떨리지 않으므로 LateUpdate에서 처리한다.
    private void LateUpdate()
    {
        if (!faceCamera) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Vector3 toCamera = _camera.transform.position - transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f) return;

        // UI는 앞면이 -Z 방향이므로, +Z가 카메라 반대쪽을 보도록 돌린다.
        transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }

    // VendingMachine이 플레이어가 범위에 들어오고 나갈 때 호출한다.
    public void SetLit(bool lit)
    {
        if (_isLit == lit) return;
        _isLit = lit;

        if (lit) TurnOn();
        else TurnOff();
    }

    // 자판기의 실제 코인 가격으로 아랫줄 글자를 채운다.
    private void UpdatePriceText()
    {
        if (priceText == null || vendingMachine == null) return;
        priceText.text = $"MAX HP +1  ·  {vendingMachine.CoinCost}";
    }

    // 네온이 지직거리며 켜지고, 간판이 팝 하고 커졌다 돌아온 뒤 켜진 상태 루프를 시작한다.
    private void TurnOn()
    {
        _stateSequence?.Kill();
        _idleTween?.Kill();
        if (heartGraphic != null) heartGraphic.color = _heartBaseColor;
        signBody.localScale = _bodyBaseScale;
        // 꺼지는 연출이 중간에 끊겼을 수 있으므로, 팝 연출 전에 크기를 원래대로 맞춘다.

        _stateSequence = DOTween.Sequence();
        _stateSequence.Append(signGroup.DOFade(1f, 0.1f));

        // 지직: 네온을 짧게 켰다 껐다 반복한다.
        for (int i = 0; i < flickerCount; i++)
        {
            _stateSequence.Append(neonGroup.DOFade(1f, flickerInterval));
            _stateSequence.Append(neonGroup.DOFade(0.15f, flickerInterval));
        }
        _stateSequence.Append(neonGroup.DOFade(1f, flickerInterval));

        if (popScale > 0f)
            _stateSequence.Join(signBody.DOPunchScale(_bodyBaseScale * popScale, popDuration, 6, 0.5f));

        if (signLight != null)
        {
            signLight.enabled = true;
            _stateSequence.Join(signLight.DOIntensity(lightIntensity, popDuration).SetEase(fadeEase));
        }

        _stateSequence.OnComplete(StartLitLoops);
    }

    // 간판을 서서히 끄고, 꺼진 상태의 하트 숨쉬기로 돌아간다.
    private void TurnOff()
    {
        _stateSequence?.Kill();
        KillLitLoops();

        _stateSequence = DOTween.Sequence();
        _stateSequence.Append(neonGroup.DOFade(0f, turnOffDuration).SetEase(fadeEase));
        _stateSequence.Join(signGroup.DOFade(offAlpha, turnOffDuration).SetEase(fadeEase));
        _stateSequence.Join(signBody.DOAnchorPos(_bodyBasePos, turnOffDuration).SetEase(fadeEase));
        _stateSequence.Join(signBody.DOScale(_bodyBaseScale, turnOffDuration).SetEase(fadeEase));
        if (heart != null) _stateSequence.Join(heart.DOScale(1f, turnOffDuration));

        if (signLight != null)
            _stateSequence.Join(signLight.DOIntensity(0f, turnOffDuration).SetEase(fadeEase));

        _stateSequence.OnComplete(() =>
        {
            if (signLight != null) signLight.enabled = false;
            StartIdle();
        });
    }

    // 켜져 있는 동안: 간판 둥실, 하트 박동, 반짝이를 무한 반복한다.
    private void StartLitLoops()
    {
        KillLitLoops();

        if (floatHeight > 0f)
        {
            _floatTween = signBody
                .DOAnchorPosY(_bodyBasePos.y + floatHeight, floatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (heart != null)
        {
            _heartBeatTween = heart
                .DOScale(heartBeatScale, heartBeatDuration)
                .SetEase(Ease.OutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (sparkles != null && sparkles.Length > 0)
        {
            // 반짝이를 하나씩 번갈아 커졌다 작아지게 해서 "반짝반짝" 하는 느낌을 준다.
            _sparkleSequence = DOTween.Sequence();
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (sparkles[i] == null) continue;
                sparkles[i].localScale = Vector3.zero;
                _sparkleSequence.Insert(i * 0.25f, sparkles[i].DOScale(1f, 0.25f).SetEase(Ease.OutBack));
                _sparkleSequence.Insert(i * 0.25f + 0.3f, sparkles[i].DOScale(0f, 0.25f).SetEase(Ease.InQuad));
            }
            _sparkleSequence.AppendInterval(0.3f).SetLoops(-1);
        }
    }

    private void KillLitLoops()
    {
        _floatTween?.Kill();
        _heartBeatTween?.Kill();
        _sparkleSequence?.Kill();

        if (sparkles == null) return;
        foreach (RectTransform s in sparkles)
            if (s != null) s.localScale = Vector3.zero;
    }

    // 꺼져 있는 동안 하트만 은은하게 밝아졌다 어두워지기를 반복한다.
    private void StartIdle()
    {
        _idleTween?.Kill();
        if (heartGraphic == null) return;

        Color dim = _heartBaseColor;
        dim.a = _heartBaseColor.a * 0.3f;
        heartGraphic.color = dim;
        _idleTween = heartGraphic
            .DOColor(_heartBaseColor, idleBreathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    // 시작할 때 꺼진 상태로 즉시 맞춘다.
    private void ApplyOffStateImmediate()
    {
        signGroup.alpha = offAlpha;
        neonGroup.alpha = 0f;
        if (signLight != null)
        {
            signLight.intensity = 0f;
            signLight.enabled = false;
        }
        if (sparkles == null) return;
        foreach (RectTransform s in sparkles)
            if (s != null) s.localScale = Vector3.zero;
    }

    private void KillAll()
    {
        _stateSequence?.Kill();
        _idleTween?.Kill();
        KillLitLoops();
    }

    [Button("켜기 테스트 (Play Mode)")]
    private void TestTurnOn()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[VendingMachineSign] Play Mode에서만 테스트할 수 있습니다."); return; }
        SetLit(true);
    }

    [Button("끄기 테스트 (Play Mode)")]
    private void TestTurnOff()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[VendingMachineSign] Play Mode에서만 테스트할 수 있습니다."); return; }
        SetLit(false);
    }
}
