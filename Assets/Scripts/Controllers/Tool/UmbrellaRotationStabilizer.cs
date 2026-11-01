
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

    [Title("런타임 상태")]
    [ReadOnly, ShowInInspector]
    private bool isGliding;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private bool wasGliding;

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
            Vector3 targetPosition =
                player.transform.TransformPoint(glideLocalPosition);

            // 손 Bone의 위치 흔들림을 무시하고
            // 플레이어 기준 위치로 부드럽게 이동
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                1f - Mathf.Exp(
                    -positionSmoothSpeed * Time.deltaTime
                )
            );

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
