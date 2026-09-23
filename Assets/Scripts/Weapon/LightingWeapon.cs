using UnityEngine;

public class LightningWeapon : BaseWeapon
{
    [Header("Lightning Settings")]
    public float splashRadius = 4f;

    [Header("Lightning Visual Settings")]
    public Color lightningColor = new Color(0.2f, 0.6f, 1f, 1f);
    public int lightningSegments = 6;
    public float jitterAmount = 0.35f;

    protected override bool TryAttack()
    {
        EnemyBase primaryTarget = FindNearestEnemy();
        if (primaryTarget == null) return false;

        // Damage main target
        primaryTarget.TakeDamage(damage);

        Vector3 startPos = transform.position;
        Vector3 mainTargetPos = primaryTarget.transform.position + Vector3.up * 0.5f;

        // Main primary lightning bolt
        CreateJaggedLightningBeam(startPos, mainTargetPos, 0.12f);

        // AOE Splash around primary target
        Collider[] splashedColliders = Physics.OverlapSphere(primaryTarget.transform.position, splashRadius);
        foreach (var col in splashedColliders)
        {
            EnemyBase splashEnemy = col.GetComponent<EnemyBase>();
            if (splashEnemy != null && splashEnemy != primaryTarget && splashEnemy.CurrentHealth > 0f)
            {
                splashEnemy.TakeDamage(damage * 0.5f);

                Vector3 splashTargetPos = splashEnemy.transform.position + Vector3.up * 0.5f;
                CreateJaggedLightningBeam(mainTargetPos, splashTargetPos, 0.06f);
            }
        }

        return true;
    }

    private void CreateJaggedLightningBeam(Vector3 start, Vector3 end, float thickness)
    {
        GameObject boltObj = new GameObject("LightningBolt");
        LineRenderer lr = boltObj.AddComponent<LineRenderer>();

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lightningColor;
        lr.endColor = Color.white;
        lr.startWidth = thickness;
        lr.endWidth = thickness * 0.5f;
        lr.positionCount = lightningSegments + 1;

        lr.SetPosition(0, start);

        for (int i = 1; i < lightningSegments; i++)
        {
            float progress = (float)i / lightningSegments;
            Vector3 midPoint = Vector3.Lerp(start, end, progress);

            Vector3 randomOffset = Random.insideUnitSphere * jitterAmount;
            lr.SetPosition(i, midPoint + randomOffset);
        }

        lr.SetPosition(lightningSegments, end);

        Destroy(boltObj, 0.1f);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}