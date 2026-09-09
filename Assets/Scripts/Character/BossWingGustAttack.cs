using System;
using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

// 평범한 패턴 중 완급 조절용 "날개 돌풍"을 담당한다. 데미지 없이, 짧은 전조 동작 후
// 반경 안의 플레이어를 강하게 밀어낸다. 넉백은 PlayerController.SetBlast()를 재사용한다
// (FanTool 반동 넉백과 같은 통로를 그대로 쓴다).
public class BossWingGustAttack : MonoBehaviour, IBossBasicAttack
{
    [Title("돌풍 설정")]
    [SerializeField, LabelText("판정 반경")]
    private float gustRadius = 3f;

    [SerializeField, LabelText("넉백 세기")]
    private float knockbackStrength = 12f;

    [SerializeField, LabelText("전조 동작 시간")]
    private float windUpDuration = 0.4f;
    // 이 시간 동안 연출만 보여주고 판정은 아직 없다 — 플레이어가 거리를 벌릴 시간이다.

    [SerializeField, LabelText("타격 레이어")]
    private LayerMask hitLayers;
    // Inspector에서 Player가 속한 레이어(Default)를 체크한다.

    [Title("연출 연결")]
    [SerializeField, LabelText("연출 대상 (보스 Visual)")]
    private Transform visualBody;
    // Inspector에서 보스의 Visual 자식을 연결한다. 비워두면 연출 없이 판정만 동작한다.

    private Tween _windUpTween;

    private void OnDestroy()
    {
        _windUpTween?.Kill();
    }

    public void Execute(float dir, Action onFinished)
    {
        StartCoroutine(GustRoutine(onFinished));
    }

    private IEnumerator GustRoutine(Action onFinished)
    {
        PlayWindUpVisual();

        yield return new WaitForSeconds(windUpDuration);

        Blast();
        onFinished?.Invoke();
    }

    private void PlayWindUpVisual()
    {
        if (visualBody == null) return;

        _windUpTween?.Kill();
        _windUpTween = visualBody.DOPunchScale(Vector3.one * 0.15f, windUpDuration, vibrato: 4, elasticity: 0.5f);
    }

    // 반경 안의 PlayerController를 찾아 보스 반대쪽으로 넉백시킨다.
    private void Blast()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, gustRadius, hitLayers);
        foreach (Collider col in cols)
        {
            if (!col.TryGetComponent<PlayerController>(out var player)) continue;

            float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;

            player.SetBlast(new Vector3(dir * knockbackStrength, 0f, 0f));
        }
    }

    [Button("날개 돌풍 테스트")]
    private void TestGust() => Execute(1f, null);
}
