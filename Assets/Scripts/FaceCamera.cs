using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    public enum BillboardType
    {
        CameraParallel, // Matches the camera's rotation (keeps sprite flat against screen)
        LookAtCamera    // Rotates toward camera position (good for cylindrical Y-lock)
    }

    [Header("Settings")]
    public BillboardType billboardType = BillboardType.CameraParallel;
    public Camera mainCamera;
    public bool lockYAxis = true;
    public float rotationSpeed = 10f; // Set to 0 for instant snap

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        Vector3 targetDirection = Vector3.forward;

        if (billboardType == BillboardType.CameraParallel)
        {
            // Align sprite with camera's forward vector
            targetDirection = mainCamera.transform.forward;
        }
        else if (billboardType == BillboardType.LookAtCamera)
        {
            // Calculate direction from camera to object
            targetDirection = transform.position - mainCamera.transform.position;
        }

        if (lockYAxis)
        {
            targetDirection.y = 0; // Keep sprite perfectly upright
        }

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

            if (rotationSpeed > 0f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            else
            {
                transform.rotation = targetRotation; // Instant snap
            }
        }
    }
}