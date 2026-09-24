using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

// --- STAT ENUMS & TYPES ---

public enum StatType
{
    None,
    MoveSpeed,
    JumpHeight,
    PickupRadius,
    MaxShield,
    ExtraJumps
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
    PlayerWeapon = 0,
    EnemySelection = 1,
    PlayerBuff = 2,      // Level 3, 8, 13...
    EnemyBuff = 3,       // Level 4, 9, 14...
    LevelRule = 4        // Level 5, 10, 15...
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
    public Sprite icon;
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

    [Tooltip("Base number of enemies spawned per selection every interval.")]
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

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when a lobby option selection is confirmed.")]
    public AudioClip selectionSound;

    [Range(0f, 1f)]
    public float selectionVolume = 1f;

    [Tooltip("Sound clip played when all items in a level are collected.")]
    public AudioClip levelCompleteSound;

    [Range(0f, 1f)]
    public float levelCompleteVolume = 1f;

    [Header("Background Music")]
    [Tooltip("Music played while resting or selecting options in the lobby.")]
    public AudioClip lobbyMusic;

    [Tooltip("Music played while active inside a level.")]
    public AudioClip inLevelMusic;

    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    [Header("UI & Objective Settings")]
    public TextMeshProUGUI itemCounterText;
    public string displayFormat = "Items: {0} / {1}";

    [Header("UI Counter Animation")]
    [Tooltip("Multiplier for text scale when animated (e.g. 1.3 = 30% larger).")]
    [SerializeField] private float counterPunchScale = 1.35f;

    [Tooltip("Duration in seconds for the pop animation.")]
    [SerializeField] private float counterAnimDuration = 0.2f;

    [Tooltip("Flash color when item is picked up.")]
    [SerializeField] private Color counterFlashColor = Color.yellow;

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
    [Tooltip("Time interval in seconds between continuous enemy spawn waves.")]
    public float enemySpawnInterval = 5f;

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
    private bool isProcessingSelection = false; // State guard against double triggers
    private int currentLobbyPhase = 0;          // 0 = 1st selection, 1 = 2nd selection

    private GameObject activeFinishPortal;
    private GameObject activeStartPortal;
    private Coroutine activeSpawnCoroutine;
    private AudioSource musicAudioSource;

    // UI Animation cached values
    private Vector3 defaultCounterScale = Vector3.one;
    private Color defaultCounterColor = Color.white;
    private Coroutine counterAnimationCoroutine;

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

        // Setup dedicated music audio source
        musicAudioSource = GetComponent<AudioSource>();
        if (musicAudioSource == null)
        {
            musicAudioSource = gameObject.AddComponent<AudioSource>();
        }
        musicAudioSource.loop = true;
        musicAudioSource.playOnAwake = false;

        // Cache original UI text properties for animation reset
        if (itemCounterText != null)
        {
            defaultCounterScale = itemCounterText.transform.localScale;
            defaultCounterColor = itemCounterText.color;
        }
    }

    private void Start()
    {
        OpenLobbyForCurrentLevel();
        UpdateUI();
    }

    // --- MUSIC CONTROLLER ---

    public void PlayMusic(AudioClip musicClip)
    {
        if (musicAudioSource == null) return;

        if (musicClip == null)
        {
            musicAudioSource.Stop();
            return;
        }

        if (musicAudioSource.clip == musicClip && musicAudioSource.isPlaying)
        {
            return;
        }

        musicAudioSource.clip = musicClip;
        musicAudioSource.volume = musicVolume;
        musicAudioSource.Play();
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
        AnimateItemCounter(); // Trigger punch animation on collect

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

    // --- UI ANIMATION ROUTINE ---

    private void AnimateItemCounter()
    {
        if (itemCounterText == null) return;

        if (counterAnimationCoroutine != null)
        {
            StopCoroutine(counterAnimationCoroutine);
        }

        counterAnimationCoroutine = StartCoroutine(PunchItemCounterRoutine());
    }

    private IEnumerator PunchItemCounterRoutine()
    {
        float halfDuration = counterAnimDuration * 0.5f;
        Vector3 targetScale = defaultCounterScale * counterPunchScale;
        float elapsed = 0f;

        // Phase 1: Scale up and transition to Flash Color
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;

            itemCounterText.transform.localScale = Vector3.Lerp(defaultCounterScale, targetScale, t);
            itemCounterText.color = Color.Lerp(defaultCounterColor, counterFlashColor, t);
            yield return null;
        }

        elapsed = 0f;

        // Phase 2: Scale back down and revert to Original Color
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;

            itemCounterText.transform.localScale = Vector3.Lerp(targetScale, defaultCounterScale, t);
            itemCounterText.color = Color.Lerp(counterFlashColor, defaultCounterColor, t);
            yield return null;
        }

        // Snap back to exact base parameters
        itemCounterText.transform.localScale = defaultCounterScale;
        itemCounterText.color = defaultCounterColor;
        counterAnimationCoroutine = null;
    }

    public void TriggerFinish()
    {
        if (isFinished) return;
        isFinished = true;

        PlayLevelCompleteSound();

        if (finishPortalPrefab != null && MapGenerator.Instance != null && activeFinishPortal == null)
        {
            Vector3 spawnPosition = MapGenerator.Instance.StartChunkWorldPosition + finishOffsetAboveStart;
            activeFinishPortal = Instantiate(finishPortalPrefab, spawnPosition, Quaternion.identity);
            Debug.Log("[GameManager] Level Complete! Finish Portal spawned.");
        }
    }

    private void PlayLevelCompleteSound()
    {
        if (levelCompleteSound != null)
        {
            AudioSource.PlayClipAtPoint(
                levelCompleteSound,
                Camera.main != null ? Camera.main.transform.position : transform.position,
                levelCompleteVolume
            );
        }
    }

    // --- LOBBY SELECTION ---

    public LobbySelectionType GetSelectionTypeForLevel(int level)
    {
        return (LobbySelectionType)((level - 1) % 5);
    }

    public void OpenLobbyForCurrentLevel()
    {
        // Hide item counter while in lobby selection
        if (itemCounterText != null)
        {
            itemCounterText.gameObject.SetActive(false);
        }

        currentLobbyPhase = 0; // Reset back to first choice
        isProcessingSelection = false;
        PlayMusic(lobbyMusic);
        StopSpawningEnemies();
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
        if (isProcessingSelection) return;
        isProcessingSelection = true;

        if (chosenOption != null)
        {
            // Level Rules CANNOT stack. Prevent duplicates.
            if (type == LobbySelectionType.LevelRule)
            {
                bool ruleAlreadyActive = ActiveModifiers.Exists(m =>
                    m is LevelRuleOption activeRule &&
                    (!string.IsNullOrEmpty(chosenOption.optionID) ? activeRule.optionID == chosenOption.optionID : activeRule.title == chosenOption.title));

                if (ruleAlreadyActive)
                {
                    Debug.LogWarning($"[GameManager] Level Rule '{chosenOption.title}' is already active and cannot stack.");
                    isProcessingSelection = false;
                    return;
                }
            }

            ActiveModifiers.Add(chosenOption);
            PlaySelectionSound();
            Debug.Log($"[GameManager] Selection ({currentLobbyPhase + 1}/2) Confirmed: '{chosenOption.title}' (Total Active Modifiers: {ActiveModifiers.Count})");
        }

        ClearLobbyObjects();

        // Check if player needs to make the 2nd selection
        if (currentLobbyPhase == 0)
        {
            currentLobbyPhase = 1;
            isProcessingSelection = false; // Reset lock for the next choice

            // Pick a random category excluding LevelRule
            LobbySelectionType randomSecondaryType = GetRandomNonRuleSelectionType();
            Spawn3DSelectionOptions(randomSecondaryType);
        }
        else
        {
            // Both selections are finished, spawn start portal
            Spawn3DStartPortal();
        }
    }

    private LobbySelectionType GetRandomNonRuleSelectionType()
    {
        List<LobbySelectionType> availableTypes = new List<LobbySelectionType>();

        // Only include non-rule categories that actually have options configured
        if (playerWeaponOptions != null && playerWeaponOptions.Count > 0)
            availableTypes.Add(LobbySelectionType.PlayerWeapon);

        if (enemySelectionOptions != null && enemySelectionOptions.Count > 0)
            availableTypes.Add(LobbySelectionType.EnemySelection);

        if (playerBuffOptions != null && playerBuffOptions.Count > 0)
            availableTypes.Add(LobbySelectionType.PlayerBuff);

        if (enemyBuffOptions != null && enemyBuffOptions.Count > 0)
            availableTypes.Add(LobbySelectionType.EnemyBuff);

        if (availableTypes.Count == 0)
        {
            // Safe fallback if pools are empty
            return LobbySelectionType.PlayerBuff;
        }

        return availableTypes[Random.Range(0, availableTypes.Count)];
    }

    private void PlaySelectionSound()
    {
        if (selectionSound != null)
        {
            AudioSource.PlayClipAtPoint(selectionSound, Camera.main != null ? Camera.main.transform.position : transform.position, selectionVolume);
        }
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

    // --- CONTINUOUS ENEMY SPAWN ROUTINE (EVERY 5 SECONDS) ---

    private IEnumerator ContinuousSpawnEnemiesRoutine(float interval)
    {
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            Vector3 currentSpawnOrigin = Vector3.zero;
            GameObject playerObj = GameObject.FindWithTag("Player");

            if (playerObj != null)
            {
                currentSpawnOrigin = playerObj.transform.position;
            }
            else if (MapGenerator.Instance != null)
            {
                currentSpawnOrigin = MapGenerator.Instance.StartChunkWorldPosition;
            }

            List<EnemySelectionOption> activeEnemyTypes = new List<EnemySelectionOption>();
            foreach (var modifier in ActiveModifiers)
            {
                if (modifier is EnemySelectionOption enemyOpt && enemyOpt.levelSpawnPrefab != null)
                {
                    activeEnemyTypes.Add(enemyOpt);
                }
            }

            if (activeEnemyTypes.Count > 0)
            {
                foreach (var enemyOpt in activeEnemyTypes)
                {
                    for (int i = 0; i < enemyOpt.enemyCount; i++)
                    {
                        SpawnSingleEnemy(enemyOpt.levelSpawnPrefab, currentSpawnOrigin);
                    }
                }

                int extraEnemyCount = Mathf.RoundToInt(GetTotalEnemyBuffValue(EnemyStatType.ExtraEnemyCount));
                for (int i = 0; i < extraEnemyCount; i++)
                {
                    int randomIndex = Random.Range(0, activeEnemyTypes.Count);
                    GameObject randomPrefab = activeEnemyTypes[randomIndex].levelSpawnPrefab;
                    SpawnSingleEnemy(randomPrefab, currentSpawnOrigin);
                }
            }

            yield return new WaitForSeconds(interval);
        }
    }

    private void StopSpawningEnemies()
    {
        if (activeSpawnCoroutine != null)
        {
            StopCoroutine(activeSpawnCoroutine);
            activeSpawnCoroutine = null;
        }
    }

    private void SpawnSingleEnemy(GameObject prefab, Vector3 spawnOrigin)
    {
        if (TryGetValidPlatformPosition(spawnOrigin, out Vector3 validSpawnPos))
        {
            GameObject spawnedEnemy = Instantiate(prefab, validSpawnPos, Quaternion.identity);
            RegisterEnemy(spawnedEnemy);
        }
        else
        {
            Vector2 safeOffset = Random.insideUnitCircle.normalized * minEnemySpawnDistance;
            Vector3 fallbackPos = spawnOrigin + new Vector3(safeOffset.x, 0.5f, safeOffset.y);

            GameObject spawnedEnemy = Instantiate(prefab, fallbackPos, Quaternion.identity);
            RegisterEnemy(spawnedEnemy);
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
                    Debug.DrawRay(rayStartPoint, Vector3.down * hit.distance, Color.green, 2f);
                    return true;
                }
            }

            Debug.DrawRay(rayStartPoint, Vector3.down * maxRaycastDistance, Color.red, 1f);
        }

        validPosition = Vector3.zero;
        return false;
    }

    public void StartSelectedLevel()
    {
        // Reveal item counter when entering actual level
        if (itemCounterText != null)
        {
            itemCounterText.gameObject.SetActive(true);
        }

        isProcessingSelection = false;
        PlayMusic(inLevelMusic);
        ClearLobbyObjects();

        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(false);
        }

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.GenerateMap();
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

        StopSpawningEnemies();
        activeSpawnCoroutine = StartCoroutine(ContinuousSpawnEnemiesRoutine(enemySpawnInterval));
    }

    public void OnLevelCompleted()
    {
        StopSpawningEnemies();

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

            if (currentLevel % 2 == 0)
            {
                MapGenerator.Instance.mapRadius++;
            }
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

    // --- ENEMY REGISTRATION & CLEANUP ---

    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy != null && !activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
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

    public List<LobbyOption> FetchOptionPoolForType(LobbySelectionType type)
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
                // Group player buffs by optionID (or title)
                var buffGroups = playerBuffOptions
                    .GroupBy(b => !string.IsNullOrEmpty(b.optionID) ? b.optionID : b.title);

                foreach (var group in buffGroups)
                {
                    string groupKey = group.Key;

                    // Sort ascending by buffValue (lowest value first)
                    var sortedBuffs = group.OrderBy(b => b.buffValue).ToList();

                    if (sortedBuffs.Count == 0) continue;

                    // Check how many times this buff has been chosen so far
                    int acquiredCount = ActiveModifiers.Count(m =>
                        m is PlayerBuffOption activeBuff &&
                        (!string.IsNullOrEmpty(activeBuff.optionID) ? activeBuff.optionID == groupKey : activeBuff.title == groupKey)
                    );

                    // Clamp index to highest available element so it repeats infinitely once maxed
                    int targetIndex = Mathf.Min(acquiredCount, sortedBuffs.Count - 1);
                    sourcePool.Add(sortedBuffs[targetIndex]);
                }
                break;

            case LobbySelectionType.EnemyBuff:
                sourcePool.AddRange(enemyBuffOptions);
                break;

            case LobbySelectionType.LevelRule:
                foreach (var rule in levelRuleOptions)
                {
                    bool alreadyChosen = ActiveModifiers.Exists(m => m is LevelRuleOption activeRule &&
                        (!string.IsNullOrEmpty(rule.optionID) ? activeRule.optionID == rule.optionID : activeRule.title == rule.title));

                    if (!alreadyChosen)
                    {
                        sourcePool.Add(rule);
                    }
                }
                break;
        }

        List<LobbyOption> selectedChoices = new List<LobbyOption>();
        List<LobbyOption> tempPool = new List<LobbyOption>(sourcePool);
        int choicesToPick = optionSpawnPoints.Length;

        // Pick unique options until pedestals are filled or pool runs out
        while (selectedChoices.Count < choicesToPick && tempPool.Count > 0)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            LobbyOption chosen = tempPool[randomIndex];
            selectedChoices.Add(chosen);

            string chosenKey = !string.IsNullOrEmpty(chosen.optionID) ? chosen.optionID : chosen.title;

            // Remove duplicates from tempPool for this selection wave
            tempPool.RemoveAll(opt =>
                (!string.IsNullOrEmpty(opt.optionID) ? opt.optionID : opt.title) == chosenKey
            );
        }

        return selectedChoices;
    }
}