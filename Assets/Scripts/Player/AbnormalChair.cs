using UnityEngine;
using System.Collections;

public class AbnormalChair : EnemyBase
{
    [Header("Chair Smooth Bounce Settings")]
    [Tooltip("Force/Speed at which the player is bounced away.")]
    [SerializeField] private float pushForce = 8f;

    [Tooltip("Upward bounce force.")]
    [SerializeField] private float pushUpwardForce = 2f;

    [Tooltip("Duration of the push effect to make it look smooth and natural.")]
    [SerializeField] private float pushDuration = 0.3f;

    [Tooltip("Cooldown time between consecutive pushes.")]
    [SerializeField] private float pushCooldown = 0.5f;

    [Header("Auto Spawn Settings")]
    [Tooltip("Check this only on the main spawner object placed in the scene.")]
    [SerializeField] private bool isSpawner = false;

    [Tooltip("Number of chairs to spawn randomly across the map.")]
    [SerializeField] private int spawnCount = 10;

    [Tooltip("Radius around the player to scatter the chairs.")]
    [SerializeField] private float spawnRadius = 35f;

    [Tooltip("Layer mask for the ground so chairs only spawn on valid floors.")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Prefab of this chair to instantiate copies (leave empty to use this GameObject).")]
    [SerializeField] private GameObject chairPrefab;

    private float pushTimer;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        // If this object is set as the spawner, it will generate chairs upon starting (when player spawns)
        if (isSpawner)
        {
            StartCoroutine(SpawnChairsRoutine());
        }
    }

    protected override void Update()
    {
        base.Update();

        if (pushTimer > 0f)
        {
            pushTimer -= Time.deltaTime;
        }
    }

    // --- AUTO SPAWN LOGIC ---
    private IEnumerator SpawnChairsRoutine()
    {
        // Wait a brief moment to ensure the player is fully spawned and initialized in the scene
        yield return new WaitForSeconds(0.1f);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 centerPosition = playerObj != null ? playerObj.transform.position : Vector3.zero;

        int spawnedCount = 0;
        int maxAttempts = spawnCount * 3;
        int attempts = 0;

        while (spawnedCount < spawnCount && attempts < maxAttempts)
        {
            attempts++;

            // Generate a random position within the radius
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = centerPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Raycast from high above straight down to find the ground
            Vector3 rayOrigin = randomPos + Vector3.up * 50f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
            {
                // Check if it hits the specified ground layer (if groundLayer is set, otherwise accept any)
                if (groundLayer == 0 || ((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                {
                    // Avoid spawning too close to the player's initial position
                    if (playerObj == null || Vector3.Distance(hit.point, playerObj.transform.position) > 4f)
                    {
                        GameObject prefabToUse = chairPrefab != null ? chairPrefab : gameObject;

                        // Instantiate the chair at the ground hit point with a random Y rotation
                        GameObject newChair = Instantiate(prefabToUse, hit.point + Vector3.up * 0.1f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                        // Disable spawner flag on the spawned copy to prevent infinite loops
                        AbnormalChair chairComponent = newChair.GetComponent<AbnormalChair>();
                        if (chairComponent != null)
                        {
                            chairComponent.isSpawner = false;
                        }

                        spawnedCount++;
                    }
                }
            }
        }

        Debug.Log($"[AbnormalChair] Successfully spawned {spawnedCount} abnormal chairs on valid ground.");

        // Destroy the master spawner object itself if it was just acting as a controller
        if (gameObject != null && isSpawner)
        {
            Destroy(gameObject);
        }
    }

    // --- COLLISION & PUSH LOGIC ---
    private void OnCollisionEnter(Collision collision)
    {
        TryPushPlayer(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPushPlayer(other.gameObject);
    }

    private void TryPushPlayer(GameObject targetObj)
    {
        if (pushTimer > 0f || targetObj == null) return;

        if (targetObj.CompareTag("Player"))
        {
            ExecuteSmoothBounce(targetObj);
            pushTimer = pushCooldown;
        }
    }

    private void ExecuteSmoothBounce(GameObject player)
    {
        Vector3 pushDirection = player.transform.position - transform.position;
        pushDirection.y = 0f;

        if (pushDirection.sqrMagnitude < 0.01f)
        {
            pushDirection = transform.forward;
        }
        pushDirection.Normalize();

        Vector3 bounceVelocity = pushDirection * pushForce + Vector3.up * pushUpwardForce;

        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            StartCoroutine(SmoothPushRigidbodyRoutine(playerRb, bounceVelocity, pushDuration));
            Debug.Log($"[{gameObject.name}] Smoothly bounced player via Rigidbody.");
            return;
        }

        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null)
        {
            StartCoroutine(SmoothPushCharacterControllerRoutine(charController, bounceVelocity, pushDuration));
            Debug.Log($"[{gameObject.name}] Smoothly bounced player via CharacterController.");
            return;
        }

        Debug.LogWarning($"[{gameObject.name}] Player does not have a Rigidbody or CharacterController component.");
    }

    private IEnumerator SmoothPushRigidbodyRoutine(Rigidbody rb, Vector3 targetVelocity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentFactor = Mathf.Lerp(1f, 0f, t * t);

            rb.linearVelocity = new Vector3(
                targetVelocity.x * currentFactor,
                Mathf.Max(rb.linearVelocity.y, targetVelocity.y * currentFactor),
                targetVelocity.z * currentFactor
            );

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator SmoothPushCharacterControllerRoutine(CharacterController controller, Vector3 targetVelocity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (controller != null)
            {
                float t = elapsed / duration;
                float currentFactor = Mathf.Lerp(1f, 0f, t);
                Vector3 currentVelocity = targetVelocity * currentFactor;

                controller.Move(currentVelocity * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}