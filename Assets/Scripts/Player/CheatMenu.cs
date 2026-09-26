using UnityEngine;
using System.Collections.Generic;

public class CheatMenu : MonoBehaviour
{
    [Header("Toggle Settings")]
    [Tooltip("Hotkey used to open/close the cheat menu.")]
    public KeyCode toggleKey = KeyCode.BackQuote; // '~' key

    [Header("Menu Appearance")]
    public float windowWidth = 400f;
    public float windowHeight = 550f;

    private bool showMenu = false;
    private Rect windowRect;
    private Vector2 scrollPosition = Vector2.zero;
    private int selectedTab = 0;
    private readonly string[] tabNames = new string[] { "Level Control", "Weapons & Buffs", "Enemy Buffs", "Rules" };

    // Cached GUI Style to prevent GC allocation in OnGUI
    private GUIStyle richTextStyle;

    private void Awake()
    {
        // Center the window on screen
        windowRect = new Rect(
            (Screen.width - windowWidth) / 2f,
            (Screen.height - windowHeight) / 2f,
            windowWidth,
            windowHeight
        );
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.F1))
        {
            ToggleMenu(!showMenu);
        }
    }

    private void ToggleMenu(bool open)
    {
        showMenu = open;

        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (showMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (player != null)
            {
                player.enabled = false;
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (player != null)
            {
                player.enabled = true;
            }
        }
    }

    private void OnGUI()
    {
        if (!showMenu) return;

        if (richTextStyle == null)
        {
            richTextStyle = new GUIStyle(GUI.skin.label) { richText = true };
        }

        windowRect = GUILayout.Window(999, windowRect, DrawCheatWindow, "🛠️ DEVELOPER CHEAT MENU");
    }

    private void DrawCheatWindow(int windowID)
    {
        if (GameManager.Instance == null)
        {
            GUILayout.Label("GameManager Instance not found!");
            GUI.DragWindow();
            return;
        }

        // Current Level State Banner
        int currentRadius = MapGenerator.Instance != null ? MapGenerator.Instance.mapRadius : 0;
        GUILayout.BeginVertical("box");
        GUILayout.Label($"Current Level: <b>{GameManager.Instance.currentLevel}</b> | Map Radius: <b>{currentRadius}</b> | Modifiers: <b>{GameManager.Instance.ActiveModifiers.Count}</b>", richTextStyle);
        GUILayout.EndVertical();

        // Navigation Tabs
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

        GUILayout.Space(10);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        switch (selectedTab)
        {
            case 0: // Level Control
                DrawLevelControls();
                break;
            case 1: // Player Weapons & Buffs
                DrawPlayerOptions();
                break;
            case 2: // Enemy Modifications
                DrawEnemyOptions();
                break;
            case 3: // Level Rules
                DrawLevelRules();
                break;
        }

        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close Menu (or press '~')", GUILayout.Height(30)))
        {
            ToggleMenu(false);
        }

        GUI.DragWindow();
    }

    // --- TAB 1: LEVEL CONTROLS ---
    private void DrawLevelControls()
    {
        GUILayout.Label("<b>Level Navigation</b>", richTextStyle);

        if (GUILayout.Button("⏩ Skip Current Level (Trigger Finish Portal)", GUILayout.Height(35)))
        {
            GameManager.Instance.TriggerFinish();
        }

        if (GUILayout.Button("🌀 Fast-Forward to Next Lobby Level (+1 Radius)", GUILayout.Height(35)))
        {
            JumpLevels(1);
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 Level (+1 Radius)")) JumpLevels(1);
        if (GUILayout.Button("+5 Levels (+5 Radius)")) JumpLevels(5);
        if (GUILayout.Button("+10 Levels (+10 Radius)")) JumpLevels(10);
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label("<b>Simulated Level Skips (2 Choices Per Level)</b>", richTextStyle);

        if (GUILayout.Button("🎲 Skip +10 Levels with 2-Choice Progression\n<i>(Selection 1: Fixed Category Cycle | Selection 2: Random Category)</i>", richTextStyle, GUILayout.Height(45)))
        {
            SkipLevelsWithSimulatedChoices(levelCount: 10);
        }

        GUILayout.Space(15);
        GUILayout.Label("<b>Player Utilities</b>", richTextStyle);

        if (GUILayout.Button("❤️ Reset Player Health & Shield", GUILayout.Height(30)))
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.ResetHealthAndShield();
            }
        }
    }

    // --- TAB 2: WEAPONS & PLAYER BUFFS ---
    private void DrawPlayerOptions()
    {
        GUILayout.Label("<b>Instant Weapon Equip (Stackable)</b>", richTextStyle);
        foreach (var weapon in GameManager.Instance.playerWeaponOptions)
        {
            if (GUILayout.Button($"Equip: {weapon.title}"))
            {
                ApplyModifierDirectly(weapon);
                ReequipWeaponsOnPlayer();
            }
        }

        GUILayout.Space(15);
        GUILayout.Label("<b>Player Buff Options (Respects Sequential Tiers)</b>", richTextStyle);

        // Fetch valid pool using GameManager logic (fetches current tier available)
        List<LobbyOption> validBuffPool = GameManager.Instance.FetchOptionPoolForType(LobbySelectionType.PlayerBuff);

        foreach (var option in validBuffPool)
        {
            if (option is PlayerBuffOption buff)
            {
                if (GUILayout.Button($"Apply Next Tier: {buff.title} (+{buff.buffValue}% {buff.targetStat})"))
                {
                    ApplyModifierDirectly(buff);
                }
            }
        }
    }

    // --- TAB 3: ENEMY SELECTION & BUFFS ---
    private void DrawEnemyOptions()
    {
        GUILayout.Label("<b>Add Enemy Types to Run (Stackable)</b>", richTextStyle);
        foreach (var enemyOpt in GameManager.Instance.enemySelectionOptions)
        {
            if (GUILayout.Button($"Add Enemy: {enemyOpt.title} (Count: {enemyOpt.enemyCount})"))
            {
                ApplyModifierDirectly(enemyOpt);
            }
        }

        GUILayout.Space(15);
        GUILayout.Label("<b>Enemy Buffs (Stackable)</b>", richTextStyle);
        foreach (var enemyBuff in GameManager.Instance.enemyBuffOptions)
        {
            if (GUILayout.Button($"Apply: {enemyBuff.title} (+{enemyBuff.buffValue} {enemyBuff.targetStat})"))
            {
                ApplyModifierDirectly(enemyBuff);
            }
        }
    }

    // --- TAB 4: LEVEL RULES ---
    private void DrawLevelRules()
    {
        GUILayout.Label("<b>Active Level Rules (Non-Stackable)</b>", richTextStyle);
        foreach (var rule in GameManager.Instance.levelRuleOptions)
        {
            bool active = GameManager.Instance.ActiveModifiers.Exists(m =>
                m is LevelRuleOption activeRule &&
                (!string.IsNullOrEmpty(rule.optionID) ? activeRule.optionID == rule.optionID : activeRule.title == rule.title));

            string status = active ? "[ACTIVE] " : "";

            if (GUILayout.Button($"{status}{rule.title} ({rule.ruleType}: {rule.ruleValue})"))
            {
                if (!active)
                {
                    ApplyModifierDirectly(rule);

                    bool isInLobby = GameManager.Instance.lobbyEnvironmentRoot != null && GameManager.Instance.lobbyEnvironmentRoot.activeInHierarchy;
                    if (!isInLobby && LevelRuleManager.Instance != null)
                    {
                        LevelRuleManager.Instance.ApplyActiveLevelRules();
                    }
                }
            }
        }
    }

    // --- HELPER METHODS FOR SIMULATED LEVEL SKIPS ---

    private void SkipLevelsWithSimulatedChoices(int levelCount)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[CheatMenu] Cannot skip levels: GameManager.Instance is null!");
            return;
        }

        int startLevel = GameManager.Instance.currentLevel;

        for (int i = 0; i < levelCount; i++)
        {
            int simulatedLevel = startLevel + i;

            // 1st Selection: Standard Level Rotation
            LobbySelectionType primaryType = GameManager.Instance.GetSelectionTypeForLevel(simulatedLevel);
            ApplyRandomFromType(primaryType);

            // 2nd Selection: Random Non-Rule Category
            LobbySelectionType secondaryType = (LobbySelectionType)Random.Range(0, 4);
            ApplyRandomFromType(secondaryType);
        }

        // Apply rules to active gameplay environment if outside lobby
        bool isInLobby = GameManager.Instance.lobbyEnvironmentRoot != null && GameManager.Instance.lobbyEnvironmentRoot.activeInHierarchy;
        if (!isInLobby && LevelRuleManager.Instance != null)
        {
            LevelRuleManager.Instance.ApplyActiveLevelRules();
        }

        // Advance level progress
        JumpLevels(levelCount);

        // Update player weapon visual slots
        ReequipWeaponsOnPlayer();

        Debug.Log($"[CheatMenu] Simulated {levelCount} levels with 2 choices per level.");
    }

    private void ApplyRandomFromType(LobbySelectionType selectionType)
    {
        // Fetch pool using GameManager to enforce tier progression, unique IDs, and rule checks
        List<LobbyOption> pool = GameManager.Instance.FetchOptionPoolForType(selectionType);

        if (pool == null || pool.Count == 0) return;

        int randomIndex = Random.Range(0, pool.Count);
        ApplyModifierDirectly(pool[randomIndex]);
    }

    private void JumpLevels(int count)
    {
        if (GameManager.Instance == null || count <= 0) return;

        if (MapGenerator.Instance != null && count > 1)
        {
            MapGenerator.Instance.mapRadius += (count / 2);
        }

        GameManager.Instance.currentLevel += (count - 1);
        GameManager.Instance.OnLevelCompleted();
    }

    private void ApplyModifierDirectly(LobbyOption option)
    {
        if (option == null) return;

        if (option is LevelRuleOption ruleOpt)
        {
            bool ruleAlreadyActive = GameManager.Instance.ActiveModifiers.Exists(m =>
                m is LevelRuleOption activeRule &&
                (!string.IsNullOrEmpty(ruleOpt.optionID) ? activeRule.optionID == ruleOpt.optionID : activeRule.title == ruleOpt.title));

            if (ruleAlreadyActive) return;
        }

        GameManager.Instance.ActiveModifiers.Add(option);
        Debug.Log($"[CheatMenu] Directly applied modifier: {option.title}");
    }

    private void ReequipWeaponsOnPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            Transform weaponContainer = playerObj.transform.Find("WeaponContainer");

            if (weaponContainer != null)
            {
                foreach (Transform child in weaponContainer)
                {
                    Destroy(child.gameObject);
                }

                foreach (var modifier in GameManager.Instance.ActiveModifiers)
                {
                    if (modifier is PlayerWeaponOption weaponOpt && weaponOpt.weaponPrefab != null)
                    {
                        Instantiate(weaponOpt.weaponPrefab, weaponContainer);
                    }
                }
            }
        }
    }
}