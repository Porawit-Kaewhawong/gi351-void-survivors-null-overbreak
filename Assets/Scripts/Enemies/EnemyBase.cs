using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 50f;
    public float CurrentHealth { get; protected set; }

    [Header("Movement Settings")]
    public float moveSpeed = 3.5f;

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

    protected float attackCooldownTimer;
    protected Vector3 spawnPosition;
    protected Quaternion spawnRotation;

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
        // Re-evaluate stats, reset HP, find player, and reset attack cooldown every time object is retrieved from pool
        ApplyEnemyBuffs();
        FindPlayer();
        attackCooldownTimer = 0f;
    }

    protected virtual void Update()
    {
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }
    }

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