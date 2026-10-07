using UnityEngine;

public class LevelRuleManager : MonoBehaviour
{
    public static LevelRuleManager Instance { get; private set; }

    [Header("Default Physics")]
    public Vector3 baseGravity = new Vector3(0f, -9.81f, 0f);

    [Header("Spawn Safety Tuning")]
    [Tooltip("Radius matching the player's CharacterController radius to prevent edge clipping.")]
    public float playerSpawnRadius = 0.5f;

    [Tooltip("Height offset above ground surface when spawning.")]
    public float spawnHeightOffset = 1.8f;

    private bool round2TriggeredThisLevel = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ApplyActiveLevelRules()
    {
        if (GameManager.Instance == null) return;

        // Force Unity to update all newly spawned chunk/platform colliders before casting rays
        Physics.SyncTransforms();

        ApplyGravityRule();
        ApplyRandomSpawnRule();
    }

    // --- TARGET: LowerGravity ---
    private void ApplyGravityRule()
    {
        if (GameManager.Instance.HasLevelRule(LevelRuleType.LowerGravity))
        {
            float reducePercent = GameManager.Instance.GetTotalLevelRuleValue(LevelRuleType.LowerGravity);
            float multiplier = Mathf.Clamp01(1f - (reducePercent / 100f));
            Physics.gravity = baseGravity * multiplier;
            Debug.Log($"[LevelRuleManager] Applied Lower Gravity: {reducePercent}% reduction.");
        }
        else
        {
            Physics.gravity = baseGravity;
        }
    }

    // --- TARGET: RandomSpawn & Safe Default Spawn ---
    private void ApplyRandomSpawnRule()
    {
        Transform playerTransform = (MapGenerator.Instance != null && MapGenerator.Instance.player != null)
            ? MapGenerator.Instance.player
            : GameObject.FindWithTag("Player")?.transform;

        if (playerTransform == null) return;

        bool hasRandomSpawnRule = GameManager.Instance != null && GameManager.Instance.HasLevelRule(LevelRuleType.RandomSpawn);

        if (TryGetValidSpawnPosition(hasRandomSpawnRule, out Vector3 safeSpawnPos))
        {
            // 1. Reset Rigidbody velocity if present
            if (playerTransform.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // 2. Safely disable CharacterController during teleportation
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 3. Set position cleanly above verified solid ground
            playerTransform.position = safeSpawnPos + Vector3.up * spawnHeightOffset;

            // 4. Force physics engine sync before re-enabling collider
            Physics.SyncTransforms();

            if (cc != null) cc.enabled = true;

            Debug.Log($"[LevelRuleManager] Safe Spawn Applied at {playerTransform.position} (RandomSpawn Rule: {hasRandomSpawnRule}).");
        }
        else if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.TeleportPlayerToSpawn();
        }
    }

    // --- TARGET: Round2 ---
    public bool CheckShouldRepeatLevelForRound2()
    {
        if (GameManager.Instance != null && GameManager.Instance.HasLevelRule(LevelRuleType.Round2) && !round2TriggeredThisLevel)
        {
            float chancePercent = GameManager.Instance.GetTotalLevelRuleValue(LevelRuleType.Round2);
            float chance = chancePercent > 0 ? chancePercent / 100f : 1f;

            if (Random.value <= chance)
            {
                round2TriggeredThisLevel = true;
                Debug.Log("[LevelRuleManager] Round 2 Triggered! Replaying level.");
                return true;
            }
        }

        round2TriggeredThisLevel = false;
        return false;
    }

    // --- SPAWN VALIDATION & GROUND RAYCASTING ---

    private bool TryGetValidSpawnPosition(bool forceRandom, out Vector3 validSpawnPos)
    {
        validSpawnPos = Vector3.zero;

        // 1. Random Spawn Rule: sample platform positions (skipping blacklisted chunks)
        if (forceRandom)
        {
            for (int i = 0; i < 25; i++)
            {
                if (MapGenerator.Instance != null && MapGenerator.Instance.TryGetRandomPlatformPosition(out Vector3 randomMapPos, excludeBlacklisted: true))
                {
                    if (IsSolidGroundBelow(randomMapPos, out Vector3 hitPoint))
                    {
                        validSpawnPos = hitPoint;
                        return true;
                    }
                }
            }
        }

        // 2. Default Start Position
        if (!forceRandom && MapGenerator.Instance != null)
        {
            Vector3 defaultStartPos = MapGenerator.Instance.StartChunkWorldPosition;
            if (IsSolidGroundBelow(defaultStartPos, out Vector3 hitPoint))
            {
                validSpawnPos = hitPoint;
                return true;
            }
            Debug.LogWarning("[LevelRuleManager] Start chunk center empty! Finding valid platform...");
        }

        // 3. Fallback: Search non-blacklisted chunks
        if (MapGenerator.Instance != null)
        {
            for (int i = 0; i < 25; i++)
            {
                if (MapGenerator.Instance.TryGetRandomPlatformPosition(out Vector3 platformPos, excludeBlacklisted: true))
                {
                    if (IsSolidGroundBelow(platformPos, out Vector3 hitPoint))
                    {
                        validSpawnPos = hitPoint;
                        return true;
                    }
                }
            }
        }

        // 4. Last Resort: SphereCast down from height
        int layerMask = (GameManager.Instance != null && GameManager.Instance.groundLayer.value != 0)
            ? GameManager.Instance.groundLayer.value
            : ~0;

        for (int i = 0; i < 30; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * 20f;
            Vector3 rayStart = new Vector3(randomCircle.x, 35f, randomCircle.y);

            if (Physics.SphereCast(rayStart, playerSpawnRadius, Vector3.down, out RaycastHit hit, 60f, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger)
                {
                    validSpawnPos = hit.point;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsSolidGroundBelow(Vector3 pos, out Vector3 hitPoint)
    {
        hitPoint = pos;

        int layerMask = (GameManager.Instance != null && GameManager.Instance.groundLayer.value != 0)
            ? GameManager.Instance.groundLayer.value
            : ~0;

        Vector3 rayStart = new Vector3(pos.x, pos.y + 20f, pos.z);

        // SphereCast ensures floor solidness across the player's full collider width rather than a single point
        if (Physics.SphereCast(rayStart, playerSpawnRadius, Vector3.down, out RaycastHit hit, 45f, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.isTrigger)
            {
                hitPoint = hit.point;
                return true;
            }
        }

        return false;
    }
}