using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

// 플레이어에 1개만 추가. 실제 명중 시 SwordHitbox에서 호출한다.
// 주의: Time.timeScale은 게임 전체에 적용된다.
public class PillowHitFeedback : MonoBehaviour
{
    [Title("카메라")]
    [SerializeField, LabelText("SideViewCamera 연결")]
    private SideViewCamera sideViewCamera;

    [Title("Hit Stop — 실제 시간 기준")]
    [SerializeField, Range(0.01f, 0.2f)]
    private float hitStopDuration = 0.045f;

    [SerializeField, Range(0.01f, 1f)]
    private float hitTimeScale = 0.05f;

    [Title("카메라 흔들림")]
    [SerializeField, Range(0.02f, 0.5f)]
    private float shakeDuration = 0.15f;

    [SerializeField, Range(0f, 0.5f)]
    private float shakeStrength = 0.12f;

    private Coroutine _routine;
    private bool _ownsTimeScale;
    private float _savedTimeScale;
    private float _savedFixedDeltaTime;

    public void PlayHitFeedback()
    {
        // 카메라는 매번 흔들어도, 짧은 시간 내 중복 Hit Stop은 막는다.
        if (sideViewCamera != null)
            sideViewCamera.Shake(shakeDuration, shakeStrength);

        if (_routine != null) return;
        _routine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        _savedTimeScale = Time.timeScale;
        _savedFixedDeltaTime = Time.fixedDeltaTime;
        _ownsTimeScale = true;

        Time.timeScale = hitTimeScale;
        Time.fixedDeltaTime = _savedFixedDeltaTime *
            (hitTimeScale / Mathf.Max(0.0001f, _savedTimeScale));

        // 느려진 시간의 영향을 받지 않고 원래 시간에 복귀한다.
        yield return new WaitForSecondsRealtime(hitStopDuration);
        RestoreTime();
        _routine = null;
    }

    private void RestoreTime()
    {
        if (!_ownsTimeScale) return;
        Time.timeScale = _savedTimeScale;
        Time.fixedDeltaTime = _savedFixedDeltaTime;
        _ownsTimeScale = false;
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        RestoreTime();
    }

    [Button("명중 타격감 단독 테스트 (Play Mode)")]
    private void TestFeedback()
    {
        if (Application.isPlaying)
            PlayHitFeedback();
    }
}
