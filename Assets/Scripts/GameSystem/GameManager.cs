using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// --- STAT ENUMS & TYPES ---

public enum StatType
{
    None,
    MoveSpeed,
    JumpHeight,
    PickupRadius,
    MaxShield
}

public enum LobbySelectionType
{
    PlayerBuff = 0,
    EnemySelection = 1,
    EnemyBuff = 2,
    LevelRule = 3
}

// --- CLASS INHERITANCE FOR CLEAN INSPECTORS ---

[System.Serializable]
public class LobbyOption
{
    public string title;
    [TextArea] public string description;

    [Header("Visuals")]
    public GameObject prefab3D;
    public string optionID;
}

[System.Serializable]
public class PlayerBuffOption : LobbyOption
{
    [Header("Buff Configuration")]
    [Tooltip("Select which stat this buff targets.")]
    public StatType targetStat = StatType.None;

    [Tooltip("Percentage bonus value (e.g., 25 = +25%).")]
    public float buffValue = 0f;
}

[System.Serializable]
public class EnemySelectionOption : LobbyOption
{
    [Header("Enemy Configuration")]
    public GameObject levelSpawnPrefab;

    [Tooltip("Number of enemies spawned per selection. Stacks when selected multiple times.")]
    public int enemyCount = 1;
}

[System.Serializable]
public class EnemyBuffOption : LobbyOption
{
    [Header("Enemy Buff Configuration")]
    public float enemyStatMultiplier = 1f;
}

[System.Serializable]
public class LevelRuleOption : LobbyOption
{
    [Header("Level Rule Configuration")]
    public string ruleType;
}

// --- MAIN GAME MANAGER ---

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI & Objective Settings")]
    public TextMeshProUGUI itemCounterText;
    public string displayFormat = "Items: {0} / {1}";

    [Header("Level Completion & Finish Portal")]
    public GameObject finishPortalPrefab;
    public Vector3 finishOffsetAboveStart = new Vector3(0f, 1f, 0f);

    [Header("Lobby Environment & Spawn Setup")]
    public Transform lobbySpawnPoint;
    public GameObject lobbyEnvironmentRoot;
    public Transform[] optionSpawnPoints;
    public Transform startPortalSpawnPoint;
    public GameObject startPortalPrefab;

    [Header("Enemy Spawning & Surface Settings")]
    [Tooltip("Preferred minimum distance from player start position.")]
    public float minEnemySpawnDistance = 6f;

    [Tooltip("Preferred maximum distance from player start position.")]
    public float maxEnemySpawnDistance = 16f;

    [Tooltip("Select the Layer(s) used for ground/platforms. Set to 'Everything' or leave unassigned to check all colliders.")]
    public LayerMask groundLayer;

    [Tooltip("Height above start position from which downward raycast scans.")]
    public float raycastStartHeight = 15f;

    [Tooltip("Maximum downward scan distance.")]
    public float maxRaycastDistance = 35f;

    [Header("Progression State")]
    public int currentLevel = 1;

    [Header("Option Pools")]
    public List<PlayerBuffOption> playerBuffOptions = new List<PlayerBuffOption>();
    public List<EnemySelectionOption> enemySelectionOptions = new List<EnemySelectionOption>();
    public List<EnemyBuffOption> enemyBuffOptions = new List<EnemyBuffOption>();
    public List<LevelRuleOption> levelRuleOptions = new List<LevelRuleOption>();

    private int totalItems = 0;
    private int collectedItems = 0;
    private bool isFinished = false;

    private GameObject activeFinishPortal;
    private GameObject activeStartPortal;
    private Coroutine activeSpawnCoroutine;

    private readonly List<GameObject> activeSpawnedPedestals = new List<GameObject>();
    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    // Contains all accumulated choices across levels
    public List<LobbyOption> ActiveModifiers { get; private set; } = new List<LobbyOption>();

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
        OpenLobbyForCurrentLevel();
        UpdateUI();
    }

    // --- STRONGLY-TYPED BUFF LOOKUP ---

    public float GetTotalBuffValue(StatType stat)
    {
        float totalPercent = 0f;

        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is PlayerBuffOption buff && buff.targetStat == stat)
            {
                totalPercent += buff.buffValue;
            }
        }

        return totalPercent;
    }

    // --- ITEM & OBJECTIVE TRACKING ---

    public void ResetItemCount()
    {
        totalItems = 0;
        collectedItems = 0;
        isFinished = false;

        if (activeFinishPortal != null)
        {
            Destroy(activeFinishPortal);
            activeFinishPortal = null;
        }

        UpdateUI();
    }

    public void RegisterItem()
    {
        totalItems++;
        UpdateUI();
    }

    public void UnregisterItem()
    {
        totalItems = Mathf.Max(0, totalItems - 1);
        UpdateUI();
    }

    public void OnItemCollected()
    {
        if (isFinished) return;

        collectedItems++;
        UpdateUI();

        if (totalItems > 0 && collectedItems >= totalItems)
        {
            TriggerFinish();
        }
    }

    private void UpdateUI()
    {
        if (itemCounterText != null)
        {
            itemCounterText.text = string.Format(displayFormat, collectedItems, totalItems);
        }
    }

    public void TriggerFinish()
    {
        if (isFinished) return;
        isFinished = true;

        if (finishPortalPrefab != null && MapGenerator.Instance != null && activeFinishPortal == null)
        {
            Vector3 spawnPosition = MapGenerator.Instance.StartChunkWorldPosition + finishOffsetAboveStart;
            activeFinishPortal = Instantiate(finishPortalPrefab, spawnPosition, Quaternion.identity);
            Debug.Log("[GameManager] Level Complete! Finish Portal spawned.");
        }
    }

    // --- LOBBY SELECTION ---

    public LobbySelectionType GetSelectionTypeForLevel(int level)
    {
        return (LobbySelectionType)((level - 1) % 4);
    }

    public void OpenLobbyForCurrentLevel()
    {
        ClearLobbyObjects();
        LobbySelectionType currentType = GetSelectionTypeForLevel(currentLevel);
        Spawn3DSelectionOptions(currentType);
    }

    private void Spawn3DSelectionOptions(LobbySelectionType type)
    {
        List<LobbyOption> optionsPool = FetchOptionPoolForType(type);
        int count = Mathf.Min(optionsPool.Count, optionSpawnPoints.Length);

        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = optionSpawnPoints[i];
            LobbyOption optionData = optionsPool[i];

            GameObject spawnedObj;
            if (optionData.prefab3D != null)
            {
                spawnedObj = Instantiate(optionData.prefab3D, spawnPoint.position, spawnPoint.rotation, spawnPoint);
            }
            else
            {
                spawnedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spawnedObj.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                spawnedObj.transform.SetParent(spawnPoint);
            }

            LobbySelection pedestal = spawnedObj.GetComponent<LobbySelection>() ?? spawnedObj.AddComponent<LobbySelection>();
            pedestal.SetupPedestal(optionData, type);
            activeSpawnedPedestals.Add(spawnedObj);
        }
    }

    public void ConfirmSelection(LobbyOption chosenOption, LobbySelectionType type)
    {
        if (chosenOption != null)
        {
            ActiveModifiers.Add(chosenOption);
            Debug.Log($"[GameManager] Selection Confirmed: '{chosenOption.title}'");
        }

        ClearLobbyObjects();
        Spawn3DStartPortal();
    }

    private void Spawn3DStartPortal()
    {
        if (startPortalPrefab != null && startPortalSpawnPoint != null)
        {
            activeStartPortal = Instantiate(startPortalPrefab, startPortalSpawnPoint.position, startPortalSpawnPoint.rotation, startPortalSpawnPoint);
            if (activeStartPortal.GetComponent<ToLevelPortal>() == null)
            {
                activeStartPortal.AddComponent<ToLevelPortal>();
            }
        }
        else
        {
            StartSelectedLevel();
        }
    }

    // --- LEVEL & PLAYER TRANSITIONS ---

    public void StartSelectedLevel()
    {
        ClearLobbyObjects();

        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(false);
        }

        Vector3 initialSpawnPosition = Vector3.zero;

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.GenerateMap();
            initialSpawnPosition = MapGenerator.Instance.StartChunkWorldPosition;
        }
        else
        {
            Transform playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (playerTransform != null) initialSpawnPosition = playerTransform.position;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.ResetHealthAndShield();

        if (activeSpawnCoroutine != null)
        {
            StopCoroutine(activeSpawnCoroutine);
        }

        activeSpawnCoroutine = StartCoroutine(SpawnEnemiesRoutine(3f, initialSpawnPosition));
    }

    private IEnumerator SpawnEnemiesRoutine(float delaySeconds, Vector3 spawnOrigin)
    {
        yield return new WaitForSeconds(delaySeconds);

        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is EnemySelectionOption enemyOpt && enemyOpt.levelSpawnPrefab != null)
            {
                int countToSpawn = Mathf.Max(1, enemyOpt.enemyCount);

                for (int i = 0; i < countToSpawn; i++)
                {
                    if (TryGetValidPlatformPosition(spawnOrigin, out Vector3 validSpawnPos))
                    {
                        GameObject spawnedEnemy = Instantiate(enemyOpt.levelSpawnPrefab, validSpawnPos, Quaternion.identity);
                        activeEnemies.Add(spawnedEnemy);
                    }
                    else
                    {
                        Debug.LogWarning($"[GameManager] Could not find platform for '{enemyOpt.title}'. Using fallback ground point directly at start origin.");
                        Vector3 fallbackPos = spawnOrigin + Vector3.up * 0.5f;
                        GameObject spawnedEnemy = Instantiate(enemyOpt.levelSpawnPrefab, fallbackPos, Quaternion.identity);
                        activeEnemies.Add(spawnedEnemy);
                    }
                }
            }
        }

        activeSpawnCoroutine = null;
    }

    /// <summary>
    /// Scans for solid platform colliders using an adaptive search radius and raycasts.
    /// </summary>
    private bool TryGetValidPlatformPosition(Vector3 origin, out Vector3 validPosition)
    {
        int maxAttempts = 30;
        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            // First 15 attempts try configured outer radius.
            // Remaining attempts dynamically shrink search radius inwards towards origin to guarantee finding chunks.
            float currentMin = (attempt < 15) ? minEnemySpawnDistance : 2f;
            float currentMax = (attempt < 15) ? maxEnemySpawnDistance : Mathf.Max(5f, minEnemySpawnDistance);
            float randomDistance = Random.Range(currentMin, currentMax);

            Vector3 targetXZ = origin + new Vector3(randomDirection.x * randomDistance, 0f, randomDirection.y * randomDistance);
            Vector3 rayStartPoint = new Vector3(targetXZ.x, origin.y + raycastStartHeight, targetXZ.z);

            if (Physics.Raycast(rayStartPoint, Vector3.down, out RaycastHit hit, maxRaycastDistance, layerMaskToUse, QueryTriggerInteraction.Ignore))
            {
                // Ensure hit object is a solid surface (not a trigger)
                if (!hit.collider.isTrigger)
                {
                    validPosition = hit.point + Vector3.up * 0.5f;
                    Debug.DrawRay(rayStartPoint, Vector3.down * hit.distance, Color.green, 5f);
                    return true;
                }
            }

            Debug.DrawRay(rayStartPoint, Vector3.down * maxRaycastDistance, Color.red, 2f);
        }

        validPosition = Vector3.zero;
        return false;
    }

    public void OnLevelCompleted()
    {
        currentLevel++;
        ClearEnemies();

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.ClearMap();
            MapGenerator.Instance.mapRadius++;
        }

        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(true);
        }

        TeleportPlayerToLobby();
        OpenLobbyForCurrentLevel();
    }

    public void OnPlayerDied()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ClearEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();
    }

    private void TeleportPlayerToLobby()
    {
        if (lobbySpawnPoint == null) return;

        Transform playerTransform = (MapGenerator.Instance != null && MapGenerator.Instance.player != null)
            ? MapGenerator.Instance.player
            : GameObject.FindWithTag("Player")?.transform;

        if (playerTransform != null)
        {
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.SetPositionAndRotation(lobbySpawnPoint.position, lobbySpawnPoint.rotation);

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }
    }

    private void ClearLobbyObjects()
    {
        foreach (var obj in activeSpawnedPedestals)
        {
            if (obj != null) Destroy(obj);
        }
        activeSpawnedPedestals.Clear();

        if (activeStartPortal != null)
        {
            Destroy(activeStartPortal);
            activeStartPortal = null;
        }
    }

    private List<LobbyOption> FetchOptionPoolForType(LobbySelectionType type)
    {
        List<LobbyOption> sourcePool = new List<LobbyOption>();

        switch (type)
        {
            case LobbySelectionType.PlayerBuff:
                sourcePool.AddRange(playerBuffOptions);
                break;
            case LobbySelectionType.EnemySelection:
                sourcePool.AddRange(enemySelectionOptions);
                break;
            case LobbySelectionType.EnemyBuff:
                sourcePool.AddRange(enemyBuffOptions);
                break;
            case LobbySelectionType.LevelRule:
                sourcePool.AddRange(levelRuleOptions);
                break;
        }

        List<LobbyOption> selectedChoices = new List<LobbyOption>();
        List<LobbyOption> tempPool = new List<LobbyOption>(sourcePool);
        int choicesToPick = Mathf.Min(optionSpawnPoints.Length, tempPool.Count);

        for (int i = 0; i < choicesToPick; i++)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            selectedChoices.Add(tempPool[randomIndex]);
            tempPool.RemoveAt(randomIndex);
        }

        return selectedChoices;
    }
}