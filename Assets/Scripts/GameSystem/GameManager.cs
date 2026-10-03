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

    [Tooltip("If true, only 1 instance will exist per level. Extra picks reduce action/spawn intervals instead of spawning duplicates.")]
    public bool isSingleInstance = false;
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

    [Header("3D World Space Lobby Text")]
    [Tooltip("World Space TextMeshPro object placed above the lobby selection area.")]
    public TextMeshPro lobbyPhaseWorldText;

    [Header("Juice & Screen FX")]
    [Tooltip("Intensity of camera shake during 100% full clear screen pulse.")]
    public float fullClearShakeMagnitude = 0.45f;

    [Tooltip("Duration of camera shake in seconds.")]
    public float fullClearShakeDuration = 0.35f;

    private Coroutine collapseFlashCoroutine;

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when a lobby option selection is confirmed.")]
    public AudioClip selectionSound;

    [Range(0f, 1f)]
    public float selectionVolume = 1f;

    [Tooltip("Sound clip played when all items in a level are collected.")]
    public AudioClip levelCompleteSound;

    [Range(0f, 1f)]
    public float levelCompleteVolume = 1f;

    [Tooltip("Sound clip played when full 100% map clear wipe triggers.")]
    public AudioClip fullClearSound;

    [Range(0f, 1f)]
    public float fullClearVolume = 1f;

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

    [Header("Exit & Collapse Mechanics")]
    [Tooltip("Percentage of items required to open the exit portal (e.g., 0.5 = 50%).")]
    [Range(0.1f, 1f)]
    public float exitUnlockPercentage = 0.5f;

    [Tooltip("Rate at which the Collapse Meter fills per second after exit opens (in %).")]
    public float collapseMeterBuildRate = 4f;

    [Tooltip("Percentage reduced from Collapse Meter per item collected after exit opens.")]
    public float itemCoolantAmount = 12f;

    [Tooltip("Permanent upgrade coins earned per second survived after exit opens.")]
    public float coinsPerSecondPostExit = 2f;

    [Tooltip("Multiplier applied to enemy speed when Collapse Meter reaches 100%.")]
    public float collapseSpeedMultiplier = 1.5f;

    [Tooltip("Multiplier applied to enemy health when Collapse Meter reaches 100%.")]
    public float collapseHealthMultiplier = 1.5f;

    [Header("Optional UI Elements for Collapse & Coins")]
    public TextMeshProUGUI collapseMeterText;
    public TextMeshProUGUI coinsText;

    [Header("Enemy Spawning & Performance Settings")]
    [Tooltip("Maximum active enemies allowed on screen simultaneously to prevent lag.")]
    public int maxActiveEnemies = 50;

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

    [Header("Lobby Environment & Spawn Setup")]
    public Transform lobbySpawnPoint;
    public GameObject lobbyEnvironmentRoot;
    public Transform[] optionSpawnPoints;
    public Transform startPortalSpawnPoint;
    public GameObject startPortalPrefab;

    [Header("Progression State")]
    public int currentLevel = 1;

    [Header("Option Pools")]
    public List<PlayerWeaponOption> playerWeaponOptions = new List<PlayerWeaponOption>();
    public List<EnemySelectionOption> enemySelectionOptions = new List<EnemySelectionOption>();
    public List<PlayerBuffOption> playerBuffOptions = new List<PlayerBuffOption>();
    public List<EnemyBuffOption> enemyBuffOptions = new List<EnemyBuffOption>();
    public List<LevelRuleOption> levelRuleOptions = new List<LevelRuleOption>();

    // Public Properties
    public int TotalItems { get; private set; } = 0;
    public int CollectedItems { get; private set; } = 0;
    public float CollapseMeter { get; private set; } = 0f;
    public bool ExitUnlocked { get; private set; } = false;
    public int CollapseLevel { get; private set; } = 0;
    public float PermanentCoinsEarned { get; private set; } = 0f;
    public float PostExitSurvivalTime { get; private set; } = 0f;

    private bool isFinished = false;
    private bool hasTriggeredFullClear = false;
    private bool isProcessingSelection = false; // State guard against double triggers
    private int currentLobbyPhase = 0;          // 0 = 1st selection, 1 = 2nd selection, 2 = 3rd selection

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

    private void Update()
    {
        // Continuously evaluate BGM pitch/speed based on collapse state
        UpdateCollapseBGM();

        // Collapse Meter & Survival Coin Loop
        if (ExitUnlocked && !isFinished)
        {
            // Advance collapse meter
            CollapseMeter += collapseMeterBuildRate * Time.deltaTime;

            // Accumulate survival time and permanent coins
            PostExitSurvivalTime += Time.deltaTime;
            PermanentCoinsEarned += coinsPerSecondPostExit * Time.deltaTime;

            // Check for 100% Collapse Overload (Repeatable Loop)
            if (CollapseMeter >= 100f)
            {
                CollapseMeter -= 100f; // Reset meter back to 0% (retaining overflow)
                CollapseLevel++;
                OnCollapseTriggered();
            }

            UpdateUI();
        }
    }

    private void OnCollapseTriggered()
    {
        Debug.LogWarning($"[GameManager] Collapse level reached LVL {CollapseLevel}! Refreshing enemy stats on field.");

        // Trigger visual juice
        FlashCollapseText();
        TriggerCameraShake(0.2f, 0.2f); // Quick warning rumble

        // Instantly apply the new stat scaling to all currently active enemies on screen
        foreach (GameObject enemyObj in activeEnemies)
        {
            if (enemyObj != null && enemyObj.TryGetComponent<EnemyBase>(out var enemyScript))
            {
                enemyScript.ApplyEnemyBuffs();
            }
        }
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

        // Apply compounding collapse stack multipliers
        if (CollapseLevel > 0)
        {
            if (stat == EnemyStatType.Health)
            {
                total *= Mathf.Pow(collapseHealthMultiplier, CollapseLevel);
            }
            else if (stat == EnemyStatType.MoveSpeed)
            {
                total *= Mathf.Pow(collapseSpeedMultiplier, CollapseLevel);
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
        CollapseMeter = 0f;
        CollapseLevel = 0; // Reset collapse stack level
        PostExitSurvivalTime = 0f;
        ExitUnlocked = false;
        isFinished = false;
        hasTriggeredFullClear = false;

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
        AnimateItemCounter(); // Trigger punch animation on collect

        // Check Exit Unlock Threshold (default 50%)
        int requiredItemsForExit = Mathf.Max(1, Mathf.CeilToInt(TotalItems * exitUnlockPercentage));
        if (!ExitUnlocked && CollectedItems >= requiredItemsForExit)
        {
            ExitUnlocked = true;
            TriggerFinishPortal();
        }

        // Post-Exit Coolant Mechanism
        if (ExitUnlocked)
        {
            CollapseMeter = Mathf.Clamp(CollapseMeter - itemCoolantAmount, 0f, 100f);
        }

        // 100% Full Map Clear Climax
        if (TotalItems > 0 && CollectedItems >= TotalItems && !hasTriggeredFullClear)
        {
            Trigger100PercentFullClearPulse();
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (itemCounterText != null)
        {
            itemCounterText.text = string.Format(displayFormat, CollectedItems, TotalItems);
        }

        if (collapseMeterText != null)
        {
            collapseMeterText.gameObject.SetActive(ExitUnlocked);
            string levelText = CollapseLevel > 0 ? $" (LVL {CollapseLevel})" : "";
            collapseMeterText.text = $"Collapse: {Mathf.RoundToInt(CollapseMeter)}%{levelText}";
            collapseMeterText.color = CollapseLevel > 0 ? Color.red : (CollapseMeter > 70f ? Color.yellow : Color.white);
        }

        if (coinsText != null)
        {
            coinsText.gameObject.SetActive(ExitUnlocked);
            coinsText.text = $"Coins: +{Mathf.FloorToInt(PermanentCoinsEarned)}";
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

    private void TriggerFinishPortal()
    {
        PlayLevelCompleteSound();

        if (finishPortalPrefab != null && MapGenerator.Instance != null && activeFinishPortal == null)
        {
            Vector3 spawnPosition = MapGenerator.Instance.StartChunkWorldPosition + finishOffsetAboveStart;
            activeFinishPortal = Instantiate(finishPortalPrefab, spawnPosition, Quaternion.identity);
            Debug.Log("[GameManager] Exit Unlocked! Finish Portal spawned.");
        }
    }

    private void Trigger100PercentFullClearPulse()
    {
        hasTriggeredFullClear = true;
        Debug.Log("[GameManager] 100% Full Clear achieved! Clearing all active enemies.");

        // Trigger full clear camera shake
        TriggerCameraShake(fullClearShakeDuration, fullClearShakeMagnitude);

        // Play full clear pulse sound
        if (fullClearSound != null)
        {
            AudioSource.PlayClipAtPoint(
                fullClearSound,
                Camera.main != null ? Camera.main.transform.position : transform.position,
                fullClearVolume
            );
        }

        // Destroy/wipe all current enemies on screen as a reward
        ClearEnemies();
    }

    public void TriggerFinish()
    {
        if (isFinished) return;
        isFinished = true;
        OnLevelCompleted();
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
        // Hide item counter and collapse elements in lobby
        if (itemCounterText != null) itemCounterText.gameObject.SetActive(false);
        if (collapseMeterText != null) collapseMeterText.gameObject.SetActive(false);
        if (coinsText != null) coinsText.gameObject.SetActive(false);

        currentLobbyPhase = 0; // Reset back to first choice
        isProcessingSelection = false;
        ExitUnlocked = false;
        CollapseLevel = 0; // Reset collapse level stack

        PlayMusic(lobbyMusic);
        StopSpawningEnemies();
        ClearLobbyObjects();

        LobbySelectionType currentType = GetSelectionTypeForLevel(currentLevel);
        Spawn3DSelectionOptions(currentType);
    }

    private void Spawn3DSelectionOptions(LobbySelectionType type)
    {
        // Update 3D World Space Text indicator
        UpdateLobbyPhaseWorldText(type);

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

    private void UpdateLobbyPhaseWorldText(LobbySelectionType type)
    {
        if (lobbyPhaseWorldText == null) return;

        lobbyPhaseWorldText.gameObject.SetActive(true);
        string categoryName = GetCategoryDisplayName(type);

        // Header: Round index | Subtitle: Category name
        lobbyPhaseWorldText.text = $"<b>CHOICE {currentLobbyPhase + 1} / 3</b>\n<size=70%><color=#FFCC00>{categoryName}</color></size>";
    }

    private string GetCategoryDisplayName(LobbySelectionType type)
    {
        switch (type)
        {
            case LobbySelectionType.PlayerWeapon: return "PLAYER WEAPON";
            case LobbySelectionType.EnemySelection: return "ENEMY TYPE";
            case LobbySelectionType.PlayerBuff: return "PLAYER BUFF";
            case LobbySelectionType.EnemyBuff: return "ENEMY BUFF";
            case LobbySelectionType.LevelRule: return "LEVEL RULE MODIFIER";
            default: return "UNKNOWN CATEGORY";
        }
    }

    private void Spawn3DStartPortal()
    {
        if (lobbyPhaseWorldText != null)
        {
            lobbyPhaseWorldText.gameObject.SetActive(true);
            lobbyPhaseWorldText.text = "<b>ALL CHOICES COMPLETE!</b>\n<size=70%><color=#00FF88>ENTER START PORTAL</color></size>";
        }

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
            Debug.Log($"[GameManager] Selection ({currentLobbyPhase + 1}/3) Confirmed: '{chosenOption.title}' (Total Active Modifiers: {ActiveModifiers.Count})");
        }

        ClearLobbyObjects();

        // Check if player needs to make further selections (3 rounds total: 0, 1, 2)
        if (currentLobbyPhase < 2)
        {
            currentLobbyPhase++;
            isProcessingSelection = false; // Reset lock for the next choice

            // Pick a random category excluding LevelRule for rounds 2 and 3
            LobbySelectionType randomSecondaryType = GetRandomNonRuleSelectionType();
            Spawn3DSelectionOptions(randomSecondaryType);
        }
        else
        {
            // All 3 selections are finished, spawn start portal
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

    // --- CONTINUOUS ENEMY SPAWN ROUTINE ---
    private IEnumerator ContinuousSpawnEnemiesRoutine(float interval)
    {
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            activeEnemies.RemoveAll(e => e == null);

            if (activeEnemies.Count < maxActiveEnemies)
            {
                Vector3 currentSpawnOrigin = Vector3.zero;
                GameObject playerObj = GameObject.FindWithTag("Player");

                if (playerObj != null)
                    currentSpawnOrigin = playerObj.transform.position;
                else if (MapGenerator.Instance != null)
                    currentSpawnOrigin = MapGenerator.Instance.StartChunkWorldPosition;

                // Group active enemy options by prefab
                var enemyGroups = ActiveModifiers
                    .OfType<EnemySelectionOption>()
                    .Where(e => e.levelSpawnPrefab != null)
                    .GroupBy(e => !string.IsNullOrEmpty(e.optionID) ? e.optionID : e.levelSpawnPrefab.name);

                foreach (var group in enemyGroups)
                {
                    if (activeEnemies.Count >= maxActiveEnemies) break;

                    EnemySelectionOption sampleOption = group.First();
                    int stackCount = group.Count();

                    if (sampleOption.isSingleInstance)
                    {
                        // Check if screen hazard/single-instance enemy is already active in scene
                        GameObject existingEnemy = activeEnemies.Find(e =>
                            e != null && e.name.StartsWith(sampleOption.levelSpawnPrefab.name));

                        if (existingEnemy == null)
                        {
                            // Spawn directly without 3D ground raycasting
                            GameObject spawned = Instantiate(sampleOption.levelSpawnPrefab, Vector3.zero, Quaternion.identity);
                            RegisterEnemy(spawned);

                            if (spawned.TryGetComponent<EnemyBase>(out var enemyScript))
                            {
                                enemyScript.SetStackCount(stackCount);
                            }
                        }
                        else if (existingEnemy.TryGetComponent<EnemyBase>(out var enemyScript))
                        {
                            // Update existing instance stack count if changed mid-level
                            enemyScript.SetStackCount(stackCount);
                        }
                    }
                    else
                    {
                        // Standard physical 3D enemies
                        int totalToSpawn = sampleOption.enemyCount * stackCount;
                        for (int i = 0; i < totalToSpawn; i++)
                        {
                            if (activeEnemies.Count >= maxActiveEnemies) break;
                            SpawnSingleEnemy(sampleOption.levelSpawnPrefab, currentSpawnOrigin);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(interval);
        }
    }

    // Update SpawnSingleEnemy return type to return the spawned enemy GameObject
    private GameObject SpawnSingleEnemy(GameObject prefab, Vector3 spawnOrigin)
    {
        Vector3 spawnPos;

        if (!TryGetValidPlatformPosition(spawnOrigin, out spawnPos))
        {
            Vector2 safeOffset = Random.insideUnitCircle.normalized * minEnemySpawnDistance;
            spawnPos = spawnOrigin + new Vector3(safeOffset.x, 0.5f, safeOffset.y);
        }

        GameObject spawnedEnemy = (SimpleEnemyPool.Instance != null)
            ? SimpleEnemyPool.Instance.GetEnemy(prefab, spawnPos, Quaternion.identity)
            : Instantiate(prefab, spawnPos, Quaternion.identity);

        RegisterEnemy(spawnedEnemy);
        return spawnedEnemy;
    }

    private void StopSpawningEnemies()
    {
        if (activeSpawnCoroutine != null)
        {
            StopCoroutine(activeSpawnCoroutine);
            activeSpawnCoroutine = null;
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
        if (lobbyPhaseWorldText != null)
        {
            lobbyPhaseWorldText.gameObject.SetActive(false);
        }

        ResetItemCount();

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

            if (currentLevel % 3 == 0)
            {
                MapGenerator.Instance.mapRadius++;
            }
        }

        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(true);
        }

        int totalCoins = PlayerPrefs.GetInt("TotalCoins", 0) + Mathf.FloorToInt(PermanentCoinsEarned);
        PlayerPrefs.SetInt("TotalCoins", totalCoins);
        PlayerPrefs.Save();

        SavePermanentCoins();

        TeleportPlayerToLobby();
        OpenLobbyForCurrentLevel();
    }

    // Call when player dies
    public void OnPlayerDied()
    {
        if (RunSummaryUI.Instance != null)
        {
            RunSummaryUI.Instance.ShowDeathSummary();
        }
        else
        {
            // Fallback reload if UI manager is not present
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Call when player enters the exit portal
    public void OnStageExitReached()
    {
        // Proceed directly to next level/lobby transition without opening summary UI
        OnLevelCompleted();
    }

    // --- ENEMY REGISTRATION & CLEANUP ---

    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy != null && !activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
    }

    public void UnregisterEnemy(GameObject enemy)
    {
        if (enemy != null && activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
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

    public void SavePermanentCoins()
    {
        int coinsToSave = Mathf.FloorToInt(PermanentCoinsEarned);
        if (coinsToSave <= 0) return;

        int totalSaved = PlayerPrefs.GetInt("TotalPermanentCoins", 0);
        PlayerPrefs.SetInt("TotalPermanentCoins", totalSaved + coinsToSave);
        PlayerPrefs.Save();

        Debug.Log($"[GameManager] Saved {coinsToSave} coins! Total lifetime coins: {totalSaved + coinsToSave}");
        PermanentCoinsEarned = 0f; // Reset buffer
    }

    // --- CAMERA SHAKE ROUTINE ---
    public void TriggerCameraShake(float duration, float magnitude)
    {
        if (Camera.main != null)
        {
            StartCoroutine(CameraShakeRoutine(duration, magnitude));
        }
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        Transform camTransform = Camera.main.transform;
        Vector3 originalPos = camTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            camTransform.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        camTransform.localPosition = originalPos;
    }

    // --- COLLAPSE UI POP ANIMATION ---
    private void FlashCollapseText()
    {
        if (collapseMeterText == null) return;
        if (collapseFlashCoroutine != null) StopCoroutine(collapseFlashCoroutine);
        collapseFlashCoroutine = StartCoroutine(FlashCollapseTextRoutine());
    }

    private IEnumerator FlashCollapseTextRoutine()
    {
        Vector3 baseScale = Vector3.one;
        Vector3 targetScale = baseScale * 1.4f;
        float duration = 0.3f;
        float elapsed = 0f;

        collapseMeterText.color = Color.yellow;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            collapseMeterText.transform.localScale = Vector3.Lerp(targetScale, baseScale, t);
            yield return null;
        }

        collapseMeterText.transform.localScale = baseScale;
    }

    private void UpdateCollapseBGM()
    {
        if (AudioManager.Instance == null) return;

        if (ExitUnlocked)
        {
            // Smoothly scale BGM pitch from 1.0x up to 1.3x based on CollapseLevel
            float bgmPitch = 1f + (CollapseLevel * 0.05f);
            AudioManager.Instance.SetBGMPitch(Mathf.Min(bgmPitch, 1.3f));
        }
        else
        {
            // Reset BGM speed/pitch to normal when exit is locked or in lobby
            AudioManager.Instance.SetBGMPitch(1f);
        }
    }
}