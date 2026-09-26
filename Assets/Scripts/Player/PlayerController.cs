using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SpriteDirectionData
{
    public Sprite idleSprite;
    public List<Sprite> walkFrames = new List<Sprite>();
}

public class PlayerController : MonoBehaviour
{
    [Header("Base Movement Settings")]
    public float baseMoveSpeed = 5f;

    [Header("Base Jump Settings")]
    public float baseJumpHeight = 2f;
    public float gravity = -20f;

    [Header("Fall Death Settings")]
    [Tooltip("If the player drops below this Y height in the world, Die() is automatically triggered.")]
    public float killYThreshold = -20f;

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

    [Header("2D Sprite & 8-Direction Animations")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Frame rate for walking animation cycles.")]
    public float animationFPS = 8f;
    [Tooltip("If checked, rotates the SpriteRenderer to face the camera in 3D space.")]
    public bool billboardToCamera = true;

    [Space(10)]
    public SpriteDirectionData north;
    public SpriteDirectionData northEast;
    public SpriteDirectionData east;
    public SpriteDirectionData southEast;
    public SpriteDirectionData south;
    public SpriteDirectionData southWest;
    public SpriteDirectionData west;
    public SpriteDirectionData northWest;

    [Header("UI Visual Feedback")]
    [Tooltip("UI Image covering the screen used for hit color flashes (e.g. full-screen panel with raycast target disabled).")]
    public Image damageOverlayImage;
    public Color shieldHitColor = new Color(0f, 0.5f, 1f, 0.4f); // Blue tint for shield hit
    public Color healthHitColor = new Color(1f, 0f, 0f, 0.4f); // Red tint for health hit
    public float flashDuration = 0.25f;

    [Header("Audio & SFX Slots")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip shieldHitSound;
    public AudioClip shieldBreakSound;
    public AudioClip healthHitSound;
    public AudioClip playerDeathSound;

    [Header("Heartbeat Settings")]
    public AudioClip heartbeatSound;
    [Range(0f, 1f)] public float heartbeatVolume = 0.8f;
    [Tooltip("Time between beats at near 100% health.")]
    public float maxHeartbeatInterval = 1.2f;
    [Tooltip("Time between beats at near 0% health.")]
    public float minHeartbeatInterval = 0.25f;

    // Current Dynamic State
    public float CurrentHealth { get; private set; }
    public float CurrentShield { get; private set; }

    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;
    private float coyoteTime = 0.15f;
    private float coyoteTimer;
    private int extraJumpsRemaining;
    private bool isDead = false;

    // Audio & Visual internal handles
    private AudioSource heartbeatAudioSource;
    private float heartbeatTimer;
    private Coroutine flashCoroutine;

    // Animation internal handles
    private int lastDirectionIndex = 4; // Default to South
    private float animTimer = 0f;
    private int currentFrameIndex = 0;

    // --- STAT CALCULATIONS ---

    public float CurrentMoveSpeed
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.MoveSpeed) : 0f;
            return baseMoveSpeed * (1f + (bonus / 100f));
        }
    }

    public float CurrentJumpHeight
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.JumpHeight) : 0f;
            return baseJumpHeight * (1f + (bonus / 100f));
        }
    }

    public float CurrentPickupRadius
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.PickupRadius) : 0f;
            return basePickupRadius * (1f + (bonus / 100f));
        }
    }

    public int MaxExtraJumps
    {
        get
        {
            return GameManager.Instance != null
                ? Mathf.FloorToInt(GameManager.Instance.GetTotalBuffValue(StatType.ExtraJumps))
                : 0;
        }
    }

    public float CurrentMaxHealth => baseMaxHealth;

    public float CurrentMaxShield
    {
        get
        {
            float flatShieldBonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.MaxShield) : 0f;
            return baseMaxShield + flatShieldBonus;
        }
    }

    private void Awake()
    {
        heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        heartbeatAudioSource.playOnAwake = false;
        heartbeatAudioSource.loop = false;
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            spriteRenderer.receiveShadows = true;

            // Sync _BaseMap for URP Lit ShadowCaster pass
            if (spriteRenderer.sprite != null && spriteRenderer.material != null)
            {
                spriteRenderer.material.SetTexture("_BaseMap", spriteRenderer.sprite.texture);
            }
        }

        ResetHealthAndShield();

        if (damageOverlayImage != null)
        {
            damageOverlayImage.color = Color.clear;
        }
    }

    private void Update()
    {
        if (isDead) return;

        CheckFallDeath();
        CheckGround();

        Vector3 moveInput = HandleMovement();

        HandleJump();
        ApplyGravity();
        HandleSpriteAnimation(moveInput);
        HandleHeartbeatSound();
    }

    private void LateUpdate()
    {
        // Keep sprite billboarded facing camera in 3D space if enabled
        if (billboardToCamera && spriteRenderer != null && cameraTransform != null)
        {
            spriteRenderer.transform.rotation = Quaternion.LookRotation(spriteRenderer.transform.position - cameraTransform.position);
        }
    }

    // --- FALL DEATH CHECK ---

    private void CheckFallDeath()
    {
        if (transform.position.y < killYThreshold)
        {
            Debug.Log($"[Player] Fell below height limit ({killYThreshold}m). Triggering death.");
            Die();
        }
    }

    // --- HEALTH & DAMAGE ---

    public void ResetHealthAndShield()
    {
        isDead = false;
        CurrentHealth = CurrentMaxHealth;
        CurrentShield = CurrentMaxShield;
        heartbeatTimer = 0f;

        if (damageOverlayImage != null)
        {
            damageOverlayImage.color = Color.clear;
        }

        Debug.Log($"[Player] Stats Initialized -> HP: {CurrentHealth}/{CurrentMaxHealth} | Shield: {CurrentShield}/{CurrentMaxShield}");
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f) return;

        float previousShield = CurrentShield;
        bool tookShieldDamage = false;
        bool tookHealthDamage = false;

        if (CurrentShield > 0f)
        {
            tookShieldDamage = true;
            float shieldDamage = Mathf.Min(CurrentShield, damage);
            CurrentShield -= shieldDamage;
            damage -= shieldDamage;

            if (previousShield > 0f && CurrentShield <= 0f)
            {
                PlaySFX(shieldBreakSound);
            }
            else
            {
                PlaySFX(shieldHitSound);
            }
        }

        if (damage > 0f)
        {
            tookHealthDamage = true;
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0f, CurrentHealth);
            PlaySFX(healthHitSound);
        }

        if (tookHealthDamage)
        {
            TriggerDamageFlash(healthHitColor);
        }
        else if (tookShieldDamage)
        {
            TriggerDamageFlash(shieldHitColor);
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

        PlaySFX(playerDeathSound);

        if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying)
        {
            heartbeatAudioSource.Stop();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
    }

    // --- HEARTBEAT SYSTEM ---

    private void HandleHeartbeatSound()
    {
        if (heartbeatSound == null || isDead) return;

        if (CurrentHealth < CurrentMaxHealth && CurrentHealth > 0f)
        {
            heartbeatTimer -= Time.deltaTime;

            if (heartbeatTimer <= 0f)
            {
                float healthRatio = Mathf.Clamp01(CurrentHealth / CurrentMaxHealth);
                float currentInterval = Mathf.Lerp(minHeartbeatInterval, maxHeartbeatInterval, healthRatio);

                heartbeatAudioSource.PlayOneShot(heartbeatSound, heartbeatVolume);
                heartbeatTimer = currentInterval;
            }
        }
        else
        {
            heartbeatTimer = 0f;
        }
    }

    // --- VISUAL & AUDIO FEEDBACK HELPERS ---

    private void TriggerDamageFlash(Color flashColor)
    {
        if (damageOverlayImage == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(DamageFlashRoutine(flashColor));
    }

    private IEnumerator DamageFlashRoutine(Color targetColor)
    {
        float elapsed = 0f;
        damageOverlayImage.color = targetColor;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            damageOverlayImage.color = Color.Lerp(targetColor, Color.clear, t);
            yield return null;
        }

        damageOverlayImage.color = Color.clear;
        flashCoroutine = null;
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, cameraTransform != null ? cameraTransform.position : transform.position, sfxVolume);
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
            extraJumpsRemaining = MaxExtraJumps;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    private Vector3 HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 rawInput = new Vector3(horizontal, 0f, vertical);

        Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * vertical + cameraRight * horizontal;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        characterController.Move(moveDirection * CurrentMoveSpeed * Time.deltaTime);

        return rawInput; // Returns local input vector for 8-way directional determination
    }

    private void HandleJump()
    {
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (coyoteTimer > 0f)
            {
                velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
                coyoteTimer = 0f;
            }
            else if (extraJumpsRemaining > 0)
            {
                velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
                extraJumpsRemaining--;
            }
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    // --- 8-WAY SPRITE ANIMATION CONTROLLER ---

    private void HandleSpriteAnimation(Vector3 inputVector)
    {
        if (spriteRenderer == null) return;

        bool isMoving = inputVector.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            // Calculate angle from input (0° = North/Up, 90° = East/Right, etc.)
            float angle = Mathf.Atan2(inputVector.x, inputVector.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // Divide 360° into 8 slices of 45° (offset by 22.5° for accurate compass snapping)
            lastDirectionIndex = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;
        }

        SpriteDirectionData dirData = GetDirectionData(lastDirectionIndex);
        if (dirData == null) return;

        if (isMoving && dirData.walkFrames != null && dirData.walkFrames.Count > 0)
        {
            // Cycle through walking frames
            animTimer += Time.deltaTime;
            float frameInterval = 1f / Mathf.Max(0.1f, animationFPS);

            if (animTimer >= frameInterval)
            {
                animTimer -= frameInterval;
                currentFrameIndex = (currentFrameIndex + 1) % dirData.walkFrames.Count;
            }

            SetSprite(dirData.walkFrames[currentFrameIndex]);
        }
        else
        {
            // Idle state
            animTimer = 0f;
            currentFrameIndex = 0;
            if (dirData.idleSprite != null)
            {
                SetSprite(dirData.idleSprite);
            }
        }
    }

    // Helper method to update both the visual sprite and the URP Lit shadow texture
    private void SetSprite(Sprite newSprite)
    {
        if (newSprite == null) return;

        spriteRenderer.sprite = newSprite;

        // Updates the URP Lit shader's _BaseMap so the ShadowCaster pass gets alpha clipping data
        if (spriteRenderer.material != null && newSprite.texture != null)
        {
            spriteRenderer.material.SetTexture("_BaseMap", newSprite.texture);
        }
    }

    private SpriteDirectionData GetDirectionData(int index)
    {
        switch (index)
        {
            case 0: return north;
            case 1: return northEast;
            case 2: return east;
            case 3: return southEast;
            case 4: return south;
            case 5: return southWest;
            case 6: return west;
            case 7: return northWest;
            default: return south;
        }
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

    public void Bounce(float force)
    {
        velocity.y = force;
        coyoteTimer = 0f;
    }
}