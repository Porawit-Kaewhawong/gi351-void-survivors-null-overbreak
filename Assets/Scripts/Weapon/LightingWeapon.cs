using UnityEngine;

public class LightningWeapon : BaseWeapon
{
    [Header("Lightning Settings")]
    public float splashRadius = 4f;

    protected override bool TryAttack()
    {
        EnemyBase primaryTarget = FindNearestEnemy();
        if (primaryTarget == null) return false;

        // Damage main target
        primaryTarget.TakeDamage(damage);

        // AOE Splash around primary target
        Collider[] splashedColliders = Physics.OverlapSphere(primaryTarget.transform.position, splashRadius);
        foreach (var col in splashedColliders)
        {
            EnemyBase splashEnemy = col.GetComponent<EnemyBase>();
            if (splashEnemy != null && splashEnemy != primaryTarget && splashEnemy.CurrentHealth > 0f)
            {
                splashEnemy.TakeDamage(damage * 0.5f);
            }
        }

        return true;
    }
}