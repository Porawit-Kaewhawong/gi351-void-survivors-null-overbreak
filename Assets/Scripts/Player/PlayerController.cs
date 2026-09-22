using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Base Movement Settings")]
    public float baseMoveSpeed = 5f;

    [Header("Base Jump Settings")]
    public float baseJumpHeight = 2f;
    public float gravity = -20f;

    [Header("Base Pickup Radius Settings")]
    public float basePickupRadius = 2f;

    [Header("Health & Shield Settings")]
    public float baseMaxHealth = 100f;
    [Tooltip("Base shield value. Shield buff options add flat numbers directly to this amount.")]
    public float baseMaxShield = 0f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.05f;
    public LayerMask groundLayer;

    [Header("Camera")]
    public Transform cameraTransform;

    // Current Dynamic State
    public float CurrentHealth { get; private set; }
    public float CurrentShield { get; private set; }

    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;
    private float coyoteTime = 0.15f;
    private float coyoteTimer;
    private bool isDead = false;

    // --- STAT CALCULATIONS ---

    // Move Speed: Percentage scaling
    public float CurrentMoveSpeed
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.MoveSpeed) : 0f;
            return baseMoveSpeed * (1f + (bonus / 100f));
        }
    }

    // Jump Height: Percentage scaling
    public float CurrentJumpHeight
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.JumpHeight) : 0f;
            return baseJumpHeight * (1f + (bonus / 100f));
        }
    }

    // Pickup Radius: Percentage scaling
    public float CurrentPickupRadius
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.PickupRadius) : 0f;
            return basePickupRadius * (1f + (bonus / 100f));
        }
    }

    // Max Health: Fixed to base value (health buff options removed)
    public float CurrentMaxHealth => baseMaxHealth;

    // Max Shield: Flat additive numerical bonus
    public float CurrentMaxShield
    {
        get
        {
            float flatShieldBonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.MaxShield) : 0f;
            return baseMaxShield + flatShieldBonus;
        }
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        ResetHealthAndShield();
    }

    private void Update()
    {
        if (isDead) return;

        CheckGround();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    // --- HEALTH & DAMAGE ---

    public void ResetHealthAndShield()
    {
        isDead = false;
        CurrentHealth = CurrentMaxHealth;
        CurrentShield = CurrentMaxShield;
        Debug.Log($"[Player] Stats Initialized -> HP: {CurrentHealth}/{CurrentMaxHealth} | Shield: {CurrentShield}/{CurrentMaxShield}");
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f) return;

        // Shield absorbs incoming damage first
        if (CurrentShield > 0f)
        {
            float shieldDamage = Mathf.Min(CurrentShield, damage);
            CurrentShield -= shieldDamage;
            damage -= shieldDamage;
        }

        // Remaining damage damages Health
        if (damage > 0f)
        {
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0f, CurrentHealth);
        }

        Debug.Log($"[Player Damaged] Current HP: {CurrentHealth} | Shield: {CurrentShield}");

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, CurrentMaxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
    }

    // --- MOVEMENT & PHYSICS ---

    private void CheckGround()
    {
        float bottom = characterController.bounds.min.y;

        Vector3 checkPosition = new Vector3(
            characterController.bounds.center.x,
            bottom + groundCheckOffset,
            characterController.bounds.center.z
        );

        isGrounded = Physics.CheckSphere(
            checkPosition,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

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
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * vertical + cameraRight * horizontal;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        characterController.Move(moveDirection * CurrentMoveSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (Input.GetKeyDown(KeyCode.Space) && coyoteTimer > 0f)
        {
            velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
            coyoteTimer = 0f;
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (characterController == null) return;

        float bottom = characterController.bounds.min.y;
        Vector3 checkPosition = new Vector3(
            characterController.bounds.center.x,
            bottom + groundCheckOffset,
            characterController.bounds.center.z
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentPickupRadius);
    }

    // --- JUMP PAD / LAUNCH API ---

    /// <summary>
    /// Sets vertical velocity directly, allowing Jump Pads or Launchers to override gravity.
    /// </summary>
    public void Bounce(float force)
    {
        velocity.y = force;
        coyoteTimer = 0f;
    }
}