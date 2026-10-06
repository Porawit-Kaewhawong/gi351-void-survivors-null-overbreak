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

    [Tooltip("Base number of chairs to spawn at the reference map radius.")]
    [SerializeField] private int baseSpawnCount = 10;

    [Tooltip("Base reference map radius (in grid chunks) used for 1x scaling ratio.")]
    [SerializeField] private float referenceMapRadius = 10f;

    [Tooltip("If checked, scales count by total map area (Radius^2) to maintain constant density. If false, scales linearly with radius.")]
    [SerializeField] private bool scaleByMapArea = false;

    [Tooltip("Percentage of the total world map radius (0.1 to 1.0) across which chairs will scatter.")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float mapRadiusCoverage = 0.75f;

    [Tooltip("Fallback scatter radius (in world units) if MapGenerator is not present.")]
    [SerializeField] private float fallbackSpawnRadius = 35f;

    [Tooltip("Layer mask for the ground so chairs only spawn on valid floors.")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Prefab of this chair to instantiate copies (leave empty to use this GameObject).")]
    [SerializeField] private GameObject chairPrefab;

    [Header("Proximity & Auto Despawn Settings")]
    [Tooltip("Radius around the player where the chair remains active.")]
    [SerializeField] private float playerDetectionRange = 25f;

    [Tooltip("Time in seconds out of player range before the chair automatically despawns.")]
    [SerializeField] private float outOfRangeDespawnTime = 5f;

    private float pushTimer;
    private float outOfRangeTimer;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (isSpawner)
        {
            StartCoroutine(SpawnChairsRoutine());
        }
        else
        {
            // Auto-register non-spawner chairs with GameManager for cleanup on level transitions
            RegisterWithGameManager();
        }
    }

    private void RegisterWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterEnemy(gameObject);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (pushTimer > 0f)
        {
            pushTimer -= Time.deltaTime;
        }

        // Only manage despawning on instantiated chairs, not the spawner template
        if (!isSpawner)
        {
            HandleOutOfRangeDespawn();
        }
    }

    // --- RANGE & AUTO-DESPAWN LOGIC ---
    private void HandleOutOfRangeDespawn()
    {
        // Use inherited playerTransform from EnemyBase
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                return; // Pause timer if no player exists in scene yet
            }
        }

        // Fast distance calculation using sqrMagnitude (saves CPU performance)
        float sqrDistance = (transform.position - playerTransform.position).sqrMagnitude;
        float sqrRange = playerDetectionRange * playerDetectionRange;

        if (sqrDistance > sqrRange)
        {
            // Out of player range -> count down towards despawn
            outOfRangeTimer += Time.deltaTime;

            if (outOfRangeTimer >= outOfRangeDespawnTime)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            // Player is in range -> reset timer to keep chair alive
            outOfRangeTimer = 0f;
        }
    }

    // --- DYNAMIC SCALED AUTO-SPAWN LOGIC ---
    private IEnumerator SpawnChairsRoutine()
    {
        // Wait briefly for player and MapGenerator to finish initial placement
        yield return new WaitForSeconds(0.1f);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 centerPosition = playerObj != null ? playerObj.transform.position : Vector3.zero;

        // 1. Calculate dynamic radius and count based on MapGenerator
        float effectiveRadius = fallbackSpawnRadius;
        int targetSpawnCount = baseSpawnCount;

        if (MapGenerator.Instance != null)
        {
            float currentMapRadius = MapGenerator.Instance.mapRadius;
            float chunkSize = MapGenerator.Instance.chunkSize;

            float totalWorldRadius = currentMapRadius * chunkSize;
            effectiveRadius = totalWorldRadius * mapRadiusCoverage;

            float radiusRatio = currentMapRadius / Mathf.Max(1f, referenceMapRadius);

            if (scaleByMapArea)
            {
                targetSpawnCount = Mathf.RoundToInt(baseSpawnCount * (radiusRatio * radiusRatio));
            }
            else
            {
                targetSpawnCount = Mathf.RoundToInt(baseSpawnCount * radiusRatio);
            }

            targetSpawnCount = Mathf.Max(1, targetSpawnCount);
        }

        // 2. Perform ground raycasting and spawning
        int spawnedCount = 0;
        int maxAttempts = targetSpawnCount * 4;
        int attempts = 0;

        while (spawnedCount < targetSpawnCount && attempts < maxAttempts)
        {
            attempts++;

            Vector2 randomCircle = Random.insideUnitCircle * effectiveRadius;
            Vector3 randomPos = centerPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            Vector3 rayOrigin = randomPos + Vector3.up * 50f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
            {
                if (groundLayer == 0 || ((1 << hit.collider.gameObject.layer) & groundLayer) != 0)
                {
                    if (playerObj == null || Vector3.Distance(hit.point, playerObj.transform.position) > 4f)
                    {
                        GameObject prefabToUse = chairPrefab != null ? chairPrefab : gameObject;

                        GameObject newChair = Instantiate(prefabToUse, hit.point + Vector3.up * 0.75f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                        AbnormalChair chairComponent = newChair.GetComponent<AbnormalChair>();
                        if (chairComponent != null)
                        {
                            chairComponent.isSpawner = false;
                        }

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.RegisterEnemy(newChair);
                        }

                        spawnedCount++;
                    }
                }
            }
        }

        Debug.Log($"[AbnormalChair] Map Radius: {(MapGenerator.Instance != null ? MapGenerator.Instance.mapRadius : 0)} | Spawned {spawnedCount}/{targetSpawnCount} chairs across {effectiveRadius:F1}m radius.");

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
            return;
        }

        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null)
        {
            StartCoroutine(SmoothPushCharacterControllerRoutine(charController, bounceVelocity, pushDuration));
            return;
        }
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