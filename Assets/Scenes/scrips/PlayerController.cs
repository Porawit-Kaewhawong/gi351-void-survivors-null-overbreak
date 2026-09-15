using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Jump")]
    public float jumpHeight = 2f;
    public float gravity = -20f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.05f;
    public LayerMask groundLayer;

    [Header("Camera")]
    public Transform cameraTransform;

    private CharacterController characterController;
    private Vector3 velocity;

    private bool isGrounded;
    private float coyoteTime = 0.15f;
    private float coyoteTimer;

    private void Start()
    {
        // Get the CharacterController component.
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        CheckGround();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void CheckGround()
    {
        // Get the lowest point of the CharacterController.
        float bottom = characterController.bounds.min.y;

        // Create a ground check position slightly above the bottom.
        Vector3 checkPosition = new Vector3(
            characterController.bounds.center.x,
            bottom + groundCheckOffset,
            characterController.bounds.center.z
        );

        // Check if the player is touching the ground.
        isGrounded = Physics.CheckSphere(
            checkPosition,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        // Give the player a short time to jump after leaving the ground.
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    private void HandleMovement()
    {
        // Get keyboard input from WASD or Arrow Keys.
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Get camera directions.
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // Keep movement on the ground.
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        // Calculate movement direction.
        Vector3 moveDirection =
            cameraForward * vertical +
            cameraRight * horizontal;

        // Prevent diagonal movement from being faster.
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        // Move the player.
        characterController.Move(
            moveDirection * moveSpeed * Time.deltaTime
        );
    }

    private void HandleJump()
    {
        // Keep the player attached to the ground.
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        // Jump when Space is pressed.
        if (Input.GetKeyDown(KeyCode.Space) && coyoteTimer > 0f)
        {
            velocity.y = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );

            // Prevent another jump immediately.
            coyoteTimer = 0f;
        }
    }

    private void ApplyGravity()
    {
        // Apply gravity every frame.
        velocity.y += gravity * Time.deltaTime;

        // Move the player vertically.
        characterController.Move(
            velocity * Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        // Show the ground check area in the Scene view.
        if (characterController == null)
            return;

        float bottom = characterController.bounds.min.y;

        Vector3 checkPosition = new Vector3(
            characterController.bounds.center.x,
            bottom + groundCheckOffset,
            characterController.bounds.center.z
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            checkPosition,
            groundCheckRadius
        );
    }
}