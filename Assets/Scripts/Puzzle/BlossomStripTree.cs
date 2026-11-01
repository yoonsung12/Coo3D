
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class BlossomStripTree : MonoBehaviour, IBlowable
{
    [Title("벗기기 조건")]
    [SerializeField, LabelText("필요 바람 시간")]
    private float requiredBlowTime = 1.5f;

    [SerializeField, LabelText("블라스트 한 방에 벗기기")]
    private bool stripOnBlast = true;

    [Title("연결")]
    [SerializeField, LabelText("튕김 트리거")]
    private Collider blossomBlocker;

    [SerializeField, LabelText("흔들 비주얼")]
    private Transform visual;

    [SerializeField, LabelText("제거할 잎사귀")]
    private GameObject leavesObject;

    [Title("Poof 이펙트")]
    [SerializeField, LabelText("펑 이펙트 프리팹")]
    private PoofEffect poofPrefab;

    [SerializeField, LabelText("이펙트 위치 보정")]
    private Vector3 poofOffset = Vector3.zero;

    [SerializeField, LabelText("이펙트 크기 배율")]
    private float poofScale = 1f;

    [SerializeField, LabelText("이펙트 자동 삭제 시간")]
    private float poofDestroyDelay = 3f;

    [Title("꽃잎 파티클 - 선택")]
    [SerializeField]
    private ParticleSystem petalParticle;

    [SerializeField]
    private float petalsPerSecond = 20f;

    [SerializeField]
    private int stripBurstCount = 60;

    [SerializeField]
    private int repelPetalCount = 15;

    [Title("사운드")]
    [SerializeField]
    private AudioClip repelSound;

    [SerializeField]
    private AudioSource audioSource;

    [Title("흔들림")]
    [SerializeField]
    private float repelShakeAngle = 6f;

    [SerializeField]
    private float repelPunchScale = 0.06f;

    [SerializeField]
    private float blowShakeAngle = 2f;

    [SerializeField]
    private float blowShakeInterval = 0.3f;

    [SerializeField]
    private float shakeDuration = 0.4f;

    [Title("런타임 상태")]
    [ReadOnly, ShowInInspector]
    private bool _isStripped;

    [ReadOnly, ShowInInspector, ProgressBar(0, 1)]
    private float Progress =>
        requiredBlowTime > 0f
            ? Mathf.Clamp01(_blowTime / requiredBlowTime)
            : 1f;

    private float _blowTime;
    private float _lastCountedFixedTime = -1f;
    private float _petalCarry;
    private float _lastBlowShakeTime = -999f;
    private float _lastShakeTime = -999f;

    private Quaternion _visualBaseRotation;
    private Vector3 _visualBaseScale;
    private Vector3 _swayPunch;

    private Sequence _shakeSequence;

    public bool IsStripped => _isStripped;

    private void Awake()
    {
        if (visual != null)
        {
            _visualBaseRotation = visual.rotation;
            _visualBaseScale = visual.localScale;
        }
    }

    private void OnDestroy()
    {
        _shakeSequence?.Kill();
    }

    public void PlayRepelFeedback()
    {
        if (_isStripped) return;

        Shake(repelShakeAngle, repelPunchScale);

        if (petalParticle != null)
            petalParticle.Emit(repelPetalCount);

        if (audioSource != null && repelSound != null)
            audioSource.PlayOneShot(repelSound);
    }

    private void Shake(float angle, float scaleRatio)
    {
        if (visual == null) return;

        // 반복 Tween 생성 제한
        if (Time.time - _lastShakeTime < shakeDuration)
            return;

        _lastShakeTime = Time.time;

        if (_visualBaseScale == Vector3.zero)
        {
            _visualBaseRotation = visual.rotation;
            _visualBaseScale = visual.localScale;
        }

        _shakeSequence?.Kill();

        visual.rotation = _visualBaseRotation;
        visual.localScale = _visualBaseScale;
        _swayPunch = Vector3.zero;

        _shakeSequence = DOTween.Sequence();

        _shakeSequence.Join(
            DOTween.Punch(
                () => _swayPunch,
                v =>
                {
                    _swayPunch = v;

                    visual.rotation =
                        Quaternion.AngleAxis(
                            v.x,
                            Vector3.forward
                        ) * _visualBaseRotation;
                },
                new Vector3(angle, 0f, 0f),
                shakeDuration,
                8,
                0.5f
            )
        );

        if (scaleRatio > 0f)
        {
            _shakeSequence.Join(
                visual.DOPunchScale(
                    _visualBaseScale * scaleRatio,
                    shakeDuration,
                    6,
                    0.5f
                )
            );
        }

        _shakeSequence.OnComplete(() =>
        {
            if (visual == null) return;

            visual.rotation = _visualBaseRotation;
            visual.localScale = _visualBaseScale;
        });
    }

    public void OnBlown(
        Vector3 direction,
        float force,
        bool impulse = false)
    {
        if (_isStripped) return;

        if (impulse && stripOnBlast)
        {
            Strip();
            return;
        }

        if (Mathf.Approximately(
            Time.fixedTime,
            _lastCountedFixedTime))
            return;

        _lastCountedFixedTime = Time.fixedTime;

        _blowTime += Time.fixedDeltaTime;

        EmitBlowPetals();

        if (Time.time - _lastBlowShakeTime >= blowShakeInterval)
        {
            _lastBlowShakeTime = Time.time;
            Shake(blowShakeAngle, 0f);
        }

        if (_blowTime >= requiredBlowTime)
            Strip();
    }

    private void EmitBlowPetals()
    {
        if (petalParticle == null) return;

        _petalCarry +=
            petalsPerSecond * Time.fixedDeltaTime;

        int count = Mathf.FloorToInt(_petalCarry);

        if (count <= 0) return;

        _petalCarry -= count;
        petalParticle.Emit(count);
    }

    private void Strip()
    {
        if (_isStripped) return;

        _isStripped = true;
        _blowTime = requiredBlowTime;

        // 먼저 잎사귀 위치 계산
        Vector3 effectPosition = transform.position;

        if (leavesObject != null)
        {
            Renderer[] renderers =
                leavesObject.GetComponentsInChildren<Renderer>(true);

            Bounds bounds = new Bounds();
            bool found = false;

            foreach (Renderer r in renderers)
            {
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            effectPosition = found
                ? bounds.center
                : leavesObject.transform.position;
        }

        effectPosition += poofOffset;

        // 잎사귀 즉시 제거
        if (leavesObject != null)
        {
            leavesObject.SetActive(false);
            Debug.Log("[Tree] 잎사귀 제거 완료");
        }
        else
        {
            Debug.LogWarning(
                "[Tree] Leaves Object 연결 안 됨!"
            );
        }

        // 튕김 판정 해제
        if (blossomBlocker != null)
            blossomBlocker.enabled = false;

        // 이펙트 자동 생성
        if (poofPrefab != null)
        {
            PoofEffect poof = Instantiate(
                poofPrefab,
                effectPosition,
                Quaternion.identity
            );

            // 프리팹 자체가 비활성 상태였을 경우
            if (!poof.gameObject.activeSelf)
                poof.gameObject.SetActive(true);

            poof.transform.localScale *= poofScale;

            poof.Play();

            Destroy(
                poof.gameObject,
                poofDestroyDelay
            );

            Debug.Log(
                "[Tree] Poof 생성 위치: " +
                effectPosition
            );
        }
        else
        {
            Debug.LogWarning(
                "[Tree] Poof Prefab 연결 안 됨!"
            );
        }

        // 파티클 선택
        if (petalParticle != null)
            petalParticle.Emit(stripBurstCount);

        // 나무 흔들림
        Shake(repelShakeAngle, repelPunchScale);
    }

    [Button("튕김 연출 테스트")]
    private void TestRepelFeedback()
    {
        PlayRepelFeedback();
    }

    [Button("벗기기 테스트")]
    private void TestStrip()
    {
        if (Application.isPlaying && !_isStripped)
            Strip();
    }

    [Button("초기화")]
    private void ResetTree()
    {
        _shakeSequence?.Kill();
        _shakeSequence = null;

        if (visual != null &&
            _visualBaseScale != Vector3.zero)
        {
            visual.rotation = _visualBaseRotation;
            visual.localScale = _visualBaseScale;
        }

        _isStripped = false;
        _blowTime = 0f;
        _lastCountedFixedTime = -1f;
        _lastShakeTime = -999f;
        _lastBlowShakeTime = -999f;
        _petalCarry = 0f;

        if (leavesObject != null)
            leavesObject.SetActive(true);

        if (blossomBlocker != null)
            blossomBlocker.enabled = true;
    }
}
