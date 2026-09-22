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
    [Tooltip("Offset multiplier from platform center toward void (0.5 = exact platform edge, 0.55 - 0.6 = floating just outside edge).")]
    [Range(0.1f, 1.0f)] public float jumpPadEdgeOffset = 0.58f;

    [Header("Generator Seed Settings")]
    public bool useRandomSeed = true;
    public int seedOffset = 10000;

    [Header("Donut & Loop Prevention")]
    public bool preventLoops = true;
    public bool prevent2x2Blocks = true;

    public Vector3 StartChunkWorldPosition { get; private set; }

    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
    private readonly List<GameObject> spawnedJumpPads = new List<GameObject>();
    private readonly HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> voidMask = new HashSet<Vector2Int>();
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

    // --- MAP GENERATION PIPELINE ---

    public void ClearMap()
    {
        // 1. Clear Active Chunks
        foreach (var chunk in activeChunks.Values)
        {
            if (chunk != null) Destroy(chunk);
        }
        activeChunks.Clear();

        // 2. Clear dynamically spawned Jump Pads
        foreach (var pad in spawnedJumpPads)
        {
            if (pad != null) Destroy(pad);
        }
        spawnedJumpPads.Clear();

        pathCells.Clear();
        voidMask.Clear();
    }

    public void GenerateMap()
    {
        ClearMap();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetItemCount();
        }

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
        }

        if (!generationSuccessful)
        {
            Debug.LogWarning("[MapGenerator] Map generation failed to reach minimum density. Using fallback bounds.");
        }

        // 1. Instantiate layout chunks
        InstantiateMapChunks();

        // 2. Apply random tilts to non-spawn chunks
        ApplyTiltedPlatforms();

        // 3. Spawn jump pads (inheriting platform rotation/tilt)
        SpawnScaledJumpPads();

        // Safety auto-finish check if layout has no items spawned
        if (GameManager.Instance != null && GameManager.Instance.finishPortalPrefab != null)
        {
            if (GameManager.Instance.TotalItems == 0)
            {
                Debug.LogWarning("[MapGenerator] No objective items found on layout! Auto-opening Finish Portal.");
                GameManager.Instance.TriggerFinish();
            }
        }

        TeleportPlayerToSpawn();
    }

    private bool TryGenerateMapPipeline()
    {
        pathCells.Clear();
        voidMask.Clear();

        pathCells.Add(startCoord);
        Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in cardinalDirs)
        {
            Vector2Int exitTile = startCoord + dir;
            voidMask.Remove(exitTile);
            pathCells.Add(exitTile);
        }

        CarveAreaBudgetedVoids();
        List<Vector2Int> crawlerSeeds = GrowOrganicBalancedTrunks();
        GrowOrganicWindingPaths(crawlerSeeds);

        int totalRadialArea = Mathf.RoundToInt(Mathf.PI * mapRadius * mapRadius);
        int availableNonVoidCells = Mathf.Max(10, totalRadialArea - voidMask.Count);
        int minAcceptablePaths = Mathf.RoundToInt(availableNonVoidCells * targetPathDensity * 0.6f);

        return pathCells.Count >= minAcceptablePaths;
    }

    private void InstantiateMapChunks()
    {
        foreach (Vector2Int coord in pathCells)
        {
            Vector3 spawnPos = new Vector3(coord.x * chunkSize, chunkYPosition, coord.y * chunkSize);

            if (coord == startCoord)
            {
                StartChunkWorldPosition = spawnPos;
                if (spawnPrefab != null)
                {
                    GameObject spawnChunk = Instantiate(spawnPrefab, spawnPos, Quaternion.identity);
                    activeChunks.Add(coord, spawnChunk);
                    SpawnItemsInChunk(spawnChunk, coord);
                    continue;
                }
            }

            (GameObject prefab, float rotation) = FindMatchingSocketPrefab(coord);
            if (prefab != null)
            {
                GameObject chunk = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, rotation, 0f));
                activeChunks.Add(coord, chunk);
                SpawnItemsInChunk(chunk, coord);
            }
        }
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

            if (coord == startCoord || chunk == null) continue;

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

        int totalJumpPadsToSpawn = Mathf.Clamp(Mathf.RoundToInt(baseCount * scaleMultiplier), 1, 15);

        List<(Vector2Int pathCoord, Vector2Int dir)> validSpots = GetValidBorderVoidSpots();
        if (validSpots.Count == 0)
        {
            Debug.LogWarning("[MapGenerator] No valid empty void cells found around chunks to place Jump Pads.");
            return;
        }

        List<Vector3> spawnedPadPositions = new List<Vector3>();
        int spawnedCount = 0;
        float minDistanceBetweenPads = chunkSize * 0.75f;

        for (int i = 0; i < totalJumpPadsToSpawn && validSpots.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, validSpots.Count);
            var spot = validSpots[randomIndex];
            validSpots.RemoveAt(randomIndex);

            // Calculate position relative to path chunk edge instead of center of empty void
            Vector3 pathWorldPos = new Vector3(spot.pathCoord.x * chunkSize, chunkYPosition, spot.pathCoord.y * chunkSize);
            Vector3 offset = new Vector3(spot.dir.x, 0f, spot.dir.y) * (chunkSize * jumpPadEdgeOffset);
            Vector3 voidWorldPos = pathWorldPos + offset;

            bool tooCloseToOtherPad = false;
            foreach (Vector3 existingPadPos in spawnedPadPositions)
            {
                if (Vector3.Distance(voidWorldPos, existingPadPos) < minDistanceBetweenPads)
                {
                    tooCloseToOtherPad = true;
                    break;
                }
            }

            if (tooCloseToOtherPad) continue;

            GameObject padInstance = Instantiate(jumpPadPrefab, voidWorldPos, Quaternion.identity, transform);
            spawnedJumpPads.Add(padInstance);

            spawnedPadPositions.Add(voidWorldPos);
            spawnedCount++;
        }

        Debug.Log($"[MapGenerator] LevelRule Active: Spawned {spawnedCount} floating Jump Pads near platform borders.");
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

        if (cc != null) cc.enabled = false;
        player.position = targetSpawnPos;
        if (cc != null) cc.enabled = true;

        Physics.SyncTransforms();
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

            for (int step = 0; step < maxStepBudget; step++)
            {
                if (Mathf.RoundToInt(Vector2Int.Distance(current, startCoord)) >= targetDistance) break;

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

                    if (Random.value < trunkBranchSeedChance || step == maxStepBudget - 1)
                    {
                        crawlerSeeds.Add(chosenTile);
                    }

                    current = chosenTile;
                }
                else break;
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
