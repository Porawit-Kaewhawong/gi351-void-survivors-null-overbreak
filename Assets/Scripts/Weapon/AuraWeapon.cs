using UnityEngine;

public class AuraWeapon : BaseWeapon
{
    protected override bool TryAttack()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attackRange);
        bool hitAny = false;

        foreach (var hit in hitColliders)
        {
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null && enemy.CurrentHealth > 0f)
            {
                enemy.TakeDamage(damage);
                hitAny = true;
            }
        }

        return hitAny;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}