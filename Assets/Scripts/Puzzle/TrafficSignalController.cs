using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

// 신호등 퍼즐의 신호를 관리한다.
// 양옆 스위치(PressButton)가 "거의 동시에"(허용 시간 이내) 눌리면 스위치를 잠그고,
// 신호등을 비추는 컷신과 함께 초록불을 끄고 빨간불을 켠다. 빨간불이 되면 CrosswalkCarHazard의 차가 횡단보도 앞에서 멈춘다.
public class TrafficSignalController : MonoBehaviour
{
    [Title("스위치")]
    [SerializeField, LabelText("스위치 목록")]
    private PressButton[] switches;
    // 이 신호를 바꾸는 데 필요한 스위치(왼쪽/오른쪽)를 모두 연결한다.

    [SerializeField, LabelText("동시 판정 허용 시간(초)")]
    private float syncWindow = 1f;
    // 모든 스위치가 이 시간 안에 한 번씩 눌리면 성공이다.
    // 되돌아오는 오브젝트는 스위치에 닿자마자 튕겨 나가므로, 정확히 같은 순간을 요구하면 성공하기 어렵다.

    [Title("신호등 표시등")]
    [SerializeField, LabelText("빨간불 렌즈")]
    private Renderer redLens;

    [SerializeField, LabelText("초록불 렌즈")]
    private Renderer greenLens;

    [SerializeField, LabelText("빨간불 조명")]
    private Light redLight;

    [SerializeField, LabelText("초록불 조명")]
    private Light greenLight;
    // 렌즈 앞을 비추는 Point Light다. 켜질 때 주변이 은은하게 물들어 불이 들어온 느낌을 준다.

    [SerializeField, LabelText("빨간불 색")]
    private Color redColor = new Color(1f, 0.1f, 0.05f);

    [SerializeField, LabelText("초록불 색")]
    private Color greenColor = new Color(0.1f, 1f, 0.4f);

    [SerializeField, LabelText("꺼진 렌즈 색")]
    private Color offColor = new Color(0.05f, 0.05f, 0.06f);

    [SerializeField, LabelText("발광 세기")]
    private float emissionIntensity = 3f;
    // 켜진 렌즈가 빛나는 정도다. 클수록 블룸이 있을 때 더 번져 보인다.

    [SerializeField, LabelText("조명 세기")]
    private float lightIntensity = 2f;

    [Title("컷신")]
    [SerializeField, LabelText("컷신 카메라")]
    private Camera cutsceneCamera;
    // 신호등을 비추는 별도 카메라다(평소 비활성). 비워두면 컷신 없이 불만 바뀐다.
    // Main Camera보다 Depth가 높아야 켜졌을 때 화면을 덮는다.

    [SerializeField, LabelText("Input Action Asset")]
    private InputActionAsset inputActionAsset;
    // 컷신 동안 Player 액션맵을 꺼서 플레이어 조작을 막는다(PauseMenuUI와 같은 방식).

    [SerializeField, LabelText("페이드 시간")]
    private float fadeDuration = 0.3f;

    [SerializeField, LabelText("초록불 꺼지기 전 대기")]
    private float beforeGreenOffDelay = 0.5f;

    [SerializeField, LabelText("빨간불 켜지기 전 대기")]
    private float beforeRedOnDelay = 0.4f;

    [SerializeField, LabelText("불 전환 시간")]
    private float lightSwitchDuration = 0.25f;

    [SerializeField, LabelText("빨간불 보여주는 시간")]
    private float holdDuration = 1.2f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("빨간불")]
    public bool IsRed { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("스위치별 마지막 눌린 시각")]
    private float[] _pressedTimes;

    private Material _redMat;
    private Material _greenMat;
    private Sequence _cutsceneSequence;
    private InputActionMap _playerActionMap;

    private void Awake()
    {
        _pressedTimes = new float[switches.Length];
        for (int i = 0; i < _pressedTimes.Length; i++)
            _pressedTimes[i] = -999f;

        // 렌즈마다 머티리얼 인스턴스를 만들어 색을 따로 바꾼다.
        if (redLens != null) _redMat = redLens.material;
        if (greenLens != null) _greenMat = greenLens.material;

        if (inputActionAsset != null)
            _playerActionMap = inputActionAsset.FindActionMap("Player");

        if (cutsceneCamera != null)
            cutsceneCamera.gameObject.SetActive(false);
    }

    private void Start()
    {
        // 평소에는 초록불(차가 다니는 상태)이다.
        SetLensImmediate(_greenMat, greenLight, greenColor, true);
        SetLensImmediate(_redMat, redLight, redColor, false);

        foreach (PressButton sw in switches)
            if (sw != null) sw.OnStateChanged += HandleSwitchChanged;
    }

    private void OnDestroy()
    {
        foreach (PressButton sw in switches)
            if (sw != null) sw.OnStateChanged -= HandleSwitchChanged;

        _cutsceneSequence?.Kill();
    }

    private void HandleSwitchChanged(PressButton changed)
    {
        if (IsRed) return;

        // 눌린 순간의 시각만 기록한다. 떨어질 때(Active)는 기록을 지우지 않아 "허용 시간" 안의 엇갈림을 인정한다.
        int index = System.Array.IndexOf(switches, changed);
        if (index < 0 || changed.State != PressButton.ButtonState.Pressed) return;
        _pressedTimes[index] = Time.time;

        // 가장 먼저 눌린 스위치와 가장 늦게 눌린 스위치의 시간 차가 허용 시간 이내면 성공이다.
        float earliest = float.MaxValue, latest = float.MinValue;
        foreach (float t in _pressedTimes)
        {
            earliest = Mathf.Min(earliest, t);
            latest = Mathf.Max(latest, t);
        }

        if (latest - earliest <= syncWindow)
            TurnRed();
    }

    // 퍼즐 성공: 스위치를 잠그고 컷신과 함께 빨간불로 바꾼다.
    public void TurnRed()
    {
        if (IsRed) return;
        IsRed = true;

        foreach (PressButton sw in switches)
            if (sw != null) sw.Lock();

        if (cutsceneCamera == null || ScreenFader.Instance == null)
        {
            // 컷신 준비가 안 된 환경에서는 불만 바로 바꾼다.
            BuildLightSwitchSequence(DOTween.Sequence());
            return;
        }

        SetPlayerInput(false);
        ScreenFader.Instance.FadeOut(PlayCutscene, fadeDuration);
    }

    // 화면이 덮인 상태에서 호출된다. 컷신 카메라로 바꾼 뒤 불 전환을 보여주고, 다시 원래 화면으로 돌아온다.
    private void PlayCutscene()
    {
        cutsceneCamera.gameObject.SetActive(true);
        ScreenFader.Instance.FadeIn(null, fadeDuration);

        _cutsceneSequence?.Kill();
        _cutsceneSequence = DOTween.Sequence().AppendInterval(fadeDuration);
        BuildLightSwitchSequence(_cutsceneSequence);
        _cutsceneSequence
            .AppendInterval(holdDuration)
            .AppendCallback(() => ScreenFader.Instance.FadeOut(EndCutscene, fadeDuration));
    }

    private void EndCutscene()
    {
        cutsceneCamera.gameObject.SetActive(false);
        ScreenFader.Instance.FadeIn(null, fadeDuration);
        SetPlayerInput(true);
    }

    // "초록불이 꺼지고 → 잠시 뒤 빨간불이 켜지는" 연출을 주어진 Sequence 뒤에 이어 붙인다.
    private void BuildLightSwitchSequence(Sequence sequence)
    {
        sequence
            .AppendInterval(beforeGreenOffDelay)
            .Append(TweenLens(_greenMat, greenLight, greenColor, false))
            .AppendInterval(beforeRedOnDelay)
            .Append(TweenLens(_redMat, redLight, redColor, true));
    }

    // 렌즈 색/발광과 조명 세기를 함께 바꾸는 Tween이다.
    private Sequence TweenLens(Material mat, Light lensLight, Color onColor, bool on)
    {
        Sequence s = DOTween.Sequence();
        if (mat != null)
        {
            Color targetBase = on ? onColor : offColor;
            Color targetEmission = on ? onColor * emissionIntensity : Color.black;
            s.Join(DOTween.To(() => mat.GetColor("_BaseColor"), c => mat.SetColor("_BaseColor", c), targetBase, lightSwitchDuration));
            s.Join(DOTween.To(() => mat.GetColor("_EmissionColor"), c => mat.SetColor("_EmissionColor", c), targetEmission, lightSwitchDuration));
        }
        if (lensLight != null)
            s.Join(lensLight.DOIntensity(on ? lightIntensity : 0f, lightSwitchDuration));
        return s;
    }

    private void SetLensImmediate(Material mat, Light lensLight, Color onColor, bool on)
    {
        if (mat != null)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_BaseColor", on ? onColor : offColor);
            mat.SetColor("_EmissionColor", on ? onColor * emissionIntensity : Color.black);
        }
        if (lensLight != null)
            lensLight.intensity = on ? lightIntensity : 0f;
    }

    private void SetPlayerInput(bool enabled)
    {
        if (_playerActionMap == null) return;
        if (enabled) _playerActionMap.Enable();
        else _playerActionMap.Disable();
    }

    [Button("빨간불 성공 테스트(컷신)")]
    private void TestTurnRed() => TurnRed();
}
