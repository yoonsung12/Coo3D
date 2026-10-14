using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections;
using UnityEngine;

// 내려찍기 연출: 낮은 시작 위치 -> 들어 올리며 확대 -> 빠르게 내려찍기 -> 압축/반동 -> 즉시 숨김.
// 바닥 물리 충돌이 아니라 내려찍기 종료 시점을 바닥 타격으로 간주한다.
// 공격키 입력 시 SW08을 교번 대각선 베기로 휘두르는 스크립트다.
// 첫 공격은 아래→위, 두 번째 공격은 위→아래로 번갈아 연결된다.
// 공격 중 재입력하면 현재 모션을 즉시 중단하고 다음 방향으로 바로 이어진다.
public class SwordAttack : MonoBehaviour
{
    [Title("SW08 참조")]
    [SerializeField, LabelText("칼 Transform")]
    private Transform sw08Transform;
    // Inspector에서 Player 자식으로 이동시킨 SW08의 Transform을 연결한다.

    [Header("캐릭터 애니메이션")]
    [SerializeField] private Animator playerAnimator;

    [BoxGroup("Swing 0 — 아래→위")]
    [SerializeField, LabelText("준비 각도")]
    private Vector3 readyAngle0 = new Vector3(110f, 0f, -20f);
    // 아래→위 베기의 시작 자세다. 칼끝이 화면 왼쪽 아래를 향한다.
    // X값이 음수이고 절대값이 클수록 칼끝이 더 왼쪽 아래로 내려간다.

    [BoxGroup("Swing 0 — 아래→위")]
    [SerializeField, LabelText("베기 완료 각도")]
    private Vector3 slashAngle0 = new Vector3(50f, 0f, 0f);
    // 아래→위 베기의 완료 자세다. 칼끝이 화면 오른쪽 위를 향한다.
    // 이 값은 Swing 1의 준비 각도와 일치시켜야 두 궤적이 같아진다.

    [Header("베개 효과")]
    [SerializeField] private Transform pillowMesh;
    [SerializeField, LabelText("내려올 때 베개 기울기 (로컬)")]
    private Vector3 pillowStartRotation = new Vector3(30f, 0f, 0f);
    [SerializeField, LabelText("착지 시 베개 각도 (로컬)")]
    private Vector3 pillowImpactRotation = Vector3.zero;
    [SerializeField, Min(0f)] private float attackStartDelay = 0.08f;
    [Header("공격 시작: 중간 지점에서 살짝 들어 올리기")]
    [SerializeField] private Vector3 preLiftStartAngle = new Vector3(75f, 0f, -10f);
    [SerializeField, Min(0.01f)] private float preLiftDuration = 0.24f;
    [SerializeField, LabelText("시작 위치 오프셋 (로컬)")]
    private Vector3 preLiftStartPositionOffset = new Vector3(0f, -0.2f, 0f);
    [SerializeField, LabelText("들어 올렸을 때 크기 배율")]
    private float preLiftScaleMultiplier = 1.2f;
    [SerializeField] private Vector3 squashMultiplier = new Vector3(1.25f, 1.25f, 0.55f);
    [Header("통통 반동 효과")]
    [SerializeField, Min(0f)] private float bounceHeight = 0.13f;
    [SerializeField, Min(0.01f)] private float bounceDuration = 0.17f;
    [SerializeField] private Vector3 bounceStretch = new Vector3(0.94f, 0.94f, 1.17f);
    [Header("내려찍기 후 연출")]
    [SerializeField, Min(0f), LabelText("통통 반동 후 사라지기 전 대기")]
    private float impactHoldDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float squashDuration = 0.065f;
    // 바닥 타격 뒤 말랑한 두 번의 반동이 끝나면 제자리에서 바로 사라진다.
    // SW08 전체가 아닌 Pillow의 시각 Mesh만 이동한다 (히트박스 유지).
    private Vector3 _pillowBaseScale;
    private Vector3 _pillowBaseLocalPosition;
    private Quaternion _pillowBaseLocalRotation;
    private Transform _cachedPillowMesh;
    private Sequence _pillowSequence;

    private PlayerController _playerController;
    // 공격하는 동안 이동을 멈추기 위해 같은 Player 오브젝트의 PlayerController를 가져와 둔다.
    // 같은 오브젝트에 붙어 있으므로 Inspector 연결 없이 GetComponent로 찾는다.

    private void Awake()
    {
        CachePillowScale();
        _playerController = GetComponent<PlayerController>();
    }

    private void OnDisable()
    {
        // 공격 도중 오브젝트가 꺼져도 이동 정지 상태가 남지 않도록 풀어준다.
        _playerController?.SetAttackLock(false);
    }

    private void CachePillowScale()
    {
        if (pillowMesh == null) return;
        if (_cachedPillowMesh == pillowMesh) return;
        _cachedPillowMesh = pillowMesh;
        _pillowBaseScale = pillowMesh.localScale;
        _pillowBaseLocalPosition = pillowMesh.localPosition;
        _pillowBaseLocalRotation = pillowMesh.localRotation;
    }

    [BoxGroup("Swing 1 — 위→아래")]
    [SerializeField, LabelText("준비 각도")]
    private Vector3 readyAngle1 = new Vector3(50f, 0f, 0f);
    // 위→아래 베기의 시작 자세다. Swing 0 완료 위치(오른쪽 위)에서 이어진다.
    // Swing 0의 베기 완료 각도와 일치해야 같은 궤적을 공유한다.

    [BoxGroup("Swing 1 — 위→아래")]
    [SerializeField, LabelText("베기 완료 각도")]
    private Vector3 slashAngle1 = new Vector3(110f, 0f, -20f);
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

    [Title("사운드")]
    [SerializeField, LabelText("쉬잉 사운드 클립")]
    private AudioClip swishClip;
    // 칼을 휘두를 때 재생할 사운드 클립이다. Inspector에서 연결한다.
    // 비워두면 소리 없이 연출만 동작한다.

    [Title("타격 판정")]
    [SerializeField, LabelText("칼날 히트박스")]
    private SwordHitbox swordHitbox;
    // SW08에 부착된 SwordHitbox 컴포넌트를 연결한다.
    // 비워두면 타격 판정 없이 연출만 동작한다.

    [Title("콤보 리셋 설정")]
    [SerializeField, LabelText("콤보 리셋 시간")]
    private float comboResetTime = 2f;
    // 마지막 공격 후 이 시간 동안 다음 공격이 없으면 콤보를 처음(아래→위)으로 되돌린다.

    [SerializeField, LabelText("공격 간격(초)")]
    private float attackCooldown = 1f;
    // 한 번 휘두른 뒤 이 시간이 지나야 다음 공격이 나간다. 그 사이의 클릭은 무시된다.
    // 값이 작을수록 연타가 빨라지고, 0이면 예전처럼 클릭할 때마다 바로 휘두른다.

    [Title("런타임 상태 (읽기 전용)")]
    [ReadOnly, ShowInInspector, LabelText("다음 공격 방향")]
    private string NextSwingLabel => _nextSwingIndex == 0 ? "아래→위 (Swing 0)" : "위→아래 (Swing 1)";

    [ReadOnly, ShowInInspector, LabelText("공격 중")]
    private bool _isAttacking;

    [ReadOnly, ShowInInspector, LabelText("다음 공격까지 남은 시간")]
    private float CooldownRemaining => Mathf.Max(0f, attackCooldown - (Time.time - _lastAttackTime));

    private float _lastAttackTime = -999f;
    // 마지막으로 공격이 실제로 나간 시간이다. 공격 간격 판정에 사용한다.

    // NFBTEnemyAI의 Counter(반격) 분기가 Player의 공격 시작/종료 타이밍을 읽기 위해 사용하는 프로퍼티다.
    public bool IsAttacking => _isAttacking;

    // 공격이 실제로 시작될 때마다 발행된다. PillowDurability가 이 이벤트를 구독해 내구도를 소모한다.
    public event Action OnAttackFired;

    // 0 = 아래→위 (Swing 0), 1 = 위→아래 (Swing 1)
    // 공격키를 누를 때마다 즉시 전환된다.
    private int _nextSwingIndex = 0;

    private Sequence _slashSequence;
    private Coroutine _comboResetCoroutine;

    private void OnDestroy()
    {
        _slashSequence?.Kill();
        _pillowSequence?.Kill();
        if (_comboResetCoroutine != null)
            StopCoroutine(_comboResetCoroutine);
    }

    // ToolManager에서 공격 모드(도구 미장착)로 공격키가 눌릴 때 호출된다.
    public void TryAttack()
    {
        // 공격 간격이 아직 안 지났으면 입력을 무시한다.
        // 이벤트 발행보다 앞에서 막아야 무시된 클릭이 베개 내구도를 소모하지 않는다.
        if (Time.time - _lastAttackTime < attackCooldown) return;
        _lastAttackTime = Time.time;

        OnAttackFired?.Invoke();

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Attack");
        }

        // 이전 공격이 진행 중이면 즉시 중단한다.
        // SW08은 현재 각도에 멈추며, 새 Sequence가 그 위치에서 바로 이어진다.
        _slashSequence?.Kill();
        _pillowSequence?.Kill();
        ResetPillowVisual();

        // 누른 시점에 즉시 다음 방향을 결정하고 인덱스를 전환한다.
        int swingIndex = 1; // 언제나 위에서 아래로 내려찍기

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

        // 공격 연출이 끝날 때(Sequence OnKill)까지 땅 위 이동을 멈춘다.
        // 이전 Sequence의 Kill이 끝난 뒤에 호출되므로, 이전 공격의 해제가 이 잠금을 덮어쓰지 않는다.
        _playerController?.SetAttackLock(true);

        // 이전 연출이 남아 있을 경우를 대비해 빛을 꺼진 상태로 초기화한다.
        if (tipLight != null) tipLight.intensity = 0f;

        CachePillowScale();
        sw08Transform.localRotation = Quaternion.Euler(preLiftStartAngle);
        if (pillowMesh != null)
        {
            pillowMesh.localPosition = _pillowBaseLocalPosition + preLiftStartPositionOffset;
            pillowMesh.localScale = _pillowBaseScale;
            pillowMesh.localRotation = Quaternion.Euler(pillowStartRotation);
        }
        Vector3 readyAngle = index == 0 ? readyAngle0 : readyAngle1;
        Vector3 slashEndAngle = index == 0 ? slashAngle0 : slashAngle1;

        // 빛 연출 Insert 위치를 계산한다. Sequence 시작 기준 각 구간의 시작 시간이다.
        float slashStartTime = preLiftDuration + attackStartDelay;
        float slashEndTime = slashStartTime + slashDuration;

        _slashSequence = DOTween.Sequence()
            // 시작 시 중간 자세에 두었다가 준비 자세로 살짝 들어 올린다.
            .AppendCallback(() => sw08Transform.localRotation = Quaternion.Euler(preLiftStartAngle))
            .Append(sw08Transform.DOLocalRotateQuaternion(Quaternion.Euler(readyAngle), preLiftDuration)
                .SetEase(Ease.OutSine))
            .AppendInterval(attackStartDelay) // 살짝 멈춘 후 내려찍기
                                              // 베기 시작 직전에 준비 각도로 정확히 스냅해 보간 오차를 제거한다.
                                              // 이렇게 해야 이후 슬래시 트윈의 시작점이 기즈모 호의 시작점과 정확히 일치한다.
            .AppendCallback(() =>
            {
                sw08Transform.localRotation = Quaternion.Euler(readyAngle);
                swordHitbox?.EnableHitbox();

                if (swordTrail != null) swordTrail.emitting = true;
                if (swishClip != null)
                    AudioSource.PlayClipAtPoint(swishClip, sw08Transform.position);
                // 베기 시작 순간 칼 위치에서 쉬잉 소리를 재생한다.

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
                // 찌그러짐은 아래 InsertCallback에서 한 번만 실행
            })
            // 압축+두 번 반동이 끝나기를 기다린 다음, 짧게 유지하고 '퍽' 하고 숨긴다.
            .AppendInterval(squashDuration + 0.025f + bounceDuration + 0.11f + impactHoldDuration)
            .AppendCallback(() =>
            {
                if (pillowMesh != null) pillowMesh.gameObject.SetActive(false);
                // 베개를 숨긴 뒤에만 회전 피벗을 원래 자세로 옮긴다.
                sw08Transform.localRotation = Quaternion.Euler(restAngle);
            })
            .OnComplete(() => _isAttacking = false)
            // 콤보 캔슬 시에도 히트박스, 잔상, 빛을 반드시 정리한다.
            // OnKill은 자연 완료 시에도 호출되므로 모두 멱등성 있게 처리한다.
            .OnKill(() =>
            {
                // 공격이 끝나거나 끊기면 이동 정지를 푼다. 다음 공격은 이 Kill 이후에 다시 잠근다.
                _playerController?.SetAttackLock(false);
                swordHitbox?.DisableHitbox();
                if (swordTrail != null) swordTrail.emitting = false;
                if (tipLight != null) tipLight.intensity = 0f;
            });

        // 휘두르기가 끝나는 순간 말랑하게 눌렸다 복원됨 (실제 충돌 감지는 아님)
        _slashSequence.InsertCallback(slashEndTime, PlayPillowSquash);
        if (pillowMesh != null)
        {
            // 처음엔 낮게 등장해 위로 들리는 동안만 커지고, 내려찍을 때 원래 크기로 복귀.
            _slashSequence.Insert(0f,
                pillowMesh.DOLocalMove(_pillowBaseLocalPosition, preLiftDuration).SetEase(Ease.OutSine));
            _slashSequence.Insert(0f,
                pillowMesh.DOScale(_pillowBaseScale * Mathf.Max(0.01f, preLiftScaleMultiplier), preLiftDuration)
                    .SetEase(Ease.OutSine));
            _slashSequence.Insert(slashStartTime,
                pillowMesh.DOScale(_pillowBaseScale, slashDuration).SetEase(Ease.InQuad));
            _slashSequence.Insert(slashStartTime,
                pillowMesh.DOLocalRotate(pillowImpactRotation, slashDuration).SetEase(Ease.InOutSine));
        }

        // 빛 반짝임: Sequence에 Insert해 캔슬 시 자동으로 함께 정리되도록 한다.
        // 베기 시작 순간 최대 강도로 점등하고, 복귀 구간 동안 서서히 소멸한다.
        if (tipLight != null)
        {
            _slashSequence
                .Insert(slashStartTime, tipLight.DOIntensity(lightMaxIntensity, 0.02f))
                .Insert(slashEndTime, tipLight.DOIntensity(0f, returnDuration));
        }
    }
    private void ResetPillowVisual()
    {
        if (pillowMesh == null) return;
        CachePillowScale();
        pillowMesh.gameObject.SetActive(true);
        pillowMesh.localPosition = _pillowBaseLocalPosition;
        pillowMesh.localScale = _pillowBaseScale;
        pillowMesh.localRotation = Quaternion.Euler(pillowStartRotation);
    }

    private void PlayPillowSquash()
    {
        if (pillowMesh == null)
        {
            Debug.LogWarning("[SwordAttack] Pillow Mesh를 Inspector에 연결하세요.", this);
            return;
        }
        CachePillowScale();
        _pillowSequence?.Kill();
        pillowMesh.localScale = _pillowBaseScale;
        pillowMesh.localPosition = _pillowBaseLocalPosition;
        pillowMesh.localRotation = Quaternion.Euler(pillowImpactRotation);

        // 퍽! 납작해진 뒤 통통 두 번 반동. 찌그러짐+반동은
        // 전체 impactHoldDuration 안에서 재생되며, 이후에 상승/소멸한다.
        float bounceUp = bounceDuration * 0.40f;
        float bounceDown = bounceDuration * 0.60f;
        Vector3 compressed = Vector3.Scale(_pillowBaseScale, squashMultiplier);
        Vector3 stretched = Vector3.Scale(_pillowBaseScale, bounceStretch);
        Vector3 firstJump = _pillowBaseLocalPosition + Vector3.up * bounceHeight;
        Vector3 secondJump = _pillowBaseLocalPosition + Vector3.up * bounceHeight * 0.28f;

        _pillowSequence = DOTween.Sequence()
            .Append(pillowMesh.DOScale(compressed, squashDuration).SetEase(Ease.OutQuad))
            .AppendInterval(0.025f)
            .Append(pillowMesh.DOScale(stretched, bounceUp).SetEase(Ease.OutQuad))
            .Join(pillowMesh.DOLocalMove(firstJump, bounceUp).SetEase(Ease.OutQuad))
            .Append(pillowMesh.DOScale(_pillowBaseScale, bounceDown).SetEase(Ease.OutBounce))
            .Join(pillowMesh.DOLocalMove(_pillowBaseLocalPosition, bounceDown).SetEase(Ease.InQuad))
            .Append(pillowMesh.DOLocalMove(secondJump, 0.05f).SetEase(Ease.OutQuad))
            .Append(pillowMesh.DOLocalMove(_pillowBaseLocalPosition, 0.06f).SetEase(Ease.InQuad));
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
        Quaternion toQ = Quaternion.Euler(toEuler);

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
        Vector3 endTip = GetTipWorldPos(toEuler);
        Gizmos.DrawSphere(startTip, 0.045f);
        Gizmos.DrawSphere(endTip, 0.045f);

        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(startTip + Vector3.up * 0.1f, $"{swingName} 준비");
        UnityEditor.Handles.Label(endTip + Vector3.up * 0.1f, $"{swingName} 완료");
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

// CustomEditor / OdinEditor 상속 방식은 Odin과 충돌해 OnSceneGUI 호출이 불안정하다.
// SceneView.duringSceneGui 이벤트를 직접 구독하면 인스펙터 시스템과 완전히 독립적으로 동작한다.
#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
public static class SwordAttackSceneHandles
{
    private static UnityEditor.SerializedObject _so;
    private static SwordAttack _lastTarget;

    static SwordAttackSceneHandles()
    {
        UnityEditor.SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(UnityEditor.SceneView sceneView)
    {
        var go = UnityEditor.Selection.activeGameObject;
        if (go == null) return;

        var attack = go.GetComponent<SwordAttack>();
        if (attack == null) return;

        // 선택 오브젝트가 바뀌면 SerializedObject를 갱신한다.
        if (attack != _lastTarget || _so == null)
        {
            _lastTarget = attack;
            _so = new UnityEditor.SerializedObject(attack);
        }

        _so.Update();

        DrawAngleHandle(_so.FindProperty("readyAngle0"), new Color(1f, 0.2f, 0.2f, 1f));
        DrawAngleHandle(_so.FindProperty("slashAngle0"), new Color(1f, 0.2f, 0.2f, 1f));
        DrawAngleHandle(_so.FindProperty("readyAngle1"), new Color(1f, 0.55f, 0.1f, 1f));
        DrawAngleHandle(_so.FindProperty("slashAngle1"), new Color(1f, 0.55f, 0.1f, 1f));

        _so.ApplyModifiedProperties();
    }

    private static void DrawAngleHandle(UnityEditor.SerializedProperty prop, Color color)
    {
        if (prop == null) return;

        var sw08Prop = _so.FindProperty("sw08Transform");
        if (sw08Prop?.objectReferenceValue == null) return;
        var sw08 = (Transform)sw08Prop.objectReferenceValue;

        Vector3 tipPos = CalcTipWorldPos(sw08, prop.vector3Value);
        float size = UnityEditor.HandleUtility.GetHandleSize(tipPos) * 0.12f;

        UnityEditor.Handles.color = color;

        UnityEditor.EditorGUI.BeginChangeCheck();
        Vector3 newPos = UnityEditor.Handles.FreeMoveHandle(
            tipPos, size, Vector3.zero, UnityEditor.Handles.SphereHandleCap);

        if (UnityEditor.EditorGUI.EndChangeCheck())
            prop.vector3Value = WorldPosToLocalEuler(sw08, newPos);
    }

    private static Vector3 CalcTipWorldPos(Transform sw08, Vector3 euler)
    {
        // 에디터 모드에서는 Y=90°로 고정해 실제 공격 방향과 동일하게 미리 본다.
        Quaternion parent = Application.isPlaying && sw08.parent != null
            ? sw08.parent.rotation
            : Quaternion.Euler(0f, 90f, 0f);
        return sw08.position + parent * Quaternion.Euler(euler) * GetTipLocalOffset(sw08);
    }



    private static Vector3 WorldPosToLocalEuler(Transform sw08, Vector3 newTipWorldPos)
    {
        Quaternion parent = Application.isPlaying && sw08.parent != null
            ? sw08.parent.rotation
            : Quaternion.Euler(0f, 90f, 0f);

        Vector3 localDir = Quaternion.Inverse(parent) * (newTipWorldPos - sw08.position);
        localDir = localDir.normalized;

        // 오일러 ZXY 역변환: Z = asin(-dir.x), X = atan2(dir.z, dir.y)
        float zAngle = Mathf.Asin(Mathf.Clamp(-localDir.x, -1f, 1f)) * Mathf.Rad2Deg;
        float xAngle = Mathf.Atan2(localDir.z, localDir.y) * Mathf.Rad2Deg;

        return new Vector3(xAngle, 0f, zAngle);
    }

    private static Vector3 GetTipLocalOffset(Transform sw08)
    {
        for (int i = 0; i < sw08.childCount; i++)
        {
            if (sw08.GetChild(i).name == "SwordTip")
                return sw08.GetChild(i).localPosition;
        }
        return new Vector3(0f, 2.05f, 0.01f);
    }
}
#endif
