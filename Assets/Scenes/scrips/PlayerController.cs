using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 200f;
    [SerializeField] private float cameraMinX = -80f;
    [SerializeField] private float cameraMaxX = 80f;

    [Header("Physics Settings")]
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;

    private float cameraRotationX;
    private float cameraRotationY;

    private Vector3 velocity;


    private void Awake()
    {
        //Get the CharacterController component attached to the player.
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        //Lock the cursor to the center of the screen for camera control.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    private void Update()
    {
        HandleMovement();
        HandleCameraRotation();
        HandleGravity();
    }


    private void HandleMovement()
    {
        //Get keyboard input from the player.
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        //Create movement direction based on the player's forward and right direction.
        Vector3 moveDirection =
            transform.right * horizontal +
            transform.forward * vertical;

        //Prevent diagonal movement from being faster.
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        //Move the player using CharacterController collision detection.
        characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

        //Rotate the player toward the movement direction.
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }


    private void HandleCameraRotation()
    {
        //Get mouse movement for camera rotation.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        //Rotate the player horizontally based on mouse movement.
        cameraRotationY += mouseX;

        //Rotate the camera vertically and limit the rotation angle.
        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(
            cameraRotationX,
            cameraMinX,
            cameraMaxX
        );

        //Apply vertical rotation to the camera.
        cameraTransform.localRotation =
            Quaternion.Euler(cameraRotationX, 0f, 0f);

        //Apply horizontal rotation to the player.
        transform.rotation =
            Quaternion.Euler(0f, cameraRotationY, 0f);
    }


    private void HandleGravity()
    {
        //Keep the player grounded and apply gravity when in the air.
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        //Apply gravity over time.
        velocity.y += gravity * Time.deltaTime;

        //Move the player vertically.
        characterController.Move(velocity * Time.deltaTime);
    }
}
