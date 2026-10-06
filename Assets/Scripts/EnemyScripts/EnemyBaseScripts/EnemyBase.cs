using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    public int StackCount { get; protected set; } = 1;

    public virtual void SetStackCount(int stackCount)
    {
        StackCount = Mathf.Max(1, stackCount);
    }

    [Header("Health Settings")]
    public float maxHealth = 50f;
    public float CurrentHealth { get; protected set; }

    [Header("Movement Settings")]
    public float moveSpeed = 3.5f;

    [Header("Catch-Up & Rubberband Settings")]
    [Tooltip("Enable dynamic speed scaling when the player gets too far away.")]
    public bool enableCatchUpSpeed = true;

    [Tooltip("Distance at which speed scaling begins to ramp up.")]
    public float catchUpStartDistance = 8f;

    [Tooltip("Distance at which maximum speed scaling multiplier is reached.")]
    public float catchUpMaxDistance = 22f;

    [Tooltip("Maximum speed multiplier applied when player is at or beyond max distance (e.g. 1.8 = +80% speed).")]
    public float maxCatchUpMultiplier = 1.8f;

    [Header("Dash / Lunge Mechanics")]
    [Tooltip("Allows the enemy to perform a sudden forward burst speed lunge when at mid-range.")]
    public bool enableDash = false;

    [Tooltip("Distance threshold from player required to initiate a lunge.")]
    public float dashTriggerDistance = 6f;

    [Tooltip("Speed multiplier applied while dashing.")]
    public float dashSpeedMultiplier = 2.5f;

    [Tooltip("Duration of the dash burst in seconds.")]
    public float dashDuration = 0.35f;

    [Tooltip("Cooldown in seconds between dashes.")]
    public float dashCooldown = 4f;

    [Header("Combat Settings")]
    public float attackDamage = 10f;
    public float attackRate = 1f; // Attacks per second
    public float attackDistance = 1.2f;

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when this enemy dies.")]
    public AudioClip deathSound;

    [Range(0f, 1f)]
    public float deathSoundVolume = 1f;

    [Header("Targeting")]
    protected Transform playerTransform;
    protected PlayerController playerController;

    [Header("Fall & Death")]
    public bool destroyOnFall = true;
    public float fallDistance = 20f;

    [Header("Damage Pop-up Settings")]
    [Tooltip("Prefab containing FloatingDamageText component.")]
    public GameObject damageTextPrefab;

    public virtual bool IsTargetable => true;

    protected float attackCooldownTimer;
    protected Vector3 spawnPosition;
    protected Quaternion spawnRotation;

    // Dash State Variables
    protected bool isDashing;
    protected float dashTimer;
    protected float dashCooldownTimer;

    // Cached base stats for scaling calculations
    protected float baseMaxHealth;
    protected float baseMoveSpeed;
    protected SpriteFlash spriteFlasher;

    protected virtual void Awake()
    {
        baseMaxHealth = maxHealth;
        baseMoveSpeed = moveSpeed;

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // Find SpriteFlash anywhere on self or child objects
        spriteFlasher = GetComponentInChildren<SpriteFlash>();
    }

    protected virtual void OnEnable()
    {
        // Re-evaluate stats, reset HP, find player, and reset attack/dash timers every time object is retrieved from pool
        ApplyEnemyBuffs();
        FindPlayer();
        attackCooldownTimer = 0f;

        // Reset dash states
        isDashing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
    }

    protected virtual void Update()
    {
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        UpdateDashLogic();
    }

    // --- GAP-CLOSING & SPEED CALCULATIONS ---

    /// <summary>
    /// Calculates current actual move speed accounting for base stats, global enemy buff modifiers, rubberband catch-up scaling, and dash state.
    /// Derived enemy scripts should call this function for movement calculations instead of raw moveSpeed.
    /// </summary>
    public virtual float GetEffectiveMoveSpeed()
    {
        float currentSpeed = moveSpeed;

        // 1. Dash speed overrides standard movement
        if (isDashing)
        {
            return currentSpeed * dashSpeedMultiplier;
        }

        // 2. Rubberbanding: Scale speed up smooth linearly as player distance increases beyond threshold
        if (enableCatchUpSpeed && playerTransform != null)
        {
            float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;
            float startSqr = catchUpStartDistance * catchUpStartDistance;

            if (sqrDistance > startSqr)
            {
                float distance = Mathf.Sqrt(sqrDistance);
                float t = Mathf.Clamp01((distance - catchUpStartDistance) / (catchUpMaxDistance - catchUpStartDistance));
                float catchUpMult = Mathf.Lerp(1f, maxCatchUpMultiplier, t);
                currentSpeed *= catchUpMult;
            }
        }

        return currentSpeed;
    }

    protected virtual void UpdateDashLogic()
    {
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }
        else if (enableDash && dashCooldownTimer <= 0f && playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Trigger dash if player is within engagement zone but out of immediate attack range
            if (distanceToPlayer <= dashTriggerDistance && distanceToPlayer > attackDistance)
            {
                TriggerDash();
            }
        }
    }

    public virtual void TriggerDash()
    {
        if (dashCooldownTimer > 0f || isDashing) return;

        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
    }

    // --- STAT BUFFS ---

    public virtual void ApplyEnemyBuffs()
    {
        if (GameManager.Instance != null)
        {
            float healthPercent = GameManager.Instance.GetTotalEnemyBuffValue(EnemyStatType.Health);
            maxHealth = baseMaxHealth * (1f + (healthPercent / 100f));

            float speedPercent = GameManager.Instance.GetTotalEnemyBuffValue(EnemyStatType.MoveSpeed);
            moveSpeed = baseMoveSpeed * (1f + (speedPercent / 100f));
        }

        CurrentHealth = maxHealth;
    }

    protected void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        if (playerTransform != null && playerController == null)
        {
            playerController = playerTransform.GetComponent<PlayerController>()
                ?? playerTransform.GetComponentInParent<PlayerController>()
                ?? playerTransform.GetComponentInChildren<PlayerController>();
        }
    }

    // --- HEALTH & COMBAT ---

    public virtual void TakeDamage(float damage)
    {
        if (damage <= 0f || CurrentHealth <= 0f) return;

        if (HitStop.Instance != null) HitStop.Instance.Trigger(0.03f);

        if (spriteFlasher != null)
        {
            spriteFlasher.Flash();
        }

        // --- SPAWN FLOATING DAMAGE TEXT ---
        if (damageTextPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.2f + (Random.insideUnitSphere * 0.3f);
            spawnPos.z = transform.position.z; // Keep Z plane aligned

            GameObject textObj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
            if (textObj.TryGetComponent<FloatingDamageText>(out var popup))
            {
                popup.Setup(damage);
            }
        }

        CurrentHealth -= damage;
        if (CurrentHealth <= 0f) Die();
    }

    protected virtual void Die()
    {
        Debug.Log($"[{gameObject.name}] Died.");

        if (deathSound != null)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXAtPosition(deathSound, transform.position, deathSoundVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(deathSound, transform.position, deathSoundVolume);
            }
        }

        if (SimpleEnemyPool.Instance != null)
        {
            SimpleEnemyPool.Instance.ReturnEnemy(gameObject);
        }
        else
        {
            if (GameManager.Instance != null) GameManager.Instance.UnregisterEnemy(gameObject);
            Destroy(gameObject);
        }
    }

    protected virtual bool TryAttackPlayer()
    {
        if (playerController == null || attackCooldownTimer > 0f) return false;

        // Use horizontal distance so height difference doesn't block attacks
        Vector3 direction = playerTransform.position - transform.position;
        direction.y = 0f;

        float sqrDistance = direction.sqrMagnitude;
        float sqrAttackDist = attackDistance * attackDistance;

        if (sqrDistance <= sqrAttackDist)
        {
            playerController.TakeDamage(attackDamage);
            attackCooldownTimer = 1f / attackRate;
            return true;
        }

        return false;
    }
}