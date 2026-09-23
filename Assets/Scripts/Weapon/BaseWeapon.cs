using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Base Weapon Stats")]
    public float damage = 15f;
    public float attackRange = 12f;
    public float attackInterval = 1f;

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when the weapon attacks.")]
    [SerializeField] protected AudioClip attackSound;

    [Range(0f, 1f)]
    [SerializeField] protected float soundVolume = 1f;

    [Tooltip("Adds slight pitch variation (+/- 10%) so continuous attacks sound less repetitive.")]
    [SerializeField] protected bool randomizePitch = true;

    protected float attackTimer;
    protected AudioSource audioSource;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D sound attached to weapon
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
            if (enemy == null || enemy.CurrentHealth <= 0f) continue;

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