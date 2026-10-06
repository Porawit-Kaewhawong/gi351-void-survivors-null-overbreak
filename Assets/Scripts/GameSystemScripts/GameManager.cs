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
    ExtraJumps,
    ShieldCharge
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

    // --- C# EVENTS FOR DECOUPLED ARCHITECTURE ---
    public static event System.Action<int, int> OnItemCountChanged;        // (collected, total)
    public static event System.Action<float, int> OnCollapseMeterChanged;  // (meterPercent, collapseLevel)
    public static event System.Action<float> OnCoinsEarnedChanged;         // (currentEarned)
    public static event System.Action OnExitUnlockedEvent;
    public static event System.Action OnFullClearTriggeredEvent;
    public static event System.Action<int> OnCollapseLevelUpEvent;         // (newLevel)

    private const string PERMANENT_COINS_KEY = "TotalPermanentCoins";

    [Header("3D World Space Lobby Text")]
    [Tooltip("World Space TextMeshPro object placed above the lobby selection area.")]
    public TextMeshPro lobbyPhaseWorldText;

    [Header("Juice & Screen FX")]
    [Tooltip("Intensity of camera shake during 100% full clear screen pulse.")]
    public float fullClearShakeMagnitude = 0.45f;

    [Tooltip("Duration of camera shake in seconds.")]
    public float fullClearShakeDuration = 0.35f;

    private Coroutine collapseFlashCoroutine;

    [Header("Dynamic Atmosphere Settings")]
    [SerializeField] private bool enableDynamicCollapseFog = true;
    [SerializeField] private Color normalFogColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    [SerializeField] private Color collapsePanicFogColor = new Color(0.6f, 0.05f, 0.05f, 1f);
    [SerializeField] private float baseFogDensity = 0.01f;
    [SerializeField] private float maxCollapseFogDensity = 0.045f;

    [Header("Enemy Spawn & Juicing VFX")]
    [Tooltip("Prefab spawned briefly at target spawn location before enemy appears.")]
    public GameObject spawnTelegraphPrefab;
    public float telegraphDuration = 0.6f;

    [Tooltip("Shockwave wave prefab spawned on player when 100% full clear triggers.")]
    public GameObject fullClearShockwavePrefab;

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

    [Tooltip("Dedicated SFX audio source for standard sound effects.")]
    public AudioSource sfxAudioSource;

    [Header("Glitch Audio Settings")]
    [Tooltip("Audio clip(s) for the glitch/static stutters during skybox swaps.")]
    [SerializeField] private AudioClip[] glitchSFXClips;

    [Tooltip("Volume multiplier for glitch sound effects.")]
    [Range(0f, 1f)]
    [SerializeField] private float glitchSFXVolume = 0.8f;

    [Tooltip("Randomize pitch slightly on each flicker for a chaotic feel.")]
    [SerializeField] private bool randomizeGlitchPitch = true;

    [Header("Background Music")]
    [Tooltip("Music played while resting or selecting options in the lobby.")]
    public AudioClip lobbyMusic;

    [Tooltip("Music played while active inside a level.")]
    public AudioClip inLevelMusic;

    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    [Header("Post-Exit Atmosphere & Music")]
    [Tooltip("Default Skybox material for lobby and active gameplay.")]
    public Material defaultSkybox;

    [Tooltip("Skybox material applied once the exit portal opens.")]
    public Material postExitSkybox;

    [Tooltip("Array of randomized music tracks to choose from once the exit portal opens.")]
    public AudioClip[] postExitMusicList;

    [Header("Skybox Transition Settings")]
    [Tooltip("Number of back-and-forth flickers before locking onto postExitSkybox.")]
    [SerializeField] private int skyboxFlickerCount = 6;

    [Tooltip("Interval in seconds between skybox swaps during flickering.")]
    [SerializeField] private float skyboxFlickerInterval = 0.08f;

    private Coroutine skyboxFlickerCoroutine;

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

    [Tooltip("Stagger delay in seconds between individual enemy spawns to prevent frame spikes.")]
    public float enemySpawnStaggerDelay = 0.05f;

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
    private bool isProcessingSelection = false;
    private int currentLobbyPhase = 0;

    private GameObject activeFinishPortal;
    private GameObject activeStartPortal;
    private Coroutine activeSpawnCoroutine;
    private AudioSource musicAudioSource;

    // UI Animation cached values
    private Vector3 defaultCounterScale = Vector3.one;
    private Color defaultCounterColor = Color.white;
    private Coroutine counterAnimationCoroutine;

    // Cache Player & Weapon references to prevent GC garbage spikes
    private Transform cachedPlayerTransform;
    private PlayerController cachedPlayerController;
    private readonly List<GameObject> activeWeapons = new List<GameObject>();

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

        // Dynamically shift atmosphere and fog during panic mode
        UpdateAtmosphere();

        // Collapse Meter & Survival Coin Loop
        if (ExitUnlocked && !isFinished)
        {
            // Advance collapse meter
            CollapseMeter += collapseMeterBuildRate * Time.deltaTime;

            // Accumulate survival time and permanent coins
            PostExitSurvivalTime += Time.deltaTime;
            PermanentCoinsEarned += coinsPerSecondPostExit * Time.deltaTime;

            // Trigger events for decoupled observers
            OnCollapseMeterChanged?.Invoke(CollapseMeter, CollapseLevel);
            OnCoinsEarnedChanged?.Invoke(PermanentCoinsEarned);

            // Check for 100% Collapse Overload (Repeatable Loop)
            if (CollapseMeter >= 100f)
            {
                CollapseMeter -= 100f; // Reset meter back to 0% (retaining overflow)
                CollapseLevel++;
                OnCollapseLevelUpEvent?.Invoke(CollapseLevel);
                OnCollapseTriggered();
            }

            UpdateUI();
        }
    }

    public Transform GetPlayerTransform()
    {
        if (cachedPlayerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                cachedPlayerTransform = playerObj.transform;
                cachedPlayerController = playerObj.GetComponent<PlayerController>();
            }
        }
        return cachedPlayerTransform;
    }

    private void UpdateAtmosphere()
    {
        if (!enableDynamicCollapseFog || !RenderSettings.fog) return;

        if (ExitUnlocked)
        {
            float collapseNormalized = Mathf.Clamp01(CollapseMeter / 100f);
            RenderSettings.fogDensity = Mathf.Lerp(baseFogDensity, maxCollapseFogDensity, collapseNormalized);
            RenderSettings.fogColor = Color.Lerp(normalFogColor, collapsePanicFogColor, collapseNormalized);
        }
        else
        {
            RenderSettings.fogDensity = baseFogDensity;
            RenderSettings.fogColor = normalFogColor;
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
        CollapseLevel = 0;
        PostExitSurvivalTime = 0f;
        ExitUnlocked = false;
        isFinished = false;
        hasTriggeredFullClear = false;

        if (activeFinishPortal != null)
        {
            Destroy(activeFinishPortal);
            activeFinishPortal = null;
        }

        OnItemCountChanged?.Invoke(CollectedItems, TotalItems);
        UpdateUI();
    }

    public void RegisterItem()
    {
        TotalItems++;
        OnItemCountChanged?.Invoke(CollectedItems, TotalItems);
        UpdateUI();
    }

    public void UnregisterItem()
    {
        TotalItems = Mathf.Max(0, TotalItems - 1);
        OnItemCountChanged?.Invoke(CollectedItems, TotalItems);
        UpdateUI();
    }

    public void OnItemCollected()
    {
        if (isFinished) return;

        CollectedItems++;
        AnimateItemCounter();
        OnItemCountChanged?.Invoke(CollectedItems, TotalItems);

        // Check Exit Unlock Threshold (default 50%)
        int requiredItemsForExit = Mathf.Max(1, Mathf.CeilToInt(TotalItems * exitUnlockPercentage));
        if (!ExitUnlocked && CollectedItems >= requiredItemsForExit)
        {
            ExitUnlocked = true;
            OnExitUnlockedEvent?.Invoke();
            TriggerFinishPortal();
        }

        // Post-Exit Coolant Mechanism
        if (ExitUnlocked)
        {
            CollapseMeter = Mathf.Clamp(CollapseMeter - itemCoolantAmount, 0f, 100f);
            OnCollapseMeterChanged?.Invoke(CollapseMeter, CollapseLevel);
        }

        // 100% Full Map Clear Climax
        if (TotalItems > 0 && CollectedItems >= TotalItems && !hasTriggeredFullClear)
        {
            OnFullClearTriggeredEvent?.Invoke();
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

        // Start flickering transition for skybox and BGM
        if (skyboxFlickerCoroutine != null)
        {
            StopCoroutine(skyboxFlickerCoroutine);
        }
        skyboxFlickerCoroutine = StartCoroutine(FlickerSkyboxAndMusicRoutine());

        // Spawn Finish Portal
        if (finishPortalPrefab != null && MapGenerator.Instance != null && activeFinishPortal == null)
        {
            Vector3 spawnPosition = MapGenerator.Instance.StartChunkWorldPosition + finishOffsetAboveStart;
            activeFinishPortal = Instantiate(finishPortalPrefab, spawnPosition, Quaternion.identity);
            Debug.Log("[GameManager] Exit Unlocked! Finish Portal spawned.");
        }
    }

    private IEnumerator FlickerSkyboxAndMusicRoutine()
    {
        // 1. Alternate skyboxes rapidly if both materials are set
        if (defaultSkybox != null && postExitSkybox != null)
        {
            for (int i = 0; i < skyboxFlickerCount; i++)
            {
                // Alternate skybox material (Even = post-exit; Odd = default)
                RenderSettings.skybox = (i % 2 == 0) ? postExitSkybox : defaultSkybox;
                DynamicGI.UpdateEnvironment();

                // Play audio stutter on each flicker
                PlayGlitchSFX();

                yield return new WaitForSeconds(skyboxFlickerInterval);
            }

            // Lock in postExitSkybox permanently
            RenderSettings.skybox = postExitSkybox;
            DynamicGI.UpdateEnvironment();
        }
        else if (postExitSkybox != null)
        {
            RenderSettings.skybox = postExitSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // 2. Switch to post-exit BGM after flickering settles
        if (postExitMusicList != null && postExitMusicList.Length > 0)
        {
            AudioClip selectedBGM = postExitMusicList[Random.Range(0, postExitMusicList.Length)];
            PlayMusic(selectedBGM);
        }

        skyboxFlickerCoroutine = null;
    }

    private void PlayGlitchSFX()
    {
        if (glitchSFXClips == null || glitchSFXClips.Length == 0) return;

        AudioClip clip = glitchSFXClips[Random.Range(0, glitchSFXClips.Length)];
        if (clip == null) return;

        if (sfxAudioSource != null)
        {
            if (randomizeGlitchPitch)
            {
                sfxAudioSource.pitch = Random.Range(0.85f, 1.25f);
            }
            sfxAudioSource.PlayOneShot(clip, glitchSFXVolume);
        }
        else if (Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, glitchSFXVolume);
        }
    }

    private void Trigger100PercentFullClearPulse()
    {
        hasTriggeredFullClear = true;
        Debug.Log("[GameManager] 100% Full Clear achieved! Clearing all active enemies.");

        // Trigger full clear camera shake
        TriggerCameraShake(fullClearShakeDuration, fullClearShakeMagnitude);

        // Spawn expanding shockwave FX on player position
        Transform playerT = GetPlayerTransform();
        if (fullClearShockwavePrefab != null && playerT != null)
        {
            Instantiate(fullClearShockwavePrefab, playerT.position, Quaternion.identity);
        }

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
        // Reset skybox back to default
        if (defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
        }

        // Hide item counter and collapse elements in lobby
        if (itemCounterText != null) itemCounterText.gameObject.SetActive(false);
        if (collapseMeterText != null) collapseMeterText.gameObject.SetActive(false);
        if (coinsText != null) coinsText.gameObject.SetActive(false);

        currentLobbyPhase = 0;
        isProcessingSelection = false;
        ExitUnlocked = false;
        CollapseLevel = 0;

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
            isProcessingSelection = false;

            LobbySelectionType randomSecondaryType = GetRandomNonRuleSelectionType();
            Spawn3DSelectionOptions(randomSecondaryType);
        }
        else
        {
            Spawn3DStartPortal();
        }
    }

    private LobbySelectionType GetRandomNonRuleSelectionType()
    {
        List<LobbySelectionType> availableTypes = new List<LobbySelectionType>();

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

        // Explicitly destroy tracked weapons from list instead of relying purely on frame-end child destroys
        foreach (var weapon in activeWeapons)
        {
            if (weapon != null) Destroy(weapon);
        }
        activeWeapons.Clear();

        // Group player weapon selections by optionID or prefab name
        var weaponGroups = ActiveModifiers
            .OfType<PlayerWeaponOption>()
            .Where(w => w.weaponPrefab != null)
            .GroupBy(w => !string.IsNullOrEmpty(w.optionID) ? w.optionID : w.weaponPrefab.name);

        foreach (var group in weaponGroups)
        {
            PlayerWeaponOption sampleWeaponOption = group.First();
            int weaponLevel = group.Count(); // Level = total times this weapon was picked

            // Instantiate ONLY 1 instance per weapon type
            GameObject weaponObj = Instantiate(sampleWeaponOption.weaponPrefab, weaponContainer);
            activeWeapons.Add(weaponObj);

            // Apply weapon level & upgraded stats
            if (weaponObj.TryGetComponent<BaseWeapon>(out var weaponScript))
            {
                weaponScript.SetWeaponLevel(weaponLevel);
            }
        }
    }

    // --- CONTINUOUS ENEMY SPAWN ROUTINE ---

    private IEnumerator ContinuousSpawnEnemiesRoutine(float interval)
    {
        yield return new WaitForSeconds(1.5f);

        while (true)
        {
            float spawnTimeTaken = 0f;
            activeEnemies.RemoveAll(e => e == null);

            Vector3 currentSpawnOrigin = Vector3.zero;
            Transform playerT = GetPlayerTransform();

            if (playerT != null)
                currentSpawnOrigin = playerT.position;
            else if (MapGenerator.Instance != null)
                currentSpawnOrigin = MapGenerator.Instance.StartChunkWorldPosition;

            int extraEnemyBuff = Mathf.RoundToInt(GetTotalEnemyBuffValue(EnemyStatType.ExtraEnemyCount));

            var enemyGroups = ActiveModifiers
                .OfType<EnemySelectionOption>()
                .Where(e => e.levelSpawnPrefab != null)
                .GroupBy(e => !string.IsNullOrEmpty(e.optionID) ? e.optionID : e.levelSpawnPrefab.name)
                .ToList();

            // PASS 1: SINGLE INSTANCE
            foreach (var group in enemyGroups)
            {
                EnemySelectionOption sampleOption = group.First();
                if (sampleOption.isSingleInstance)
                {
                    int stackCount = group.Count();
                    GameObject existingEnemy = activeEnemies.Find(e =>
                        e != null && e.name.StartsWith(sampleOption.levelSpawnPrefab.name));

                    if (existingEnemy == null && activeEnemies.Count < maxActiveEnemies)
                    {
                        SpawnSingleEnemy(sampleOption.levelSpawnPrefab, currentSpawnOrigin, stackCount);
                        yield return new WaitForSeconds(enemySpawnStaggerDelay);
                        spawnTimeTaken += enemySpawnStaggerDelay;
                    }
                    else if (existingEnemy != null && existingEnemy.TryGetComponent<EnemyBase>(out var enemyScript))
                    {
                        enemyScript.SetStackCount(stackCount);
                    }
                }
            }

            // PASS 2: MULTI-INSTANCE SPAWNING
            var multiInstanceGroups = enemyGroups
                .Where(g => !g.First().isSingleInstance)
                .Select(g => new
                {
                    Prefab = g.First().levelSpawnPrefab,
                    TotalToSpawn = (g.First().enemyCount + extraEnemyBuff) * g.Count()
                })
                .ToList();

            if (multiInstanceGroups.Count > 0)
            {
                int maxSpawnsInAnyGroup = multiInstanceGroups.Max(g => g.TotalToSpawn);

                for (int step = 0; step < maxSpawnsInAnyGroup; step++)
                {
                    if (activeEnemies.Count >= maxActiveEnemies) break;

                    foreach (var groupInfo in multiInstanceGroups)
                    {
                        if (activeEnemies.Count >= maxActiveEnemies) break;

                        if (step < groupInfo.TotalToSpawn)
                        {
                            SpawnSingleEnemy(groupInfo.Prefab, currentSpawnOrigin, 1);
                            yield return new WaitForSeconds(enemySpawnStaggerDelay);
                            spawnTimeTaken += enemySpawnStaggerDelay;
                        }
                    }
                }
            }

            // Subtract the time spent staggering from the wave interval
            float remainingWait = Mathf.Max(0.5f, interval - spawnTimeTaken);
            yield return new WaitForSeconds(remainingWait);
        }
    }

    private void SpawnSingleEnemy(GameObject prefab, Vector3 spawnOrigin, int stackCount)
    {
        Vector3 spawnPos;

        if (!TryGetValidPlatformPosition(spawnOrigin, out spawnPos))
        {
            Vector2 safeOffset = Random.insideUnitCircle.normalized * minEnemySpawnDistance;
            spawnPos = spawnOrigin + new Vector3(safeOffset.x, 0.5f, safeOffset.y);
        }

        StartCoroutine(SpawnEnemyWithTelegraphRoutine(prefab, spawnPos, stackCount));
    }

    private IEnumerator SpawnEnemyWithTelegraphRoutine(GameObject enemyPrefab, Vector3 spawnPos, int stackCount)
    {
        // 1. Spawn Telegraph Indicator
        GameObject indicator = null;
        if (spawnTelegraphPrefab != null)
        {
            indicator = Instantiate(spawnTelegraphPrefab, spawnPos, Quaternion.identity);
        }

        if (telegraphDuration > 0f)
        {
            yield return new WaitForSeconds(telegraphDuration);
        }

        if (indicator != null) Destroy(indicator);

        // 2. Instantiate or Pool Enemy
        GameObject spawnedEnemy = (SimpleEnemyPool.Instance != null)
            ? SimpleEnemyPool.Instance.GetEnemy(enemyPrefab, spawnPos, Quaternion.identity)
            : Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        if (spawnedEnemy != null)
        {
            RegisterEnemy(spawnedEnemy);
            if (spawnedEnemy.TryGetComponent<EnemyBase>(out var enemyScript))
            {
                enemyScript.SetStackCount(stackCount);
            }
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
        if (defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
        }

        if (lobbyPhaseWorldText != null)
        {
            lobbyPhaseWorldText.gameObject.SetActive(false);
        }

        ResetItemCount();

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

        Transform playerTransform = GetPlayerTransform();
        if (playerTransform != null && playerTransform.TryGetComponent<PlayerController>(out var player))
        {
            player.ResetHealthAndShield();
            EquipPlayerWeapons(playerTransform);
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

        SavePermanentCoins();

        TeleportPlayerToLobby();
        OpenLobbyForCurrentLevel();
    }

    public void OnPlayerDied()
    {
        if (RunSummaryUI.Instance != null)
        {
            RunSummaryUI.Instance.ShowDeathSummary();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void OnStageExitReached()
    {
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

        Transform playerTransform = GetPlayerTransform();

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
                AddTieredBuffsToPool(playerBuffOptions, sourcePool);
                break;

            case LobbySelectionType.EnemyBuff:
                AddTieredBuffsToPool(enemyBuffOptions, sourcePool);
                break;

            case LobbySelectionType.LevelRule:
                foreach (var rule in levelRuleOptions)
                {
                    bool alreadyChosen = ActiveModifiers.Exists(m => m is LevelRuleOption activeRule &&
                        (!string.IsNullOrEmpty(rule.optionID) ? rule.optionID == activeRule.optionID : rule.title == activeRule.title));

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

        while (selectedChoices.Count < choicesToPick && tempPool.Count > 0)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            LobbyOption chosen = tempPool[randomIndex];
            selectedChoices.Add(chosen);

            string chosenKey = !string.IsNullOrEmpty(chosen.optionID) ? chosen.optionID : chosen.title;

            tempPool.RemoveAll(opt =>
                (!string.IsNullOrEmpty(opt.optionID) ? opt.optionID : opt.title) == chosenKey
            );
        }

        return selectedChoices;
    }

    private void AddTieredBuffsToPool<T>(List<T> buffOptions, List<LobbyOption> targetPool) where T : LobbyOption
    {
        var buffGroups = buffOptions.GroupBy(b => !string.IsNullOrEmpty(b.optionID) ? b.optionID : b.title);

        foreach (var group in buffGroups)
        {
            string groupKey = group.Key;
            var sortedBuffs = group.OrderBy(b => GetBuffValue(b)).ToList();

            if (sortedBuffs.Count == 0) continue;

            int acquiredCount = ActiveModifiers.Count(m =>
                (!string.IsNullOrEmpty(m.optionID) ? m.optionID == groupKey : m.title == groupKey)
            );

            // Cap at max tier: if acquiredCount reaches max available tiers, exclude from pool
            if (acquiredCount < sortedBuffs.Count)
            {
                targetPool.Add(sortedBuffs[acquiredCount]);
            }
        }
    }

    private float GetBuffValue(LobbyOption option)
    {
        if (option is PlayerBuffOption pBuff) return pBuff.buffValue;
        if (option is EnemyBuffOption eBuff) return eBuff.buffValue;
        return 0f;
    }

    public void SavePermanentCoins()
    {
        int coinsToSave = Mathf.FloorToInt(PermanentCoinsEarned);
        if (coinsToSave <= 0) return;

        int totalSaved = PlayerPrefs.GetInt(PERMANENT_COINS_KEY, 0);
        PlayerPrefs.SetInt(PERMANENT_COINS_KEY, totalSaved + coinsToSave);
        PlayerPrefs.Save();

        Debug.Log($"[GameManager] Saved {coinsToSave} coins! Total lifetime coins: {totalSaved + coinsToSave}");
        PermanentCoinsEarned = 0f;
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
            float bgmPitch = 1f + (CollapseLevel * 0.05f);
            AudioManager.Instance.SetBGMPitch(Mathf.Min(bgmPitch, 1.3f));
        }
        else
        {
            AudioManager.Instance.SetBGMPitch(1f);
        }
    }
}