using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine;

// 공격키 입력 시 SW08을 교번 대각선 베기로 휘두르는 스크립트다.
// 첫 공격은 아래→위, 두 번째 공격은 위→아래로 번갈아 연결된다.
// 공격 중 재입력하면 현재 모션을 즉시 중단하고 다음 방향으로 바로 이어진다.
public class SwordAttack : MonoBehaviour
{
    [Title("SW08 참조")]
    [SerializeField, LabelText("칼 Transform")]
    private Transform sw08Transform;
    // Inspector에서 Player 자식으로 이동시킨 SW08의 Transform을 연결한다.

    [BoxGroup("Swing 0 — 아래→위")]
    [SerializeField, LabelText("준비 각도")]
    private Vector3 readyAngle0 = new Vector3(-110f, 0f, 0f);
    // 아래→위 베기의 시작 자세다. 칼끝이 화면 왼쪽 아래를 향한다.
    // X값이 음수이고 절대값이 클수록 칼끝이 더 왼쪽 아래로 내려간다.

    [BoxGroup("Swing 0 — 아래→위")]
    [SerializeField, LabelText("베기 완료 각도")]
    private Vector3 slashAngle0 = new Vector3(50f, 0f, 0f);
    // 아래→위 베기의 완료 자세다. 칼끝이 화면 오른쪽 위를 향한다.
    // 이 값은 Swing 1의 준비 각도와 일치시켜야 두 궤적이 같아진다.

    [BoxGroup("Swing 1 — 위→아래")]
    [SerializeField, LabelText("준비 각도")]
    private Vector3 readyAngle1 = new Vector3(50f, 0f, 0f);
    // 위→아래 베기의 시작 자세다. Swing 0 완료 위치(오른쪽 위)에서 이어진다.
    // Swing 0의 베기 완료 각도와 일치해야 같은 궤적을 공유한다.

    [BoxGroup("Swing 1 — 위→아래")]
    [SerializeField, LabelText("베기 완료 각도")]
    private Vector3 slashAngle1 = new Vector3(-110f, 0f, 0f);
    // 위→아래 베기의 완료 자세다. 칼끝이 화면 왼쪽 아래를 향한다.
    // Swing 0의 준비 각도와 일치해 두 스윙이 완전히 같은 호를 반대 방향으로 이동한다.

    [Title("공통 타이밍")]
    [SerializeField, LabelText("기본 대기 각도")]
    private Vector3 restAngle = new Vector3(30f, 0f, 0f);
    // 공격이 끝난 후 칼이 돌아오는 기본 자세 각도다.

    [SerializeField, LabelText("위치 보정 시간")]
    private float correctDuration = 0.05f;
    // 이전 공격 중단 후 다음 준비 자세로 이동하는 시간이다.
    // 짧을수록 즉각적으로 이어지는 느낌이 난다.

    [SerializeField, LabelText("베기 시간")]
    private float slashDuration = 0.13f;
    // 값이 작을수록 더 빠르고 날카로운 느낌이 난다.

    [SerializeField, LabelText("복귀 시간")]
    private float returnDuration = 0.25f;
    // 베기 후 대기 자세로 돌아오는 시간이다.

    [SerializeField, LabelText("베기 Ease")]
    private Ease slashEase = Ease.OutQuart;
    // 처음에 빠르고 끝에 느려지는 느낌을 준다.

    [Title("잔상 효과")]
    [SerializeField, LabelText("칼끝 TrailRenderer")]
    private TrailRenderer swordTrail;
    // SW08 자식 SwordTip 오브젝트의 TrailRenderer를 연결한다.
    // 비워두면 잔상 효과가 동작하지 않는다.

    [SerializeField, LabelText("칼끝 Point Light")]
    private Light tipLight;
    // SW08 자식 SwordTip 오브젝트의 Point Light를 연결한다.
    // 비워두면 빛 반짝임 효과가 동작하지 않는다.

    [SerializeField, LabelText("빛 최대 강도")]
    private float lightMaxIntensity = 3f;
    // 베기 순간 칼끝에서 발산하는 빛의 최대 밝기다. 클수록 더 강하게 빛난다.
    // URP Bloom이 활성화되어 있으면 HDR 효과로 번짐 효과도 생긴다.

    [Title("타격 판정")]
    [SerializeField, LabelText("칼날 히트박스")]
    private SwordHitbox swordHitbox;
    // SW08에 부착된 SwordHitbox 컴포넌트를 연결한다.
    // 비워두면 타격 판정 없이 연출만 동작한다.

    [Title("콤보 리셋 설정")]
    [SerializeField, LabelText("콤보 리셋 시간")]
    private float comboResetTime = 2f;
    // 마지막 공격 후 이 시간 동안 다음 공격이 없으면 콤보를 처음(아래→위)으로 되돌린다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("다음 공격 방향")]
    private string NextSwingLabel => _nextSwingIndex == 0 ? "아래→위 (Swing 0)" : "위→아래 (Swing 1)";

    [ReadOnly, ShowInInspector, LabelText("공격 중")]
    private bool _isAttacking;

    // 0 = 아래→위 (Swing 0), 1 = 위→아래 (Swing 1)
    // 공격키를 누를 때마다 즉시 전환된다.
    private int _nextSwingIndex = 0;

    private Sequence _slashSequence;
    private Coroutine _comboResetCoroutine;

    private void OnDestroy()
    {
        _slashSequence?.Kill();
        if (_comboResetCoroutine != null)
            StopCoroutine(_comboResetCoroutine);
    }

    // ToolManager에서 공격 모드(도구 미장착)로 공격키가 눌릴 때 호출된다.
    public void TryAttack()
    {
        // 이전 공격이 진행 중이면 즉시 중단한다.
        // SW08은 현재 각도에 멈추며, 새 Sequence가 그 위치에서 바로 이어진다.
        _slashSequence?.Kill();

        // 누른 시점에 즉시 다음 방향을 결정하고 인덱스를 전환한다.
        int swingIndex = _nextSwingIndex;
        _nextSwingIndex = 1 - _nextSwingIndex;

        // 콤보 리셋 타이머를 재시작한다.
        if (_comboResetCoroutine != null)
            StopCoroutine(_comboResetCoroutine);
        _comboResetCoroutine = StartCoroutine(ComboResetRoutine());

        PlaySwingSequence(swingIndex);
    }

    private void PlaySwingSequence(int index)
    {
        if (sw08Transform == null) return;

        _isAttacking = true;

        // 이전 연출이 남아 있을 경우를 대비해 빛을 꺼진 상태로 초기화한다.
        if (tipLight != null) tipLight.intensity = 0f;

        Vector3 readyAngle   = index == 0 ? readyAngle0 : readyAngle1;
        Vector3 slashEndAngle = index == 0 ? slashAngle0 : slashAngle1;

        // 빛 연출 Insert 위치를 계산한다. Sequence 시작 기준 각 구간의 시작 시간이다.
        float slashStartTime = correctDuration;
        float slashEndTime   = correctDuration + slashDuration;

        _slashSequence = DOTween.Sequence()
            // 현재 위치에서 준비 각도로 빠르게 이동한다.
            // DOLocalRotateQuaternion을 사용해 오일러 정규화 문제 없이 순수 쿼터니언 보간으로 이동한다.
            .Append(sw08Transform.DOLocalRotateQuaternion(Quaternion.Euler(readyAngle), correctDuration))
            // 베기 시작 직전에 준비 각도로 정확히 스냅해 보간 오차를 제거한다.
            // 이렇게 해야 이후 슬래시 트윈의 시작점이 기즈모 호의 시작점과 정확히 일치한다.
            .AppendCallback(() =>
            {
                sw08Transform.localRotation = Quaternion.Euler(readyAngle);
                swordHitbox?.EnableHitbox();
                if (swordTrail != null) swordTrail.emitting = true;
            })
            // 준비 자세에서 베기 완료 각도로 빠르게 휘두른다.
            // DOLocalRotateQuaternion으로 오일러 정규화 없이 순수 쿼터니언 경로로 이동해 기즈모 호와 일치시킨다.
            .Append(sw08Transform.DOLocalRotateQuaternion(Quaternion.Euler(slashEndAngle), slashDuration).SetEase(slashEase))
            // 베기 완료: 히트박스 비활성화, 잔상 끄기
            // 잔상은 emitting을 끈 이후에도 TrailRenderer의 Time 설정만큼 서서히 사라진다.
            .AppendCallback(() =>
            {
                swordHitbox?.DisableHitbox();
                if (swordTrail != null) swordTrail.emitting = false;
            })
            // 베기 후 기본 대기 자세로 부드럽게 복귀한다.
            .Append(sw08Transform.DOLocalRotate(restAngle, returnDuration))
            .OnComplete(() => _isAttacking = false)
            // 콤보 캔슬 시에도 히트박스, 잔상, 빛을 반드시 정리한다.
            // OnKill은 자연 완료 시에도 호출되므로 모두 멱등성 있게 처리한다.
            .OnKill(() =>
            {
                swordHitbox?.DisableHitbox();
                if (swordTrail != null) swordTrail.emitting = false;
                if (tipLight != null) tipLight.intensity = 0f;
            });

        // 빛 반짝임: Sequence에 Insert해 캔슬 시 자동으로 함께 정리되도록 한다.
        // 베기 시작 순간 최대 강도로 점등하고, 복귀 구간 동안 서서히 소멸한다.
        if (tipLight != null)
        {
            _slashSequence
                .Insert(slashStartTime, tipLight.DOIntensity(lightMaxIntensity, 0.02f))
                .Insert(slashEndTime,   tipLight.DOIntensity(0f, returnDuration));
        }
    }

    private IEnumerator ComboResetRoutine()
    {
        yield return new WaitForSeconds(comboResetTime);
        // 일정 시간 공격이 없으면 다음 공격이 아래→위(Swing 0)부터 시작되도록 리셋한다.
        _nextSwingIndex = 0;
        _comboResetCoroutine = null;
    }

    [Button("Swing 0 테스트 (아래→위)")]
    private void TestSwing0() => PlaySwingSequence(0);

    [Button("Swing 1 테스트 (위→아래)")]
    private void TestSwing1() => PlaySwingSequence(1);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (sw08Transform == null) return;

        // Swing 0: 빨간 호 (아래→위)
        DrawSwingArc(readyAngle0, slashAngle0, new Color(1f, 0.2f, 0.2f, 0.9f), "Swing 0");
        // Swing 1: 주황 호 (위→아래)
        DrawSwingArc(readyAngle1, slashAngle1, new Color(1f, 0.55f, 0.1f, 0.9f), "Swing 1");

        // 대기 자세 위치를 파란 점으로 표시한다.
        Vector3 restTip = GetTipWorldPos(restAngle);
        Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.9f);
        Gizmos.DrawSphere(restTip, 0.03f);
        UnityEditor.Handles.Label(restTip + Vector3.up * 0.1f, "대기");
    }

    private void DrawSwingArc(Vector3 fromEuler, Vector3 toEuler, Color color, string swingName)
    {
        const int steps = 24;
        Vector3 prev = Vector3.zero;
        Gizmos.color = color;

        Quaternion fromQ = Quaternion.Euler(fromEuler);
        Quaternion toQ   = Quaternion.Euler(toEuler);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            // DOTween과 동일하게 Quaternion.Slerp로 보간해 실제 베기 궤적과 일치시킨다.
            // Vector3.Lerp(euler)는 다축 회전에서 실제 경로와 달라지므로 사용하지 않는다.
            Vector3 tipWorld = GetTipWorldPos(Quaternion.Slerp(fromQ, toQ, t));
            if (i > 0) Gizmos.DrawLine(prev, tipWorld);
            prev = tipWorld;
        }

        // 준비 자세와 베기 완료 자세의 칼끝을 구(sphere)로 강조한다.
        Vector3 startTip = GetTipWorldPos(fromEuler);
        Vector3 endTip   = GetTipWorldPos(toEuler);
        Gizmos.DrawSphere(startTip, 0.045f);
        Gizmos.DrawSphere(endTip,   0.045f);

        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(startTip + Vector3.up * 0.1f, $"{swingName} 준비");
        UnityEditor.Handles.Label(endTip   + Vector3.up * 0.1f, $"{swingName} 완료");
    }

    // SW08이 주어진 로컬 회전각일 때 칼끝의 월드 위치를 계산한다.
    private Vector3 GetTipWorldPos(Vector3 euler)
        => GetTipWorldPos(Quaternion.Euler(euler));

    // Quaternion을 직접 받아 칼끝 월드 위치를 계산한다. Slerp 보간 결과를 그대로 전달할 때 사용한다.
    private Vector3 GetTipWorldPos(Quaternion localRot)
    {
        Vector3 pivot = sw08Transform.position;

        // Play Mode에서는 Player가 마우스 방향으로 Y 회전(±90°)하므로 실제 회전을 그대로 사용한다.
        // Edit Mode에서는 Player가 기본 Y=0°이라 호가 깊이 방향(Z)으로 그려져 게임 화면과 달라 보인다.
        // Y=90°(오른쪽 바라보기)로 고정해 Edit Mode에서도 실제 공격 방향과 같은 호를 미리 볼 수 있게 한다.
        Quaternion parent;
        if (Application.isPlaying && sw08Transform.parent != null)
            parent = sw08Transform.parent.rotation;
        else
            parent = Quaternion.Euler(0f, 90f, 0f);

        return pivot + parent * localRot * GetTipLocalOffset();
    }

    // SwordTip 자식 오브젝트의 로컬 오프셋을 반환한다.
    // SwordTip이 없으면 기본값을 사용한다.
    private Vector3 GetTipLocalOffset()
    {
        for (int i = 0; i < sw08Transform.childCount; i++)
        {
            if (sw08Transform.GetChild(i).name == "SwordTip")
                return sw08Transform.GetChild(i).localPosition;
        }
        return new Vector3(0f, 2.05f, 0.01f);
    }
#endif
}
