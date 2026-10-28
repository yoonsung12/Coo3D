using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 보스 아레나(원형섬)에서 비둘킹 봄 패턴(BossSpringIslandPattern)이 땅에서 솟아오르게 하는 바위다.
// 평소에는 땅속에 숨어 있다가(Start에서 HideImmediate) 패턴이 Rise()를 부르면 솟아오르고,
// 보스가 돌진으로 들이받으면 Break()로 영구히 부서진다. 부서진 바위(IsBroken)는 다시 돌아오지 않는다.
// 겨울 바람 패턴 등 다른 패턴이 IsBroken/IsRisen을 읽을 수 있도록 공개 읽기 전용 프로퍼티로 제공한다.
// 위치 이동은 DOTween(DOMoveY)으로 하는데, 바위는 Rigidbody 없는 정적 오브젝트라 물리 충돌 문제는 없다.
public class IslandRock : MonoBehaviour
{
    [Title("솟아오름 설정")]
    [SerializeField, LabelText("숨었을 때 내려가는 깊이")]
    private float hiddenOffsetY = 2.2f;
    // 바위 높이(2m)보다 크게 잡아야 숨었을 때 지면 아래로 완전히 가려진다.

    [SerializeField, LabelText("솟아오르는 시간")]
    private float riseDuration = 0.5f;

    [SerializeField, LabelText("솟아오르는 Ease")]
    private Ease riseEase = Ease.OutBack;

    [SerializeField, LabelText("솟아오른 뒤 흔들림 세기")]
    private float riseShakeStrength = 0.1f;

    [SerializeField, LabelText("가라앉는 시간")]
    private float sinkDuration = 0.5f;

    [Title("경고(유인 충돌 예고) 설정")]
    [SerializeField, LabelText("경고 흔들림 세기")]
    private float warningShakeStrength = 0.08f;
    // 보스가 이 바위를 들이받을 예정일 때 좌우로 떨리는 정도. 클수록 크게 떨린다.

    [SerializeField, LabelText("경고 점멸 색")]
    private Color warningFlashColor = new Color(1f, 0.9f, 0.2f, 1f);

    [SerializeField, LabelText("경고 점멸 한 번 시간")]
    private float warningFlashDuration = 0.25f;

    [Title("부서짐 설정")]
    [SerializeField, LabelText("부서지는 연출 시간")]
    private float breakDuration = 0.4f;

    [SerializeField, LabelText("부서질 때 흔들림 세기")]
    private float breakShakeStrength = 0.3f;

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("솟아올라 있음")]
    public bool IsRisen { get; private set; }

    [ReadOnly, ShowInInspector, LabelText("부서짐 (영구)")]
    public bool IsBroken { get; private set; }

    // 솟아올랐을 때의 바위 중심 XZ와 바닥 면적 반지름이다. 패턴이 돌진 경로/밀어내기 판정에 사용한다.
    public Vector3 FootprintCenter => new Vector3(_shownPosition.x, 0f, _shownPosition.z);
    public float FootprintRadius { get; private set; } = 1f;

    private Collider[] _colliders;
    private Renderer[] _renderers;
    private Vector3 _shownPosition;
    private Tween _tween;
    private Tween _warningShakeTween; // 경고 전용 트윈. Rise/Sink/Break용 _tween과 분리해 서로 끊지 않게 한다.
    private Tween _warningFlashTween;
    private bool _warningOn;
    private float _flash01;
    private Color[] _baseColors;
    private bool[] _hasBaseColor;
    private MaterialPropertyBlock _block;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _shownPosition = transform.position;
        CacheBaseColors();

        // 솟아오른 상태(씬 저장 상태)의 콜라이더 범위로 바닥 반지름을 계산한다. 숨기기 전에 한 번만 계산해야 정확하다.
        float radius = 0f;
        foreach (Collider col in _colliders)
        {
            Vector3 e = col.bounds.extents;
            radius = Mathf.Max(radius, e.x, e.z);
        }
        if (radius > 0f) FootprintRadius = radius;
    }

    private void Start()
    {
        HideImmediate();
    }

    private void OnDisable()
    {
        StopWarning(false);
        if (_tween != null) _tween.Kill();
    }

    private void OnDestroy()
    {
        StopWarning(false);
        if (_tween != null) _tween.Kill();
    }

    // 연출 없이 즉시 땅속으로 숨긴다. 숨은 동안 콜라이더는 꺼서 플레이어/보스가 걸리지 않게 한다.
    [Button("즉시 숨기기 (테스트)")]
    public void HideImmediate()
    {
        if (IsBroken) return;
        StopWarning(false);
        if (_tween != null) _tween.Kill();
        transform.position = _shownPosition + Vector3.down * hiddenOffsetY;
        IsRisen = false;
        SetCollidersEnabled(false);
    }

    [Button("솟아오르기 (테스트)")]
    public void Rise()
    {
        if (IsBroken || IsRisen) return;
        IsRisen = true;
        SetCollidersEnabled(true); // 솟는 도중에도 막히도록 시작 시점에 켠다.

        if (_tween != null) _tween.Kill();
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMoveY(_shownPosition.y, riseDuration).SetEase(riseEase));
        seq.Append(transform.DOShakePosition(0.2f, new Vector3(riseShakeStrength, 0f, riseShakeStrength), 20));
        seq.OnComplete(() => transform.position = _shownPosition); // 흔들림 오차를 제거한다.
        _tween = seq;
    }

    [Button("가라앉기 (테스트)")]
    public void Sink()
    {
        if (IsBroken || !IsRisen) return;
        IsRisen = false;

        StopWarning(true);
        if (_tween != null) _tween.Kill();
        _tween = transform.DOMoveY(_shownPosition.y - hiddenOffsetY, sinkDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => SetCollidersEnabled(false));
    }

    // 보스에게 들이받혀 영구히 부서진다. 콜라이더는 즉시 꺼서 보스 돌진이 막히지 않게 한다.
    [Button("부수기 (테스트)")]
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        IsRisen = false;
        SetCollidersEnabled(false);

        StopWarning(true);
        if (_tween != null) _tween.Kill();
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOShakePosition(breakDuration, new Vector3(breakShakeStrength, 0f, breakShakeStrength), 30));
        seq.Join(transform.DOScale(Vector3.zero, breakDuration).SetEase(Ease.InBack));
        seq.OnComplete(() =>
        {
            foreach (Renderer r in _renderers)
                r.enabled = false;
        });
        _tween = seq;
    }

    // 보스가 이 바위를 들이받을 예정일 때 켜는 경고 연출이다(좌우 떨림 + 색 점멸).
    // 이미 같은 상태면 아무것도 하지 않으므로 매 프레임 호출돼도 트윈이 재시작되지 않는다.
    public void SetWarning(bool on)
    {
        if (on == _warningOn) return;
        if (on)
        {
            if (!IsRisen || IsBroken) return; // 솟아 있는 바위만 경고한다.
            _warningOn = true;

            _warningShakeTween = transform.DOShakePosition(0.3f, new Vector3(warningShakeStrength, 0f, 0f), 25, 90f, false, false)
                .SetLoops(-1, LoopType.Restart);

            _flash01 = 0f;
            _warningFlashTween = DOTween.To(() => _flash01, v => { _flash01 = v; ApplyFlash(); }, 1f, warningFlashDuration)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            StopWarning(true);
        }
    }

    // 경고 트윈을 끄고 색을 원래대로 돌린다. restorePosition이면 떨리던 위치를 원래 자리로 복구한다.
    private void StopWarning(bool restorePosition)
    {
        if (_warningShakeTween != null) { _warningShakeTween.Kill(); _warningShakeTween = null; }
        if (_warningFlashTween != null) { _warningFlashTween.Kill(); _warningFlashTween = null; }

        bool wasOn = _warningOn;
        _warningOn = false;
        if (!wasOn) return;

        if (restorePosition && IsRisen && !IsBroken) transform.position = _shownPosition;
        if (_renderers != null)
        {
            foreach (Renderer r in _renderers)
            {
                if (r != null) r.SetPropertyBlock(null);
            }
        }
    }

    // 렌더러 원래 _BaseColor를 저장해 둔다(점멸 때 이 색에서 경고색으로 섞기 위해서다).
    private void CacheBaseColors()
    {
        _block = new MaterialPropertyBlock();
        _baseColors = new Color[_renderers.Length];
        _hasBaseColor = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material m = _renderers[i].sharedMaterial;
            if (m != null && m.HasProperty("_BaseColor"))
            {
                _baseColors[i] = m.GetColor("_BaseColor");
                _hasBaseColor[i] = true;
            }
        }
    }

    private void ApplyFlash()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_hasBaseColor[i] || _renderers[i] == null) continue;
            _block.SetColor("_BaseColor", Color.Lerp(_baseColors[i], warningFlashColor, _flash01));
            _renderers[i].SetPropertyBlock(_block);
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider col in _colliders)
            col.enabled = enabled;
    }
}
