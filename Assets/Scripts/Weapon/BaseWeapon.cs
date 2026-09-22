using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Base Weapon Stats")]
    public float damage = 15f;
    public float attackRange = 12f;
    public float attackInterval = 1f;

    protected float attackTimer;

    protected virtual void Update()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            if (TryAttack())
            {
                attackTimer = attackInterval;
            }
        }
    }

    protected abstract bool TryAttack();

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
}