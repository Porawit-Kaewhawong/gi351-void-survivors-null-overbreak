using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Base Weapon Stats")]
    public float damage = 15f;
    public float attackRange = 12f;
    public float attackInterval = 1f;

    [Header("Weapon Stacking / Level Scaling")]
    public int Level { get; protected set; } = 1;

    [Tooltip("Percentage bonus damage added per extra weapon level (e.g., 0.4 = +40% damage per level).")]
    public float damageScalePerLevel = 0.05f;

    [Tooltip("Percentage attack interval reduction per extra weapon level (e.g., 0.15 = 15% faster attacks per level).")]
    public float cooldownReductionPerLevel = 0.02f;

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when the weapon attacks.")]
    [SerializeField] protected AudioClip attackSound;

    [Range(0f, 1f)]
    [SerializeField] protected float soundVolume = 1f;

    [Tooltip("Adds slight pitch variation (+/- 10%) so continuous attacks sound less repetitive.")]
    [SerializeField] protected bool randomizePitch = true;

    protected float attackTimer;
    protected AudioSource audioSource;

    // Cached initial stats for clean level math
    protected float baseDamage;
    protected float baseAttackInterval;
    protected float baseAttackRange;

    protected virtual void Awake()
    {
        baseDamage = damage;
        baseAttackInterval = attackInterval;
        baseAttackRange = attackRange;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D sound attached to weapon
    }

    /// <summary>
    /// Called when the weapon is spawned to apply stat upgrades for duplicate picks.
    /// </summary>
    public virtual void SetWeaponLevel(int level)
    {
        Level = Mathf.Max(1, level);

        // Level 1 = 100% stats. Level 2 = +40% damage, 15% faster attack, etc.
        float damageMultiplier = 1f + ((Level - 1) * damageScalePerLevel);
        damage = baseDamage * damageMultiplier;

        // Decrease interval (faster attack speed) with diminishing returns (min 0.05s)
        float cooldownMultiplier = Mathf.Pow(1f - cooldownReductionPerLevel, Level - 1);
        attackInterval = Mathf.Max(0.05f, baseAttackInterval * cooldownMultiplier);
    }

    protected virtual void Update()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            if (TryAttack())
            {
                PlayAttackSound();
                attackTimer = attackInterval;
            }
        }
    }

    protected abstract bool TryAttack();

    protected virtual void PlayAttackSound()
    {
        if (attackSound != null && audioSource != null)
        {
            audioSource.pitch = randomizePitch ? Random.Range(0.9f, 1.1f) : 1f;
            audioSource.PlayOneShot(attackSound, soundVolume);
        }
    }

    protected EnemyBase FindNearestEnemy()
    {
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        EnemyBase nearest = null;
        float minDistanceSqr = attackRange * attackRange;

        foreach (var enemy in enemies)
        {
            // Ignore null, dead, or non-targetable hazards
            if (enemy == null || enemy.CurrentHealth <= 0f || !enemy.IsTargetable) continue;

            float distSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distSqr <= minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearest = enemy;
            }
        }

        return nearest;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}