using UnityEngine;

public class FacePlayer : MonoBehaviour
{
    [Header("Targeting Settings")]
    public Transform player;
    public float detectionRange = 10f;
    public float rotationSpeed = 5f;
    public bool lockYAxis = true;

    void Start()
    {
        // Auto-assign player by tag if not assigned in Inspector
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionRange)
        {
            Vector3 direction = player.position - transform.position;

            if (lockYAxis)
            {
                direction.y = 0; // Prevents object from tilting up/down
            }

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    // Visualize the range in Scene View
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}