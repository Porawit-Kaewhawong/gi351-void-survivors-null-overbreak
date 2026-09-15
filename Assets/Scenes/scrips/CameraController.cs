using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera")]
    public float distance = 6f;
    public float targetHeight = 1f;

    [Header("Mouse")]
    public float mouseSensitivity = 2.5f;

    [Header("Vertical Rotation")]
    public float minPitch = -15f;
    public float maxPitch = 60f;

    [Header("Follow")]
    public float followSpeed = 12f;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        // Lock the mouse to the center of the screen.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Start behind the player.
        yaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleMouseRotation();
        FollowPlayer();
    }

    private void HandleMouseRotation()
    {
        // Get mouse movement.
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Rotate around the player horizontally.
        yaw += mouseX * mouseSensitivity;

        // Rotate the camera vertically.
        pitch -= mouseY * mouseSensitivity;

        // Limit vertical rotation.
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void FollowPlayer()
    {
        // Create the camera rotation.
        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        // Set the exact point the camera follows.
        Vector3 targetPosition =
            target.position +
            Vector3.up * targetHeight;

        // Position the camera behind the player.
        Vector3 desiredPosition =
            targetPosition -
            rotation * Vector3.forward * distance;

        // Smoothly follow the player.
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        // Keep the player locked in the center.
        transform.LookAt(targetPosition);
    }
}