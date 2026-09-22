using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 50f;
    public float CurrentHealth { get; protected set; }

    [Header("Combat Settings")]
    public float attackDamage = 10f;
    public float attackRate = 1f; // Attacks per second
    public float attackDistance = 1.2f;

    [Header("Targeting")]
    protected Transform playerTransform;
    protected PlayerController playerController;

    [Header("Fall & Death")]
    public bool destroyOnFall = true;
    public float fallDistance = 20f;

    protected float attackCooldownTimer;
    protected Vector3 spawnPosition;
    protected Quaternion spawnRotation;

    protected virtual void Awake()
    {
        CurrentHealth = maxHealth;
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

    protected void FindPlayer()
    {
        if (playerTransform != null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
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
        Destroy(gameObject);
    }

    protected virtual bool TryAttackPlayer()
    {
        if (playerController == null || attackCooldownTimer > 0f) return false;

        float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;
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