using UnityEngine;

public class GunWeapon : BaseWeapon
{
    [Header("Gun Visuals")]
    public LineRenderer tracerPrefab;

    protected override bool TryAttack()
    {
        EnemyBase target = FindNearestEnemy();
        if (target == null) return false;

        target.TakeDamage(damage);

        if (tracerPrefab != null)
        {
            LineRenderer tracer = Instantiate(tracerPrefab);
            tracer.SetPosition(0, transform.position);
            tracer.SetPosition(1, target.transform.position);
            Destroy(tracer.gameObject, 0.1f);
        }

        return true;
    }
}