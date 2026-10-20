
using UnityEngine;

public class PoofBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        // 카메라의 방향에 맞춰 정면 유지
        transform.rotation = targetCamera.transform.rotation;
    }
}
