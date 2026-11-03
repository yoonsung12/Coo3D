
using UnityEngine;
using Sirenix.OdinInspector;

[DefaultExecutionOrder(1000)]
public class UmbrellaRotationStabilizer : MonoBehaviour
{
    [Title("연결")]
    [SerializeField]
    private UmbrellaTool umbrellaTool;

    [SerializeField]
    private PlayerController player;

    [Title("하강 위치 설정")]
    [SerializeField, LabelText("플레이어 기준 위치")]
    private Vector3 glideLocalPosition = new Vector3(0f, 1.5f, 0f);

    [SerializeField, LabelText("위치 보정 속도")]
    private float positionSmoothSpeed = 15f;

    [Title("하강 회전 설정")]
    [SerializeField, LabelText("하강 회전값")]
    private Vector3 glideWorldRotation;

    [SerializeField, LabelText("회전 보정 속도")]
    private float rotationSmoothSpeed = 12f;

    [Title("손 따라가기")]
    [SerializeField, LabelText("손 따라가기")]
    private bool followHand = true;
    // 켜면 활공 중 우산이 오른손(원래 부모 뼈)을 따라가서 손이 항상 우산대를 쥐고 있다.
    // 끄면 예전처럼 위의 "플레이어 기준 위치"(glideLocalPosition)에 고정된다.

    [SerializeField, LabelText("손잡이 위치"), ShowIf(nameof(followHand))]
    private float handGripOffset = 0.7f;
    // 우산 중심에서 아래로 이 거리(m)만큼 떨어진 곳을 손이 잡는다.
    // 값이 커질수록 우산이 손보다 더 위로 올라간다. (우산대 끝은 중심에서 약 0.8m 아래)

    [Title("런타임 상태")]
    [ReadOnly, ShowInInspector]
    private bool isGliding;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private bool wasGliding;

    private Vector3 smoothedOffset;
    // 플레이어 위치 기준 우산의 상대 위치다. 월드 위치 대신 이 값을 보간해야 플레이어가 움직여도 우산이 뒤처지지 않는다.

    private void Awake()
    {
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;

        if (umbrellaTool == null)
            umbrellaTool = GetComponentInParent<UmbrellaTool>();

        if (player == null)
            player = GetComponentInParent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (umbrellaTool == null || player == null)
            return;

        isGliding = umbrellaTool.IsOpen && !player.IsGrounded;

        if (isGliding)
        {
            // 플레이어 기준 목표 위치
            Vector3 targetPosition = followHand && originalParent != null
                ? originalParent.position + Vector3.up * handGripOffset
                : player.transform.TransformPoint(glideLocalPosition);
            // 손 따라가기: 오른손 뼈 위치에서 손잡이 거리만큼 위를 목표로 잡는다. 활공 애니메이션에서 손이 좌우로 움직여도 우산대를 놓치지 않는다.

            Vector3 targetOffset = targetPosition - player.transform.position;
            if (!wasGliding)
                smoothedOffset = transform.position - player.transform.position;
            // 활공을 막 시작한 프레임에는 현재 위치에서 출발해 튀지 않게 한다.

            smoothedOffset = Vector3.Lerp(
                smoothedOffset,
                targetOffset,
                1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime)
            );
            transform.position = player.transform.position + smoothedOffset;
            // 플레이어 기준 상대 위치만 부드럽게 보간해 손 떨림은 걸러내고, 플레이어 이동에 따른 뒤처짐은 없앤다.

            // 기존 하강 회전 보정
            Quaternion targetRotation =
                Quaternion.Euler(glideWorldRotation);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(
                    -rotationSmoothSpeed * Time.deltaTime
                )
            );
        }
        else if (wasGliding)
        {
            // 하강이 끝나면 기존 손 Bone 기준 위치/회전 복구
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
        }

        wasGliding = isGliding;
    }

    [Button("현재 위치를 하강용으로 저장")]
    private void SaveCurrentPosition()
    {
        if (player == null)
            player = GetComponentInParent<PlayerController>();

        if (player == null)
        {
            Debug.LogWarning("[Umbrella] PlayerController 연결 필요");
            return;
        }

        glideLocalPosition =
            player.transform.InverseTransformPoint(transform.position);

        Debug.Log(
            "[Umbrella] 하강 위치 저장: " + glideLocalPosition
        );
    }

    [Button("현재 회전을 하강용으로 저장")]
    private void SaveCurrentRotation()
    {
        glideWorldRotation = transform.eulerAngles;

        Debug.Log(
            "[Umbrella] 하강 회전 저장: " + glideWorldRotation
        );
    }
}
