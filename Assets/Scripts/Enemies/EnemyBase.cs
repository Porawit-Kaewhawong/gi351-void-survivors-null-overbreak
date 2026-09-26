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

    protected float attackCooldownTimer;
    protected Vector3 spawnPosition;
    protected Quaternion spawnRotation;

    // Cached base stats for scaling calculations
    protected float baseMaxHealth;
    protected float baseMoveSpeed;

    protected virtual void Awake()
    {
        baseMaxHealth = maxHealth;
        baseMoveSpeed = moveSpeed;

        ApplyEnemyBuffs();

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        FindPlayer();
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

        CurrentHealth -= damage;
        Debug.Log($"[{gameObject.name}] Took {damage} DMG. Remaining HP: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Debug.Log($"[{gameObject.name}] Died.");

        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathSoundVolume);
        }

        Destroy(gameObject);
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