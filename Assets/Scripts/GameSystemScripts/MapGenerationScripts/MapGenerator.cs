using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance { get; private set; }

    [Header("3D World & Map Size Settings")]
    public Transform player;
    public GameObject spawnPrefab;
    public float chunkSize = 50f;
    public float chunkYPosition = 0f;
    [Tooltip("Radial map radius (in chunks) from spawn.")]
    public int mapRadius = 15;

    [Header("Async Performance & Smooth Loading")]
    [Tooltip("Maximum allowed time per frame (in milliseconds) spent instantiating chunks to prevent lag spikes.")]
    public float frameTimeBudgetMs = 12f;

    [Header("Dynamic Chunk Pop-In Animation")]
    [Tooltip("Duration of chunk spawn pop animation in seconds.")]
    public float chunkAnimDuration = 0.4f;

    [Tooltip("Vertical drop distance from which new chunks rise up during spawn animation.")]
    public float spawnYOffset = -4f;

    [Tooltip("Scale and positioning curve for pop-in effect.")]
    public AnimationCurve spawnPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Nullscape Verticality (Archipelago Elevation)")]
    public bool enableVerticality = true;
    [Tooltip("Dynamically pitch and roll chunks into climbable ramps when connecting different height tiers.")]
    public bool enableSlopeRamps = true;
    [Tooltip("Vertical height difference per tier (e.g., 6 units on Y).")]
    public float heightStep = 6f;
    [Tooltip("Chance (0 to 1) for a path tile to step up or down in height.")]
    [Range(0f, 0.5f)] public float heightStepChance = 0.20f;
    [Tooltip("Maximum allowed height tier offset above or below spawn (+/- tiers).")]
    public int maxElevationTier = 3;

    [Header("Megabonk Landmark / Void Shrine Settings")]
    [Tooltip("Special Shrine/Landmark prefabs spawned at the tips of main trunks.")]
    public GameObject[] shrinePrefabs;

    [Header("Balanced Organic Trunks (Directional Winding)")]
    [Range(0.4f, 0.95f)] public float maxTrunkLengthRatio = 0.75f;
    [Range(0.5f, 3.0f)] public float trunkForwardBias = 1.0f;

    [Header("Trunk Mid-Branch Seeding")]
    [Range(0.1f, 0.6f)] public float trunkBranchSeedChance = 0.35f;

    [Header("Dynamic Void Budgeting (Target ~50%)")]
    [Range(0.2f, 0.7f)] public float targetVoidRatio = 0.50f;
    [Range(0.4f, 0.85f)] public float bigVoidShare = 0.70f;

    [Header("Organic Path Crawler Settings")]
    [Range(0.25f, 0.8f)] public float targetPathDensity = 0.45f;
    [Range(0.05f, 0.4f)] public float crawlerBranchChance = 0.25f;

    [Header("Generator Reliability & Fail-Safe")]
    public int maxGenerationRetries = 10;

    [Header("Socket Prefab Library")]
    public GameObject[] allChunkPrefabs;

    [Header("Item / Collectible Spawner Settings")]
    public GameObject[] itemPrefabs;
    public string itemSpawnPointName = "ItemSpawn";

    [Header("Level Rule Objects & Scaling")]
    public GameObject jumpPadPrefab;
    [Tooltip("Base reference map radius used for 1x multiplier scaling.")]
    public float referenceMapRadius = 10f;
    [Tooltip("Offset multiplier from platform center toward void (0.5 = exact platform edge).")]
    [Range(0.1f, 1.0f)] public float jumpPadEdgeOffset = 0.58f;

    [Header("Generator Seed Settings")]
    public bool useRandomSeed = true;
    public int seedOffset = 10000;

    [Header("Donut & Loop Prevention")]
    public bool preventLoops = true;
    public bool prevent2x2Blocks = true;

    [Header("Escape Phase Collapse Settings")]
    [Tooltip("Automatically make all platforms crumble once the exit portal is opened.")]
    public bool enableEscapeCrumble = true;
    public float crumbleDelaySeconds = 3f;

    public Vector3 StartChunkWorldPosition { get; private set; }
    public bool IsGenerating { get; private set; } = false;

    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, int> cellHeights = new Dictionary<Vector2Int, int>();
    private readonly List<GameObject> spawnedJumpPads = new List<GameObject>();
    private readonly HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> voidMask = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> landmarkCells = new HashSet<Vector2Int>();
    private Vector2Int startCoord;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (useRandomSeed) seedOffset = Random.Range(0, 1000000);
        Random.InitState(seedOffset);
    }

    public Vector2Int GetGridCoord(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.z / chunkSize)
        );
    }

    public bool IsPathCell(Vector2Int coord) => pathCells.Contains(coord);

    public int GetCellHeightTier(Vector2Int coord) => cellHeights.TryGetValue(coord, out int tier) ? tier : 0;

    // --- MAP GENERATION PIPELINE ---

    public void ClearMap()
    {
        foreach (var chunk in activeChunks.Values)
        {
            if (chunk != null) Destroy(chunk);
        }
        activeChunks.Clear();

        foreach (var pad in spawnedJumpPads)
        {
            if (pad != null) Destroy(pad);
        }
        spawnedJumpPads.Clear();

        pathCells.Clear();
        voidMask.Clear();
        cellHeights.Clear();
        landmarkCells.Clear();
    }

    public void GenerateMap()
    {
        StopAllCoroutines();
        StartCoroutine(GenerateMapRoutine());
    }

    private IEnumerator GenerateMapRoutine()
    {
        IsGenerating = true;
        ClearMap();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetItemCount();
        }

        // Lock player in place while generating
        SetPlayerMovementLock(true);

        startCoord = (player != null) ? GetGridCoord(player.position) : Vector2Int.zero;
        bool generationSuccessful = false;

        for (int attempt = 0; attempt < maxGenerationRetries; attempt++)
        {
            if (TryGenerateMapPipeline())
            {
                generationSuccessful = true;
                break;
            }

            seedOffset += 137;
            Random.InitState(seedOffset);
            yield return null; // Spread retries over frames if needed
        }

        if (!generationSuccessful)
        {
            Debug.LogWarning("[MapGenerator] Map generation failed to reach minimum density. Using fallback bounds.");
        }

        // 1. Instantiate layout chunks asynchronously with pop animation
        yield return StartCoroutine(InstantiateMapChunksAsync());

        // 2. Apply random tilts to non-spawn chunks
        ApplyTiltedPlatforms();

        // 3. Spawn jump pads
        SpawnScaledJumpPads();

        // Safety auto-finish check
        if (GameManager.Instance != null && GameManager.Instance.finishPortalPrefab != null)
        {
            if (GameManager.Instance.TotalItems == 0)
            {
                Debug.LogWarning("[MapGenerator] No objective items found on layout! Auto-opening Finish Portal.");
                GameManager.Instance.TriggerFinish();
            }
        }

        // Finalize player position and restore controls
        if (LevelRuleManager.Instance != null)
        {
            LevelRuleManager.Instance.ApplyActiveLevelRules();
        }
        else
        {
            TeleportPlayerToSpawn();
        }

        SetPlayerMovementLock(false);
        IsGenerating = false;
    }

    private bool TryGenerateMapPipeline()
    {
        pathCells.Clear();
        voidMask.Clear();
        cellHeights.Clear();
        landmarkCells.Clear();

        pathCells.Add(startCoord);
        cellHeights[startCoord] = 0;

        Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in cardinalDirs)
        {
            Vector2Int exitTile = startCoord + dir;
            voidMask.Remove(exitTile);
            pathCells.Add(exitTile);
            cellHeights[exitTile] = 0;
        }

        CarveAreaBudgetedVoids();
        List<Vector2Int> crawlerSeeds = GrowOrganicBalancedTrunks();
        GrowOrganicWindingPaths(crawlerSeeds);

        int totalRadialArea = Mathf.RoundToInt(Mathf.PI * mapRadius * mapRadius);
        int availableNonVoidCells = Mathf.Max(10, totalRadialArea - voidMask.Count);
        int minAcceptablePaths = Mathf.RoundToInt(availableNonVoidCells * targetPathDensity * 0.6f);

        return pathCells.Count >= minAcceptablePaths;
    }

    private IEnumerator InstantiateMapChunksAsync()
    {
        float frameStartTime = Time.realtimeSinceStartup;

        // Step A: Spawn Start/Spawn platform immediately so player stands on it right away
        if (pathCells.Contains(startCoord))
        {
            float computedY = chunkYPosition + (GetCellHeightTier(startCoord) * heightStep);
            Vector3 spawnPos = new Vector3(startCoord.x * chunkSize, computedY, startCoord.y * chunkSize);
            StartChunkWorldPosition = spawnPos;

            if (spawnPrefab != null)
            {
                GameObject startChunk = Instantiate(spawnPrefab, spawnPos, Quaternion.identity);
                activeChunks.Add(startCoord, startChunk);
                SpawnItemsInChunk(startChunk, startCoord);
            }

            TeleportPlayerToSpawn();
        }

        // Step B: Instantiate remaining chunks spread over frames
        foreach (Vector2Int coord in pathCells)
        {
            if (coord == startCoord) continue;

            int tier = GetCellHeightTier(coord);
            float computedY = chunkYPosition + (tier * heightStep);
            Vector3 targetSpawnPos = new Vector3(coord.x * chunkSize, computedY, coord.y * chunkSize);

            GameObject chunkInstance = null;
            Quaternion targetRotation = Quaternion.identity;

            if (landmarkCells.Contains(coord) && shrinePrefabs != null && shrinePrefabs.Length > 0)
            {
                int shrineIndex = Random.Range(0, shrinePrefabs.Length);
                if (enableVerticality && enableSlopeRamps)
                {
                    targetRotation = CalculateSlopeRotation(coord, tier);
                }

                chunkInstance = Instantiate(shrinePrefabs[shrineIndex], targetSpawnPos, targetRotation);
                activeChunks.Add(coord, chunkInstance);
            }
            else
            {
                (GameObject prefab, float socketYRotation) = FindMatchingSocketPrefab(coord);
                if (prefab != null)
                {
                    Quaternion socketRotation = Quaternion.Euler(0f, socketYRotation, 0f);
                    targetRotation = socketRotation;

                    if (enableVerticality && enableSlopeRamps)
                    {
                        Quaternion slopeRotation = CalculateSlopeRotation(coord, tier);
                        targetRotation = slopeRotation * socketRotation;
                    }

                    chunkInstance = Instantiate(prefab, targetSpawnPos, targetRotation);
                    activeChunks.Add(coord, chunkInstance);
                    SpawnItemsInChunk(chunkInstance, coord);
                }
            }

            // Trigger Pop-In Animation Routine for Chunk
            if (chunkInstance != null)
            {
                StartCoroutine(AnimateChunkSpawnRoutine(chunkInstance, targetSpawnPos, targetRotation));

                if (enableEscapeCrumble && coord != startCoord)
                {
                    EscapeCollapseChunk crumble = chunkInstance.AddComponent<EscapeCollapseChunk>();
                    crumble.delayBeforeFall = crumbleDelaySeconds;
                }
            }

            // Frame Budgeting: Yield to next frame if execution time exceeds time budget
            if ((Time.realtimeSinceStartup - frameStartTime) * 1000f >= frameTimeBudgetMs)
            {
                yield return null;
                frameStartTime = Time.realtimeSinceStartup;
            }
        }
    }

    private IEnumerator AnimateChunkSpawnRoutine(GameObject chunk, Vector3 targetPos, Quaternion targetRot)
    {
        if (chunk == null) yield break;

        Vector3 startPos = targetPos + Vector3.up * spawnYOffset;
        Vector3 finalScale = chunk.transform.localScale;
        float elapsed = 0f;

        chunk.transform.position = startPos;
        chunk.transform.localScale = Vector3.zero;

        while (elapsed < chunkAnimDuration)
        {
            if (chunk == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / chunkAnimDuration);
            float evaluatedT = spawnPopCurve.Evaluate(t);

            chunk.transform.position = Vector3.Lerp(startPos, targetPos, evaluatedT);
            chunk.transform.localScale = Vector3.Lerp(Vector3.zero, finalScale, evaluatedT);

            yield return null;
        }

        if (chunk != null)
        {
            chunk.transform.position = targetPos;
            chunk.transform.localScale = finalScale;
            chunk.transform.rotation = targetRot;
        }
    }

    private void SetPlayerMovementLock(bool isLocked)
    {
        if (player == null) return;

        // Toggle CharacterController component or custom Movement scripts
        MonoBehaviour playerController = player.GetComponent("PlayerController") as MonoBehaviour;
        if (playerController != null)
        {
            playerController.enabled = !isLocked;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = !isLocked;
        }

        // Freeze physics velocity if using Rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = isLocked;
            if (isLocked) rb.linearVelocity = Vector3.zero;
        }
    }

    // --- SLOPE & RAMP CALCULATION ---

    private Quaternion CalculateSlopeRotation(Vector2Int coord, int currentTier)
    {
        float dz = 0f;
        float dx = 0f;

        Vector2Int north = coord + Vector2Int.up;
        Vector2Int south = coord + Vector2Int.down;
        Vector2Int east = coord + Vector2Int.right;
        Vector2Int west = coord + Vector2Int.left;

        if (IsPathCell(north)) dz += (GetCellHeightTier(north) - currentTier);
        if (IsPathCell(south)) dz -= (GetCellHeightTier(south) - currentTier);

        if (IsPathCell(east)) dx += (GetCellHeightTier(east) - currentTier);
        if (IsPathCell(west)) dx -= (GetCellHeightTier(west) - currentTier);

        if (Mathf.Approximately(dz, 0f) && Mathf.Approximately(dx, 0f))
        {
            return Quaternion.identity;
        }

        float pitchAngle = Mathf.Atan2(dz * heightStep, chunkSize) * Mathf.Rad2Deg;
        float rollAngle = Mathf.Atan2(dx * heightStep, chunkSize) * Mathf.Rad2Deg;

        return Quaternion.Euler(-pitchAngle, 0f, rollAngle);
    }

    // --- LEVEL RULE MODIFIERS & SPAWNING ---

    private void ApplyTiltedPlatforms()
    {
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.HasLevelRule(LevelRuleType.TiltedPlatforms)) return;

        float maxTiltDegrees = GameManager.Instance.GetTotalLevelRuleValue(LevelRuleType.TiltedPlatforms);
        if (maxTiltDegrees <= 0f) return;

        int tiltedCount = 0;
        foreach (var kvp in activeChunks)
        {
            Vector2Int coord = kvp.Key;
            GameObject chunk = kvp.Value;

            if (coord == startCoord || landmarkCells.Contains(coord) || chunk == null) continue;

            float randomX = Random.Range(-maxTiltDegrees, maxTiltDegrees);
            float randomZ = Random.Range(-maxTiltDegrees, maxTiltDegrees);

            chunk.transform.Rotate(randomX, 0f, randomZ, Space.Self);
            tiltedCount++;
        }

        Debug.Log($"[MapGenerator] LevelRule Active: Tilted {tiltedCount} platforms up to ±{maxTiltDegrees}°.");
    }

    private void SpawnScaledJumpPads()
    {
        if (jumpPadPrefab == null || GameManager.Instance == null) return;
        if (!GameManager.Instance.HasLevelRule(LevelRuleType.JumpPad)) return;

        float baseCount = GameManager.Instance.GetTotalLevelRuleValue(LevelRuleType.JumpPad);
        if (baseCount <= 0f) return;

        float safeRefRadius = Mathf.Max(1f, referenceMapRadius);
        float scaleMultiplier = Mathf.Max(1f, mapRadius / safeRefRadius);

        int totalJumpPadsToSpawn = Mathf.Clamp(Mathf.RoundToInt(baseCount * scaleMultiplier), 1, 20);

        List<(Vector2Int pathCoord, Vector2Int dir)> validSpots = GetValidBorderVoidSpots();
        if (validSpots.Count == 0) return;

        List<Vector3> spawnedPadPositions = new List<Vector3>();
        int spawnedCount = 0;
        float minDistanceBetweenPads = chunkSize * 0.75f;

        for (int i = 0; i < totalJumpPadsToSpawn && validSpots.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, validSpots.Count);
            var spot = validSpots[randomIndex];
            validSpots.RemoveAt(randomIndex);

            int tier = GetCellHeightTier(spot.pathCoord);
            float pathY = chunkYPosition + (tier * heightStep);

            Vector3 pathWorldPos = new Vector3(spot.pathCoord.x * chunkSize, pathY, spot.pathCoord.y * chunkSize);
            Vector3 offset = new Vector3(spot.dir.x, 0f, spot.dir.y) * (chunkSize * jumpPadEdgeOffset);
            Vector3 voidWorldPos = pathWorldPos + offset;

            bool tooClose = false;
            foreach (Vector3 existingPadPos in spawnedPadPositions)
            {
                if (Vector3.Distance(voidWorldPos, existingPadPos) < minDistanceBetweenPads)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            GameObject padInstance = Instantiate(jumpPadPrefab, voidWorldPos, Quaternion.identity, transform);
            spawnedJumpPads.Add(padInstance);

            spawnedPadPositions.Add(voidWorldPos);
            spawnedCount++;
        }

        Debug.Log($"[MapGenerator] LevelRule Active: Spawned {spawnedCount} floating Jump Pads.");
    }

    private List<(Vector2Int pathCoord, Vector2Int dir)> GetValidBorderVoidSpots()
    {
        List<(Vector2Int pathCoord, Vector2Int dir)> spots = new List<(Vector2Int, Vector2Int)>();
        HashSet<Vector2Int> visitedVoids = new HashSet<Vector2Int>();
        Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int pathCoord in pathCells)
        {
            if (pathCoord == startCoord) continue;

            foreach (Vector2Int dir in cardinalDirs)
            {
                Vector2Int neighbor = pathCoord + dir;
                if (!IsPathCell(neighbor) && visitedVoids.Add(neighbor))
                {
                    spots.Add((pathCoord, dir));
                }
            }
        }

        return spots;
    }

    public void TeleportPlayerToSpawn()
    {
        if (player == null) return;

        Vector3 targetSpawnPos = StartChunkWorldPosition + new Vector3(0f, 2f, 0f);
        CharacterController cc = player.GetComponent<CharacterController>();

        bool wasCCEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        player.position = targetSpawnPos;

        if (cc != null) cc.enabled = wasCCEnabled;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Gets a random valid generated chunk platform world position.
    /// Optionally filters out chunks marked with MapChunk.isBlacklistedFromSpawn.
    /// </summary>
    public bool TryGetRandomPlatformPosition(out Vector3 platformPosition, bool excludeBlacklisted = true)
    {
        platformPosition = Vector3.zero;

        if (activeChunks == null || activeChunks.Count == 0)
        {
            return false;
        }

        List<GameObject> candidateChunks = new List<GameObject>();

        foreach (var chunk in activeChunks.Values)
        {
            if (chunk == null) continue;

            if (excludeBlacklisted)
            {
                MapChunk mapChunk = chunk.GetComponent<MapChunk>();
                if (mapChunk != null && mapChunk.isBlacklistedFromSpawn)
                {
                    continue; // Skip blacklisted chunk
                }
            }

            candidateChunks.Add(chunk);
        }

        // Fallback: If all chunks happen to be blacklisted, allow any active chunk
        if (candidateChunks.Count == 0)
        {
            foreach (var chunk in activeChunks.Values)
            {
                if (chunk != null) candidateChunks.Add(chunk);
            }
        }

        if (candidateChunks.Count == 0) return false;

        GameObject selectedChunk = candidateChunks[Random.Range(0, candidateChunks.Count)];

        // Check if selected chunk has custom spawn points defined
        MapChunk selectedMapChunk = selectedChunk.GetComponent<MapChunk>();
        if (selectedMapChunk != null && selectedMapChunk.customSpawnPoints != null && selectedMapChunk.customSpawnPoints.Length > 0)
        {
            List<Transform> validPoints = new List<Transform>();
            foreach (var pt in selectedMapChunk.customSpawnPoints)
            {
                if (pt != null) validPoints.Add(pt);
            }

            if (validPoints.Count > 0)
            {
                platformPosition = validPoints[Random.Range(0, validPoints.Count)].position;
                return true;
            }
        }

        platformPosition = selectedChunk.transform.position;
        return true;
    }

    // --- ALGORITHMIC GENERATION STAGES ---

    private List<Vector2Int> GrowOrganicBalancedTrunks()
    {
        Vector2Int[] targetDirections = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int[] stepOptions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        List<Vector2Int> crawlerSeeds = new List<Vector2Int>();
        int targetDistance = Mathf.Max(3, Mathf.RoundToInt(mapRadius * maxTrunkLengthRatio));

        foreach (var targetDir in targetDirections)
        {
            Vector2Int current = startCoord + targetDir;
            crawlerSeeds.Add(current);
            int maxStepBudget = targetDistance * 4;

            int currentHeightTier = 0;
            cellHeights[current] = currentHeightTier;

            for (int step = 0; step < maxStepBudget; step++)
            {
                if (Mathf.RoundToInt(Vector2Int.Distance(current, startCoord)) >= targetDistance)
                {
                    landmarkCells.Add(current);
                    break;
                }

                List<(Vector2Int tile, float weight)> candidates = new List<(Vector2Int, float)>();

                foreach (var dir in stepOptions)
                {
                    Vector2Int neighbor = current + dir;
                    voidMask.Remove(neighbor);

                    if (IsWithinEuclideanRadius(neighbor, mapRadius) && !WouldCreateDonutOrLoop(neighbor, current))
                    {
                        float alignment = Vector2.Dot((Vector2)dir, (Vector2)targetDir);
                        if (alignment >= -0.3f)
                        {
                            float weight = (alignment * trunkForwardBias) + Random.Range(0.4f, 1.8f);
                            candidates.Add((neighbor, weight));
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    candidates.Sort((a, b) => b.weight.CompareTo(a.weight));
                    Vector2Int chosenTile = (Random.value < 0.7f || candidates.Count == 1)
                        ? candidates[0].tile
                        : candidates[Random.Range(0, candidates.Count)].tile;

                    pathCells.Add(chosenTile);

                    if (enableVerticality && Random.value < heightStepChance)
                    {
                        int delta = Random.value < 0.5f ? 1 : -1;
                        currentHeightTier = Mathf.Clamp(currentHeightTier + delta, -maxElevationTier, maxElevationTier);
                    }
                    cellHeights[chosenTile] = currentHeightTier;

                    if (Random.value < trunkBranchSeedChance || step == maxStepBudget - 1)
                    {
                        crawlerSeeds.Add(chosenTile);
                    }

                    current = chosenTile;
                }
                else
                {
                    landmarkCells.Add(current);
                    break;
                }
            }
        }
        return crawlerSeeds;
    }

    private void CarveAreaBudgetedVoids()
    {
        HashSet<Vector2Int> protectedZone = new HashSet<Vector2Int>();
        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                protectedZone.Add(startCoord + new Vector2Int(x, y));
            }
        }

        int totalRadialArea = Mathf.RoundToInt(Mathf.PI * mapRadius * mapRadius);
        int totalTargetVoidTiles = Mathf.RoundToInt(totalRadialArea * targetVoidRatio);

        int bigVoidBudget = Mathf.RoundToInt(totalTargetVoidTiles * bigVoidShare);
        int smallVoidBudget = totalTargetVoidTiles - bigVoidBudget;

        int numBigVoids = Mathf.Max(1, Mathf.RoundToInt(mapRadius * 0.15f));
        int targetTilesPerBigVoid = Mathf.Max(10, bigVoidBudget / numBigVoids);

        for (int i = 0; i < numBigVoids; i++)
        {
            Vector2Int center = GetRandomCoordInRadialMap(protectedZone);
            CarveDrunkardWalkVoid(center, targetTilesPerBigVoid, protectedZone);
        }

        int numSmallVoids = Mathf.Max(3, Mathf.RoundToInt(mapRadius * 0.35f));
        int targetTilesPerSmallVoid = Mathf.Max(3, smallVoidBudget / numSmallVoids);

        for (int i = 0; i < numSmallVoids; i++)
        {
            Vector2Int center = GetRandomCoordInRadialMap(protectedZone);
            CarveDrunkardWalkVoid(center, targetTilesPerSmallVoid, protectedZone);
        }
    }

    private void CarveDrunkardWalkVoid(Vector2Int start, int targetNewTiles, HashSet<Vector2Int> protectedZone)
    {
        Vector2Int current = start;
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int addedTiles = 0;
        int safetyLimit = targetNewTiles * 25;

        while (addedTiles < targetNewTiles && safetyLimit > 0)
        {
            safetyLimit--;
            if (IsWithinEuclideanRadius(current, mapRadius) && !protectedZone.Contains(current))
            {
                if (voidMask.Add(current)) addedTiles++;
            }

            current += dirs[Random.Range(0, dirs.Length)];
            if (!IsWithinEuclideanRadius(current, mapRadius)) current = start;
        }
    }

    private Vector2Int GetRandomCoordInRadialMap(HashSet<Vector2Int> protectedZone)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector2Int coord = new Vector2Int(
                startCoord.x + Random.Range(-mapRadius, mapRadius + 1),
                startCoord.y + Random.Range(-mapRadius, mapRadius + 1)
            );

            if (!protectedZone.Contains(coord) && IsWithinEuclideanRadius(coord, mapRadius))
            {
                return coord;
            }
        }
        return startCoord;
    }

    private void GrowOrganicWindingPaths(List<Vector2Int> initialSeeds)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        List<Vector2Int> activeCrawlers = new List<Vector2Int>(initialSeeds);

        int totalRadialArea = Mathf.RoundToInt(Mathf.PI * mapRadius * mapRadius);
        int availableNonVoidCells = Mathf.Max(10, totalRadialArea - voidMask.Count);
        int targetPathCount = Mathf.RoundToInt(availableNonVoidCells * targetPathDensity);

        int safetyLimit = 35000;

        while (pathCells.Count < targetPathCount && activeCrawlers.Count > 0 && safetyLimit > 0)
        {
            safetyLimit--;
            int randomIndex = Random.Range(0, activeCrawlers.Count);
            Vector2Int current = activeCrawlers[randomIndex];
            int parentTier = GetCellHeightTier(current);

            List<Vector2Int> validSteps = new List<Vector2Int>();
            foreach (var dir in dirs)
            {
                Vector2Int neighbor = current + dir;
                if (IsWithinEuclideanRadius(neighbor, mapRadius) && !voidMask.Contains(neighbor) && !WouldCreateDonutOrLoop(neighbor, current))
                {
                    validSteps.Add(neighbor);
                }
            }

            if (validSteps.Count > 0)
            {
                Vector2Int chosenTile = validSteps[Random.Range(0, validSteps.Count)];
                pathCells.Add(chosenTile);

                int nextTier = parentTier;
                if (enableVerticality && Random.value < heightStepChance)
                {
                    int delta = Random.value < 0.5f ? 1 : -1;
                    nextTier = Mathf.Clamp(parentTier + delta, -maxElevationTier, maxElevationTier);
                }
                cellHeights[chosenTile] = nextTier;

                activeCrawlers[randomIndex] = chosenTile;

                if (Random.value < crawlerBranchChance) activeCrawlers.Add(chosenTile);
            }
            else
            {
                activeCrawlers.RemoveAt(randomIndex);
            }
        }
    }

    private bool IsWithinEuclideanRadius(Vector2Int coord, float maxRadius)
    {
        Vector2Int offset = coord - startCoord;
        return (offset.x * offset.x + offset.y * offset.y) <= (maxRadius * maxRadius);
    }

    private bool WouldCreateDonutOrLoop(Vector2Int candidate, Vector2Int fromTile)
    {
        if (pathCells.Contains(candidate)) return true;

        if (preventLoops)
        {
            Vector2Int[] neighbors = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            int existingNeighbors = 0;

            foreach (var n in neighbors)
            {
                Vector2Int neighborCoord = candidate + n;
                if (neighborCoord != fromTile && pathCells.Contains(neighborCoord))
                {
                    existingNeighbors++;
                }
            }
            if (existingNeighbors > 0) return true;
        }

        if (prevent2x2Blocks)
        {
            Vector2Int[] offsets = { new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };
            foreach (var offset in offsets)
            {
                Vector2Int p2 = candidate + new Vector2Int(offset.x, 0);
                Vector2Int p3 = candidate + new Vector2Int(0, offset.y);
                Vector2Int p4 = candidate + offset;

                if (pathCells.Contains(p2) && pathCells.Contains(p3) && pathCells.Contains(p4))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // --- ITEM SPAWNING & PREFAB MATCHING ---

    private void SpawnItemsInChunk(GameObject chunkInstance, Vector2Int coord)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;

        Transform[] allTransforms = chunkInstance.GetComponentsInChildren<Transform>();
        int spawnPointIndex = 0;

        foreach (Transform child in allTransforms)
        {
            if (child.name.Equals(itemSpawnPointName, System.StringComparison.OrdinalIgnoreCase))
            {
                int hash = Mathf.Abs(((coord.x + seedOffset) * 73856093) ^ ((coord.y + seedOffset) * 19349663) ^ (spawnPointIndex * 83492791));
                int itemIndex = hash % itemPrefabs.Length;
                GameObject selectedItem = itemPrefabs[itemIndex];

                if (selectedItem != null && selectedItem != jumpPadPrefab && !selectedItem.name.Contains("JumpPad"))
                {
                    GameObject itemInstance = Instantiate(selectedItem, child);
                    itemInstance.transform.SetLocalPositionAndRotation(Vector3.zero, selectedItem.transform.localRotation);
                }
                spawnPointIndex++;
            }
        }
    }

    private (GameObject prefab, float rotation) FindMatchingSocketPrefab(Vector2Int coord)
    {
        bool reqN = IsPathCell(coord + Vector2Int.up);
        bool reqE = IsPathCell(coord + Vector2Int.right);
        bool reqS = IsPathCell(coord + Vector2Int.down);
        bool reqW = IsPathCell(coord + Vector2Int.left);

        int hash = Mathf.Abs(((coord.x + seedOffset) * 73856093) ^ ((coord.y + seedOffset) * 19349663));
        float[] possibleRotations = { 0f, 90f, 180f, 270f };
        List<GameObject> shuffledPrefabs = GetShuffledPrefabs(hash);

        foreach (var prefab in shuffledPrefabs)
        {
            if (prefab == null) continue;

            bool hasN = prefab.transform.Find("North") != null;
            bool hasE = prefab.transform.Find("East") != null;
            bool hasS = prefab.transform.Find("South") != null;
            bool hasW = prefab.transform.Find("West") != null;

            foreach (float rot in possibleRotations)
            {
                bool curN = false, curE = false, curS = false, curW = false;

                if (rot == 0f) { curN = hasN; curE = hasE; curS = hasS; curW = hasW; }
                else if (rot == 90f) { curN = hasW; curE = hasN; curS = hasE; curW = hasS; }
                else if (rot == 180f) { curN = hasS; curE = hasW; curS = hasN; curW = hasE; }
                else if (rot == 270f) { curN = hasE; curE = hasS; curS = hasW; curW = hasN; }

                if (curN == reqN && curE == reqE && curS == reqS && curW == reqW)
                {
                    return (prefab, rot);
                }
            }
        }

        foreach (var prefab in shuffledPrefabs)
        {
            if (prefab == null) continue;

            bool hasN = prefab.transform.Find("North") != null;
            bool hasE = prefab.transform.Find("East") != null;
            bool hasS = prefab.transform.Find("South") != null;
            bool hasW = prefab.transform.Find("West") != null;

            foreach (float rot in possibleRotations)
            {
                bool curN = false, curE = false, curS = false, curW = false;

                if (rot == 0f) { curN = hasN; curE = hasE; curS = hasS; curW = hasW; }
                else if (rot == 90f) { curN = hasW; curE = hasN; curS = hasE; curW = hasS; }
                else if (rot == 180f) { curN = hasS; curE = hasW; curS = hasN; curW = hasE; }
                else if (rot == 270f) { curN = hasE; curE = hasS; curS = hasW; curW = hasN; }

                if ((!reqN || curN) && (!reqE || curE) && (!reqS || curS) && (!reqW || curW))
                {
                    return (prefab, rot);
                }
            }
        }

        return (null, 0f);
    }

    private List<GameObject> GetShuffledPrefabs(int hash)
    {
        List<GameObject> list = new List<GameObject>();
        foreach (var p in allChunkPrefabs) if (p != null) list.Add(p);

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = (hash + i) % list.Count;
            GameObject temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }

        return list;
    }
}