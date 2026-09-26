using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private float damage = 10f;
    private bool hasHit = false;

    public void Initialize(float dmg)
    {
        damage = dmg;
        Destroy(gameObject, 4f);
    }

    // Handles physical solid collisions
    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.gameObject);
    }

    // Handles trigger zone collisions (if Is Trigger is enabled on either object)
    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other.gameObject);
    }

    private void HandleImpact(GameObject hitObject)
    {
        if (hasHit) return;

        // Ignore hitting the enemy launcher itself
        if (hitObject.GetComponent<EnemyBase>() != null) return;

        hasHit = true;

        // Check if the object or any of its parents are tagged "Player" or have PlayerController
        if (hitObject.CompareTag("Player") || hitObject.GetComponentInParent<PlayerController>() != null)
        {
            PlayerController player = hitObject.GetComponentInParent<PlayerController>();

            if (player != null)
            {
                player.TakeDamage(damage);
                Debug.Log($"[EnemyProjectile] Successfully dealt {damage} damage to Player!");
            }
            else
            {
                Debug.LogWarning("[EnemyProjectile] Hit object tagged 'Player', but no PlayerController script was found on it or its parents!");
            }
        }
        else
        {
            Debug.Log($"[EnemyProjectile] Hit non-player object: {hitObject.name}");
        }

        Destroy(gameObject);
    }
}