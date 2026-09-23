using System.Collections;
using UnityEngine;

public class AuraWeapon : BaseWeapon
{
    [Header("Aura Visual Settings")]
    public Color auraColor = new Color(0f, 0.8f, 1f, 0.4f);

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

        if (hitAny)
        {
            StartCoroutine(SpawnAuraPulseRoutine());
        }

        return hitAny;
    }

    private IEnumerator SpawnAuraPulseRoutine()
    {
        GameObject pulseObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(pulseObj.GetComponent<Collider>());

        pulseObj.transform.position = transform.position;
        pulseObj.transform.localScale = Vector3.zero;

        Material pulseMat = new Material(Shader.Find("Sprites/Default"));
        pulseMat.color = auraColor;
        pulseObj.GetComponent<Renderer>().material = pulseMat;

        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 targetScale = Vector3.one * (attackRange * 2f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            if (pulseObj != null)
            {
                pulseObj.transform.position = transform.position;
                pulseObj.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, progress);

                Color current = auraColor;
                current.a = Mathf.Lerp(auraColor.a, 0f, progress);
                pulseMat.color = current;
            }

            yield return null;
        }

        if (pulseObj != null) Destroy(pulseObj);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}