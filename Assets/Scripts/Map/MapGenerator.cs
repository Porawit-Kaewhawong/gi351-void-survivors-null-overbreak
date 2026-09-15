using System.Collections.Generic;
using UnityEngine;

public class NullscapeSocketGenerator : MonoBehaviour
{
    [Header("3D World Settings")]
    public Transform player;
    public GameObject spawnPrefab;
    public float chunkSize = 50f;
    public int renderDistance = 4;
    public float chunkYPosition = 0f;

    [Header("Socket Prefab Library")]
    [Tooltip("Drop ALL path prefabs here (Must contain child Empty GameObjects: North, East, South, West)")]
    public GameObject[] allChunkPrefabs;

    [Header("Item / Collectible Spawner Settings")]
    public GameObject[] itemPrefabs;
    public string itemSpawnPointName = "ItemSpawn";

    [Header("Main Path Settings")]
    public bool useRandomSeed = true;
    public int seedOffset = 10000;
    [Range(0.01f, 0.2f)] public float pathFrequency = 0.08f;
    public int pathAmplitude = 3;

    [Header("Branch & Cross-Path Settings")]
    public bool enableSubBranches = true;
    [Range(3, 10)] public int splitInterval = 5;
    [Range(1, 5)] public int maxSubBranchLength = 3;
    [Range(10, 90)] public int splitChance = 60;
    [Range(0, 100)] public int crossPathChance = 30;

    private Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int currentChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int startCoord;

    private static readonly Vector2Int[] EightNeighbors = new Vector2Int[]
    {
        new Vector2Int(0, 1),  new Vector2Int(1, 1),  new Vector2Int(1, 0),  new Vector2Int(1, -1),
        new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1)
    };

    void Start()
    {
        if (useRandomSeed) seedOffset = Random.Range(0, 1000000);

        if (player != null)
        {
            startCoord = GetGridCoord(player.position);
            UpdateChunkPosition();
        }
    }

    void Update()
    {
        if (player == null) return;
        UpdateChunkPosition();
    }

    Vector2Int GetGridCoord(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.z / chunkSize)
        );
    }

    void UpdateChunkPosition()
    {
        Vector2Int newCoord = GetGridCoord(player.position);
        if (newCoord != currentChunkCoord)
        {
            currentChunkCoord = newCoord;
            UpdateChunks();
        }
    }

    void UpdateChunks()
    {
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var chunk in activeChunks)
        {
            if (Vector2Int.Distance(chunk.Key, currentChunkCoord) > renderDistance)
                toRemove.Add(chunk.Key);
        }

        foreach (var coord in toRemove)
        {
            Destroy(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2Int coord = currentChunkCoord + new Vector2Int(x, z);

                if (!activeChunks.ContainsKey(coord) && IsPathCell(coord))
                {
                    Vector3 spawnPos = new Vector3(coord.x * chunkSize, chunkYPosition, coord.y * chunkSize);

                    if (coord == startCoord && spawnPrefab != null)
                    {
                        GameObject spawnChunk = Instantiate(spawnPrefab, spawnPos, Quaternion.identity);
                        activeChunks.Add(coord, spawnChunk);
                        SpawnItemsInChunk(spawnChunk, coord);
                        continue;
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
        }
    }

    // --- ITEM SPAWNING (PRESERVES PREFAB ROTATION) ---

    private void SpawnItemsInChunk(GameObject chunkInstance, Vector2Int coord)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0) return;

        Transform[] allTransforms = chunkInstance.GetComponentsInChildren<Transform>();
        int spawnPointIndex = 0;

        foreach (Transform child in allTransforms)
        {
            if (child.name.Equals(itemSpawnPointName, System.StringComparison.OrdinalIgnoreCase))
            {
                // Seed-based hash to select item type consistently
                int hash = Mathf.Abs(((coord.x + seedOffset) * 73856093) ^
                                     ((coord.y + seedOffset) * 19349663) ^
                                     (spawnPointIndex * 83492791));

                int itemIndex = hash % itemPrefabs.Length;
                GameObject selectedItem = itemPrefabs[itemIndex];

                if (selectedItem != null)
                {
                    // Parent directly to marker and restore the item prefab's local rotation & position
                    GameObject itemInstance = Instantiate(selectedItem, child);
                    itemInstance.transform.localPosition = Vector3.zero;
                    itemInstance.transform.localRotation = selectedItem.transform.localRotation;
                }

                spawnPointIndex++;
            }
        }
    }

    // --- GRID EVALUATION ---

    public bool IsPathCell(Vector2Int coord)
    {
        if (coord == startCoord) return true;

        if (IsOnMainBranch(coord)) return true;

        if (enableSubBranches && IsValidSubBranchCell(coord)) return true;

        return false;
    }

    private bool IsOnMainBranch(Vector2Int coord)
    {
        int dx = coord.x - startCoord.x;
        int dz = coord.y - startCoord.y;

        return IsOnBranchRay(dz, dx, 0f) ||       // North
               IsOnBranchRay(-dz, dx, 500f) ||    // South
               IsOnBranchRay(dx, dz, 1000f) ||    // East
               IsOnBranchRay(-dx, dz, 1500f);     // West
    }

    private bool IsOnBranchRay(int primaryDist, int secondaryCoord, float seedShift)
    {
        if (primaryDist <= 0) return false;

        int targetCurr = GetBranchSwayAt(primaryDist, seedShift);
        int targetPrev = GetBranchSwayAt(primaryDist - 1, seedShift);

        int minTarget = Mathf.Min(targetPrev, targetCurr);
        int maxTarget = Mathf.Max(targetPrev, targetCurr);

        return secondaryCoord >= minTarget && secondaryCoord <= maxTarget;
    }

    private bool IsValidSubBranchCell(Vector2Int coord)
    {
        int dx = coord.x - startCoord.x;
        int dz = coord.y - startCoord.y;

        if (CheckAndValidateSubBranch(dz, dx, 0f, true, coord)) return true;
        if (CheckAndValidateSubBranch(-dz, dx, 500f, true, coord)) return true;
        if (CheckAndValidateSubBranch(dx, dz, 1000f, false, coord)) return true;
        if (CheckAndValidateSubBranch(-dx, dz, 1500f, false, coord)) return true;

        return false;
    }

    private bool CheckAndValidateSubBranch(int primaryDist, int secondaryCoord, float seedShift, bool isVerticalRay, Vector2Int globalCoord)
    {
        if (primaryDist <= 0) return false;

        int splitP = Mathf.RoundToInt((float)primaryDist / splitInterval) * splitInterval;
        if (splitP <= 0 || primaryDist != splitP) return false;

        int splitHash = Mathf.Abs(((splitP + (int)seedShift + seedOffset) * 48271) % 100);
        if (splitHash >= splitChance) return false;

        int trunkPos = GetBranchSwayAt(splitP, seedShift);
        int offset = secondaryCoord - trunkPos;
        if (offset == 0) return false;

        int crossHash = Mathf.Abs(((splitP + (int)seedShift + seedOffset) * 16807) % 100);
        bool isCross = (crossHash < crossPathChance);

        bool isNegativeSide = (offset < 0);
        bool isPositiveSide = (offset > 0);

        if (!isCross)
        {
            bool allowNegative = (splitHash % 2 == 0);
            if (isNegativeSide && !allowNegative) return false;
            if (isPositiveSide && allowNegative) return false;
        }

        int distFromTrunk = Mathf.Abs(offset);
        if (distFromTrunk > maxSubBranchLength) return false;

        Vector2Int rootTile = GetWorldCoordFromRay(splitP, trunkPos, seedShift);
        Vector2Int branchDir = isVerticalRay ? (isNegativeSide ? Vector2Int.left : Vector2Int.right)
                                             : (isNegativeSide ? Vector2Int.down : Vector2Int.up);

        Vector2Int current = rootTile;
        for (int step = 1; step <= distFromTrunk; step++)
        {
            current += branchDir;

            foreach (var nOffset in EightNeighbors)
            {
                Vector2Int neighbor = current + nOffset;

                if (neighbor == current || neighbor == rootTile || neighbor == current - branchDir)
                    continue;

                if (IsLocalMainRayTile(neighbor, splitP, seedShift))
                    continue;

                if (IsOnMainBranch(neighbor))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsLocalMainRayTile(Vector2Int testCoord, int splitP, float seedShift)
    {
        for (int p = splitP - 2; p <= splitP + 2; p++)
        {
            if (p <= 0) continue;
            int sway = GetBranchSwayAt(p, seedShift);
            Vector2Int mainTile = GetWorldCoordFromRay(p, sway, seedShift);
            if (testCoord == mainTile) return true;
        }
        return false;
    }

    private Vector2Int GetWorldCoordFromRay(int primaryDist, int secondaryCoord, float seedShift)
    {
        if (seedShift == 0f) return startCoord + new Vector2Int(secondaryCoord, primaryDist);
        if (seedShift == 500f) return startCoord + new Vector2Int(secondaryCoord, -primaryDist);
        if (seedShift == 1000f) return startCoord + new Vector2Int(primaryDist, secondaryCoord);
        return startCoord + new Vector2Int(-primaryDist, secondaryCoord);
    }

    private int GetBranchSwayAt(int primaryDist, float seedShift)
    {
        float noiseStart = Mathf.PerlinNoise((0 + seedOffset + seedShift) * pathFrequency, 0.5f);
        float noiseCurr = Mathf.PerlinNoise((primaryDist + seedOffset + seedShift) * pathFrequency, 0.5f);
        return Mathf.RoundToInt((noiseCurr - noiseStart) * 2f * pathAmplitude);
    }

    // --- SOCKET PREFAB MATCHING ---

    private (GameObject prefab, float rotation) FindMatchingSocketPrefab(Vector2Int coord)
    {
        bool reqN = IsPathCell(coord + Vector2Int.up);
        bool reqE = IsPathCell(coord + Vector2Int.right);
        bool reqS = IsPathCell(coord + Vector2Int.down);
        bool reqW = IsPathCell(coord + Vector2Int.left);

        int hash = Mathf.Abs(((coord.x + seedOffset) * 73856093) ^ ((coord.y + seedOffset) * 19349663));
        float[] possibleRotations = new float[] { 0f, 90f, 180f, 270f };

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
                if (rot == 90f) { curN = hasW; curE = hasN; curS = hasE; curW = hasS; }
                if (rot == 180f) { curN = hasS; curE = hasW; curS = hasN; curW = hasE; }
                if (rot == 270f) { curN = hasE; curE = hasS; curS = hasW; curW = hasN; }

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
                if (rot == 90f) { curN = hasW; curE = hasN; curS = hasE; curW = hasS; }
                if (rot == 180f) { curN = hasS; curE = hasW; curS = hasN; curW = hasE; }
                if (rot == 270f) { curN = hasE; curE = hasS; curS = hasW; curW = hasN; }

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