using UnityEngine;

public class GunWeapon : BaseWeapon
{
    [Header("Gun Visuals")]
    [Tooltip("Optional custom tracer prefab. If left unassigned, a dynamic tracer beam will be generated.")]
    public LineRenderer tracerPrefab;

    [Tooltip("Color of the bullet tracer beam.")]
    public Color tracerColor = Color.yellow;

    protected override bool TryAttack()
    {
        EnemyBase target = FindNearestEnemy();
        if (target == null) return false;

        target.TakeDamage(damage);

        Vector3 startPos = transform.position;
        Vector3 endPos = target.transform.position + Vector3.up * 0.5f;

        SpawnTracerVisual(startPos, endPos);

        return true;
    }

    private void SpawnTracerVisual(Vector3 start, Vector3 end)
    {
        if (tracerPrefab != null)
        {
            LineRenderer tracer = Instantiate(tracerPrefab);
            tracer.SetPosition(0, start);
            tracer.SetPosition(1, end);
            Destroy(tracer.gameObject, 0.08f);
        }
        else
        {
            GameObject tracerObj = new GameObject("GunTracer");
            LineRenderer lr = tracerObj.AddComponent<LineRenderer>();

            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = tracerColor;
            lr.endColor = Color.white;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.02f;
            lr.positionCount = 2;

            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            Destroy(tracerObj, 0.08f);
        }
    }
}