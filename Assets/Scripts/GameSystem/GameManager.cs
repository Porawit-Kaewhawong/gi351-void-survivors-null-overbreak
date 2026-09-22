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

public enum EnemyStatType
{
    None,
    ExtraEnemyCount,
    MoveSpeed,
    Health
}

public enum LobbySelectionType
{
    PlayerWeapon = 0,      // Level 1, 6, 11...
    EnemySelection = 1, // Level 2, 7, 12...
    PlayerBuff = 2,     // Level 3, 8, 13...
    EnemyBuff = 3,      // Level 4, 9, 14...
    LevelRule = 4    // Level 5, 10, 15...
}

public enum LevelRuleType
{
    None,
    TiltedPlatforms, // Value = Max Angle of tilt
    TiltedPlatform,  // Alias for singular lookup
    LowerGravity,    // Value = % Reduction (e.g., 50 = -50% gravity)
    RandomSpawn,     // Value = Enable toggle (1 = true)
    JumpPad,         // Value = Number of jump pads to spawn
    Round2           // Value = % Chance to trigger replay
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
public class PlayerWeaponOption : LobbyOption
{
    [Header("Weapon Configuration")]
    [Tooltip("Prefab containing the Auto-Attack Weapon script (e.g., Gun, Aura, Lightning Staff).")]
    public GameObject weaponPrefab;
}

[System.Serializable]
public class EnemySelectionOption : LobbyOption
{
    [Header("Enemy Configuration")]
    public GameObject levelSpawnPrefab;

    [Tooltip("Base number of enemies spawned per selection.")]
    public int enemyCount = 1;
}

[System.Serializable]
public class EnemyBuffOption : LobbyOption
{
    [Header("Enemy Buff Configuration")]
    [Tooltip("Select which enemy stat this buff targets.")]
    public EnemyStatType targetStat = EnemyStatType.None;

    [Tooltip("Value bonus. Percentage for Health/Speed (e.g., 25 = +25%). Flat addition for ExtraEnemyCount (e.g., 2 = +2 enemies per group).")]
    public float buffValue = 0f;
}

[System.Serializable]
public class LevelRuleOption : LobbyOption
{
    [Header("Level Rule Configuration")]
    [Tooltip("Select the Level Rule rule type.")]
    public LevelRuleType ruleType = LevelRuleType.None;

    [Tooltip("Rule modifier parameter (e.g., 25 = 25% platform tilt, 50 = 50% gravity reduction).")]
    public float ruleValue = 0f;
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
    public float minEnemySpawnDistance = 12f;

    [Tooltip("Preferred maximum distance from player start position.")]
    public float maxEnemySpawnDistance = 22f;

    [Tooltip("Select the Layer(s) used for ground/platforms. Set to 'Everything' or leave unassigned to check all colliders.")]
    public LayerMask groundLayer;

    [Tooltip("Height above start position from which downward raycast scans.")]
    public float raycastStartHeight = 15f;

    [Tooltip("Maximum downward scan distance.")]
    public float maxRaycastDistance = 35f;

    [Header("Progression State")]
    public int currentLevel = 1;

    [Header("Option Pools")]
    public List<PlayerWeaponOption> playerWeaponOptions = new List<PlayerWeaponOption>();
    public List<EnemySelectionOption> enemySelectionOptions = new List<EnemySelectionOption>();
    public List<PlayerBuffOption> playerBuffOptions = new List<PlayerBuffOption>();
    public List<EnemyBuffOption> enemyBuffOptions = new List<EnemyBuffOption>();
    public List<LevelRuleOption> levelRuleOptions = new List<LevelRuleOption>();

    public int TotalItems { get; private set; } = 0;
    public int CollectedItems { get; private set; } = 0;

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

    // --- STRONGLY-TYPED MODIFIER LOOKUPS ---

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

    public float GetTotalEnemyBuffValue(EnemyStatType stat)
    {
        float total = 0f;

        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is EnemyBuffOption buff && buff.targetStat == stat)
            {
                total += buff.buffValue;
            }
        }

        return total;
    }

    public bool HasLevelRule(LevelRuleType rule)
    {
        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is LevelRuleOption ruleOpt)
            {
                if (ruleOpt.ruleType == rule) return true;
                if ((rule == LevelRuleType.TiltedPlatforms || rule == LevelRuleType.TiltedPlatform) &&
                    (ruleOpt.ruleType == LevelRuleType.TiltedPlatforms || ruleOpt.ruleType == LevelRuleType.TiltedPlatform))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public float GetTotalLevelRuleValue(LevelRuleType rule)
    {
        float total = 0f;

        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is LevelRuleOption ruleOpt)
            {
                if (ruleOpt.ruleType == rule)
                {
                    total += ruleOpt.ruleValue;
                }
                else if ((rule == LevelRuleType.TiltedPlatforms || rule == LevelRuleType.TiltedPlatform) &&
                         (ruleOpt.ruleType == LevelRuleType.TiltedPlatforms || ruleOpt.ruleType == LevelRuleType.TiltedPlatform))
                {
                    total += ruleOpt.ruleValue;
                }
            }
        }

        return total;
    }

    public bool HasLevelRule(string ruleName)
    {
        if (System.Enum.TryParse(ruleName, true, out LevelRuleType ruleType))
        {
            return HasLevelRule(ruleType);
        }
        return false;
    }

    public float GetTotalLevelRuleValue(string ruleName)
    {
        if (System.Enum.TryParse(ruleName, true, out LevelRuleType ruleType))
        {
            return GetTotalLevelRuleValue(ruleType);
        }
        return 0f;
    }

    // --- ITEM & OBJECTIVE TRACKING ---

    public void ResetItemCount()
    {
        TotalItems = 0;
        CollectedItems = 0;
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
        TotalItems++;
        UpdateUI();
    }

    public void UnregisterItem()
    {
        TotalItems = Mathf.Max(0, TotalItems - 1);
        UpdateUI();
    }

    public void OnItemCollected()
    {
        if (isFinished) return;

        CollectedItems++;
        UpdateUI();

        if (TotalItems > 0 && CollectedItems >= TotalItems)
        {
            TriggerFinish();
        }
    }

    private void UpdateUI()
    {
        if (itemCounterText != null)
        {
            itemCounterText.text = string.Format(displayFormat, CollectedItems, TotalItems);
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
        return (LobbySelectionType)((level - 1) % 5);
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
            // Prevent duplicate registration if physics trigger fires twice in one frame
            if (!ActiveModifiers.Contains(chosenOption))
            {
                ActiveModifiers.Add(chosenOption);
                Debug.Log($"[GameManager] Selection Confirmed: '{chosenOption.title}' (Total Active Modifiers: {ActiveModifiers.Count})");
            }
            else
            {
                Debug.LogWarning($"[GameManager] Prevented duplicate trigger for '{chosenOption.title}'.");
                return; // Exit early to avoid spawning double portals
            }
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

    private void EquipPlayerWeapons(Transform playerTransform)
    {
        if (playerTransform == null) return;

        Transform weaponContainer = playerTransform.Find("WeaponContainer");
        if (weaponContainer == null)
        {
            GameObject containerObj = new GameObject("WeaponContainer");
            containerObj.transform.SetParent(playerTransform);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            weaponContainer = containerObj.transform;
        }

        foreach (Transform child in weaponContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is PlayerWeaponOption weaponOpt && weaponOpt.weaponPrefab != null)
            {
                Instantiate(weaponOpt.weaponPrefab, weaponContainer);
            }
        }
    }

    private IEnumerator SpawnEnemiesRoutine(float delaySeconds, Vector3 spawnOrigin)
    {
        yield return new WaitForSeconds(delaySeconds);

        List<EnemySelectionOption> activeEnemyTypes = new List<EnemySelectionOption>();
        foreach (var modifier in ActiveModifiers)
        {
            if (modifier is EnemySelectionOption enemyOpt && enemyOpt.levelSpawnPrefab != null)
            {
                activeEnemyTypes.Add(enemyOpt);
            }
        }

        if (activeEnemyTypes.Count == 0) yield break;

        foreach (var enemyOpt in activeEnemyTypes)
        {
            for (int i = 0; i < enemyOpt.enemyCount; i++)
            {
                SpawnSingleEnemy(enemyOpt.levelSpawnPrefab, spawnOrigin);
            }
        }

        int extraEnemyCount = Mathf.RoundToInt(GetTotalEnemyBuffValue(EnemyStatType.ExtraEnemyCount));

        for (int i = 0; i < extraEnemyCount; i++)
        {
            int randomIndex = Random.Range(0, activeEnemyTypes.Count);
            GameObject randomPrefab = activeEnemyTypes[randomIndex].levelSpawnPrefab;

            SpawnSingleEnemy(randomPrefab, spawnOrigin);
        }

        activeSpawnCoroutine = null;
    }

    private void SpawnSingleEnemy(GameObject prefab, Vector3 spawnOrigin)
    {
        if (TryGetValidPlatformPosition(spawnOrigin, out Vector3 validSpawnPos))
        {
            GameObject spawnedEnemy = Instantiate(prefab, validSpawnPos, Quaternion.identity);
            activeEnemies.Add(spawnedEnemy);
        }
        else
        {
            Vector3 fallbackPos = spawnOrigin + Vector3.up * 0.5f;
            GameObject spawnedEnemy = Instantiate(prefab, fallbackPos, Quaternion.identity);
            activeEnemies.Add(spawnedEnemy);
        }
    }

    private bool TryGetValidPlatformPosition(Vector3 origin, out Vector3 validPosition)
    {
        int maxAttempts = 30;
        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minEnemySpawnDistance, maxEnemySpawnDistance);

            Vector3 targetXZ = origin + new Vector3(randomDirection.x * randomDistance, 0f, randomDirection.y * randomDistance);
            Vector3 rayStartPoint = new Vector3(targetXZ.x, origin.y + raycastStartHeight, targetXZ.z);

            if (Physics.Raycast(rayStartPoint, Vector3.down, out RaycastHit hit, maxRaycastDistance, layerMaskToUse, QueryTriggerInteraction.Ignore))
            {
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

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.ResetHealthAndShield();
            EquipPlayerWeapons(player.transform);
        }

        if (LevelRuleManager.Instance != null)
        {
            LevelRuleManager.Instance.ApplyActiveLevelRules();
        }

        if (activeSpawnCoroutine != null)
        {
            StopCoroutine(activeSpawnCoroutine);
        }

        activeSpawnCoroutine = StartCoroutine(SpawnEnemiesRoutine(3f, initialSpawnPosition));
    }

    public void OnLevelCompleted()
    {
        if (LevelRuleManager.Instance != null && LevelRuleManager.Instance.CheckShouldRepeatLevelForRound2())
        {
            Debug.Log("[GameManager] Round 2 Active! Restarting current level without incrementing level index.");
            StartSelectedLevel();
            return;
        }

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
            case LobbySelectionType.PlayerWeapon:
                sourcePool.AddRange(playerWeaponOptions);
                break;

            case LobbySelectionType.EnemySelection:
                sourcePool.AddRange(enemySelectionOptions);
                break;

            case LobbySelectionType.PlayerBuff:
                sourcePool.AddRange(playerBuffOptions);
                break;

            case LobbySelectionType.EnemyBuff:
                sourcePool.AddRange(enemyBuffOptions);
                break;

            case LobbySelectionType.LevelRule:
                foreach (var rule in levelRuleOptions)
                {
                    bool alreadyChosen = ActiveModifiers.Exists(m => m is LevelRuleOption activeRule && activeRule.optionID == rule.optionID);
                    if (!alreadyChosen)
                    {
                        sourcePool.Add(rule);
                    }
                }
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