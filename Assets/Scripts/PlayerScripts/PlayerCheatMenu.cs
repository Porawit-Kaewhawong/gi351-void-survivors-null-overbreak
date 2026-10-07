using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CheatMenu : MonoBehaviour
{
    [Header("Toggle Settings")]
    [Tooltip("Hotkey used to open/close the cheat menu.")]
    public KeyCode toggleKey = KeyCode.BackQuote; // '~' key
    public KeyCode alternateToggleKey = KeyCode.F1;

    [Header("Menu Settings")]
    public float windowWidth = 480f;
    public float windowHeight = 620f;
    [Tooltip("Pause gameplay time when the cheat menu is active.")]
    public bool pauseTimeWhenOpen = true;

    // God Mode State
    public static bool GodMode { get; private set; } = false;

    private bool showMenu = false;
    private Rect windowRect;
    private Vector2 scrollPosition = Vector2.zero;
    private int selectedTab = 0;
    private readonly string[] tabNames = new string[] {
        "Level & Progression",
        "Weapons & Buffs",
        "Enemies",
        "Rules",
        "Active Modifiers"
    };

    private float savedTimeScale = 1f;

    // Cached GUI Styles
    private GUIStyle headerStyle;
    private GUIStyle boldLabelStyle;
    private GUIStyle boxStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        // Center window on screen
        windowRect = new Rect(
            (Screen.width - windowWidth) / 2f,
            (Screen.height - windowHeight) / 2f,
            windowWidth,
            windowHeight
        );
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(alternateToggleKey))
        {
            ToggleMenu(!showMenu);
        }
    }

    public void ToggleMenu(bool open)
    {
        showMenu = open;

        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (showMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (pauseTimeWhenOpen)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (player != null)
            {
                player.enabled = false;
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (pauseTimeWhenOpen)
            {
                Time.timeScale = savedTimeScale > 0 ? savedTimeScale : 1f;
            }

            if (player != null)
            {
                player.enabled = true;
            }
        }
    }

    private void OnGUI()
    {
        if (!showMenu) return;

        InitStyles();

        // Clamp window within screen bounds
        windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);

        windowRect = GUILayout.Window(999, windowRect, DrawCheatWindow, "🛠 DEVELOPER CHEAT MENU");
    }

    private void InitStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
        }

        if (boldLabelStyle == null)
        {
            boldLabelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true
            };
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 6, 6)
            };
        }
    }

    private void DrawCheatWindow(int windowID)
    {
        if (GameManager.Instance == null)
        {
            GUILayout.Label("<b>GameManager Instance not found!</b>", boldLabelStyle);
            GUI.DragWindow(new Rect(0, 0, windowWidth, 25));
            return;
        }

        // Header Status Info
        int currentRadius = MapGenerator.Instance != null ? MapGenerator.Instance.mapRadius : 0;
        int activeModCount = GameManager.Instance.ActiveModifiers != null ? GameManager.Instance.ActiveModifiers.Count : 0;

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label($"Level: <b>{GameManager.Instance.currentLevel}</b> | Radius: <b>{currentRadius}</b> | Active Mods: <b>{activeModCount}</b>", boldLabelStyle);
        GUILayout.EndVertical();

        GUILayout.Space(5);

        // Navigation Toolbar
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

        GUILayout.Space(10);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        switch (selectedTab)
        {
            case 0:
                DrawLevelControls();
                break;
            case 1:
                DrawPlayerOptions();
                break;
            case 2:
                DrawEnemyOptions();
                break;
            case 3:
                DrawLevelRules();
                break;
            case 4:
                DrawActiveModifiers();
                break;
        }

        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Close Menu (~ / F1)", GUILayout.Height(30)))
        {
            ToggleMenu(false);
        }

        GUI.DragWindow(new Rect(0, 0, windowWidth, 25));
    }

    // --- TAB 1: LEVEL & PROGRESSION CONTROLS ---
    private void DrawLevelControls()
    {
        GUILayout.Label("<b>Instant Level Fast-Forward (Raw Skip)</b>", headerStyle);

        if (GUILayout.Button("⏩ Trigger Finish Portal / Complete Level", buttonStyle, GUILayout.MinHeight(32)))
        {
            GameManager.Instance.TriggerFinish();
        }

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 Level (+1 Radius)", buttonStyle, GUILayout.MinHeight(30))) JumpLevels(1);
        if (GUILayout.Button("+5 Levels (+5 Radius)", buttonStyle, GUILayout.MinHeight(30))) JumpLevels(5);
        if (GUILayout.Button("+10 Levels (+10 Radius)", buttonStyle, GUILayout.MinHeight(30))) JumpLevels(10);
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label("<b>Simulated Quick Progression (Real Gameplay Order)</b>", headerStyle);
        GUILayout.Label("<i>Executes exact lobby rotation (3 choice phases per level). Picks 1 option per phase from GameManager option pools.</i>", boldLabelStyle);
        GUILayout.Space(5);

        if (GUILayout.Button("🎲 +1 Level with Real Choice Order (3 Picks)", buttonStyle, GUILayout.MinHeight(34)))
        {
            SkipLevelsWithSimulatedChoices(levelCount: 1);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🎲 +5 Levels Draft (+15 Picks)", buttonStyle, GUILayout.MinHeight(34)))
        {
            SkipLevelsWithSimulatedChoices(levelCount: 5);
        }
        if (GUILayout.Button("🎲 +10 Levels Draft (+30 Picks)", buttonStyle, GUILayout.MinHeight(34)))
        {
            SkipLevelsWithSimulatedChoices(levelCount: 10);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label("<b>Player Utilities</b>", headerStyle);

        GodMode = GUILayout.Toggle(GodMode, " 🛡️ God Mode (Invincibility)");

        if (GUILayout.Button("❤️ Restore Full Health & Shield", buttonStyle, GUILayout.MinHeight(32)))
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
        // SECTION 1: WEAPONS
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Instant Weapon Equip (Stackable)</b>", headerStyle);
        GUILayout.Space(5);

        if (GameManager.Instance.playerWeaponOptions != null && GameManager.Instance.playerWeaponOptions.Count > 0)
        {
            var weaponGroups = GameManager.Instance.playerWeaponOptions
                .Where(w => w != null)
                .GroupBy(w => !string.IsNullOrEmpty(w.optionID) ? w.optionID : w.title);

            foreach (var group in weaponGroups)
            {
                var weapon = group.First();
                int currentLevel = GameManager.Instance.ActiveModifiers.Count(m =>
                    m is PlayerWeaponOption w &&
                    (!string.IsNullOrEmpty(w.optionID) ? w.optionID == group.Key : w.title == group.Key));

                string levelTag = currentLevel > 0 ? $" [Lvl {currentLevel}]" : "";
                if (GUILayout.Button($"Equip +1: {weapon.title}{levelTag}", buttonStyle, GUILayout.MinHeight(32)))
                {
                    ApplyModifierDirectly(weapon);
                    ReequipWeaponsOnPlayer();
                }
            }
        }
        else
        {
            GUILayout.Label("No player weapon options configured.");
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // SECTION 2: PLAYER BUFFS (TIERED - INFINITE STACKABLE)
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Player Buff Options (Infinite Tiered Stacking)</b>", headerStyle);
        GUILayout.Space(5);

        if (GameManager.Instance.playerBuffOptions != null && GameManager.Instance.playerBuffOptions.Count > 0)
        {
            var buffGroups = GameManager.Instance.playerBuffOptions
                .Where(b => b != null)
                .GroupBy(b => !string.IsNullOrEmpty(b.optionID) ? b.optionID : b.title);

            foreach (var group in buffGroups)
            {
                string key = group.Key;
                var sortedBuffs = group.OrderBy(b => b.buffValue).ToList();
                int acquiredCount = GameManager.Instance.ActiveModifiers.Count(m =>
                    m is PlayerBuffOption &&
                    (!string.IsNullOrEmpty(m.optionID) ? m.optionID == key : m.title == key));

                // If acquired count is within defined tiers, pick that tier; otherwise repeat max tier indefinitely
                PlayerBuffOption buffToApply = acquiredCount < sortedBuffs.Count ? sortedBuffs[acquiredCount] : sortedBuffs.Last();
                string tierInfo = acquiredCount < sortedBuffs.Count
                    ? $"Tier {acquiredCount + 1}/{sortedBuffs.Count}"
                    : $"Max Tier (Stack x{acquiredCount + 1})";

                string labelText = $"Apply {tierInfo}: <b>{buffToApply.title}</b> (+{buffToApply.buffValue}% {buffToApply.targetStat})";

                if (GUILayout.Button(labelText, buttonStyle, GUILayout.MinHeight(36)))
                {
                    ApplyModifierDirectly(buffToApply);
                }
            }
        }
        else
        {
            GUILayout.Label("No player buff options configured.");
        }
        GUILayout.EndVertical();
    }

    // --- TAB 3: ENEMY SELECTION & BUFFS ---
    private void DrawEnemyOptions()
    {
        // SECTION 1: ENEMY SELECTION
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Add Enemy Types to Run (Stackable)</b>", headerStyle);
        GUILayout.Space(5);

        if (GameManager.Instance.enemySelectionOptions != null && GameManager.Instance.enemySelectionOptions.Count > 0)
        {
            var enemyGroups = GameManager.Instance.enemySelectionOptions
                .Where(e => e != null)
                .GroupBy(e => !string.IsNullOrEmpty(e.optionID) ? e.optionID : e.title);

            foreach (var group in enemyGroups)
            {
                var enemyOpt = group.First();
                int activeCount = GameManager.Instance.ActiveModifiers.Count(m =>
                    m is EnemySelectionOption e &&
                    (!string.IsNullOrEmpty(e.optionID) ? e.optionID == group.Key : e.title == group.Key));

                string stackTag = activeCount > 0 ? $" [x{activeCount}]" : "";
                if (GUILayout.Button($"Add Enemy: {enemyOpt.title}{stackTag} (Base: {enemyOpt.enemyCount})", buttonStyle, GUILayout.MinHeight(32)))
                {
                    ApplyModifierDirectly(enemyOpt);
                }
            }
        }
        else
        {
            GUILayout.Label("No enemy selection options configured.");
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        // SECTION 2: ENEMY BUFFS (TIERED - INFINITE STACKABLE)
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Enemy Buff Options (Infinite Tiered Stacking)</b>", headerStyle);
        GUILayout.Space(5);

        if (GameManager.Instance.enemyBuffOptions != null && GameManager.Instance.enemyBuffOptions.Count > 0)
        {
            var buffGroups = GameManager.Instance.enemyBuffOptions
                .Where(b => b != null)
                .GroupBy(b => !string.IsNullOrEmpty(b.optionID) ? b.optionID : b.title);

            foreach (var group in buffGroups)
            {
                string key = group.Key;
                var sortedBuffs = group.OrderBy(b => b.buffValue).ToList();
                int acquiredCount = GameManager.Instance.ActiveModifiers.Count(m =>
                    m is EnemyBuffOption &&
                    (!string.IsNullOrEmpty(m.optionID) ? m.optionID == key : m.title == key));

                // If acquired count is within defined tiers, pick that tier; otherwise repeat max tier indefinitely
                EnemyBuffOption buffToApply = acquiredCount < sortedBuffs.Count ? sortedBuffs[acquiredCount] : sortedBuffs.Last();
                string tierInfo = acquiredCount < sortedBuffs.Count
                    ? $"Tier {acquiredCount + 1}/{sortedBuffs.Count}"
                    : $"Max Tier (Stack x{acquiredCount + 1})";

                string labelText = $"Apply {tierInfo}: <b>{buffToApply.title}</b> (+{buffToApply.buffValue} {buffToApply.targetStat})";

                if (GUILayout.Button(labelText, buttonStyle, GUILayout.MinHeight(36)))
                {
                    ApplyModifierDirectly(buffToApply);
                }
            }
        }
        else
        {
            GUILayout.Label("No enemy buff options configured.");
        }
        GUILayout.EndVertical();
    }

    // --- TAB 4: LEVEL RULES ---
    private void DrawLevelRules()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Active Level Rules (Non-Stackable)</b>", headerStyle);
        GUILayout.Space(5);

        if (GameManager.Instance.levelRuleOptions != null && GameManager.Instance.levelRuleOptions.Count > 0)
        {
            foreach (var rule in GameManager.Instance.levelRuleOptions)
            {
                if (rule == null) continue;

                bool active = GameManager.Instance.ActiveModifiers != null && GameManager.Instance.ActiveModifiers.Exists(m =>
                    m is LevelRuleOption activeRule &&
                    (!string.IsNullOrEmpty(rule.optionID) ? activeRule.optionID == rule.optionID : activeRule.title == rule.title));

                string status = active ? "[ACTIVE] " : "";

                if (GUILayout.Button($"{status}{rule.title} ({rule.ruleType}: {rule.ruleValue})", buttonStyle, GUILayout.MinHeight(34)))
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
        else
        {
            GUILayout.Label("No level rule options configured.");
        }
        GUILayout.EndVertical();
    }

    // --- TAB 5: ACTIVE MODIFIERS MANAGEMENT ---
    private void DrawActiveModifiers()
    {
        GUILayout.Label("<b>Currently Active Modifiers</b>", headerStyle);

        if (GameManager.Instance.ActiveModifiers == null || GameManager.Instance.ActiveModifiers.Count == 0)
        {
            GUILayout.Label("No modifiers currently active.");
            return;
        }

        if (GUILayout.Button("🗑️ Clear All Active Modifiers", buttonStyle, GUILayout.MinHeight(32)))
        {
            GameManager.Instance.ActiveModifiers.Clear();
            ReequipWeaponsOnPlayer();
            Debug.Log("[CheatMenu] Cleared all active modifiers.");
            return;
        }

        GUILayout.Space(10);

        for (int i = GameManager.Instance.ActiveModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = GameManager.Instance.ActiveModifiers[i];
            if (modifier == null) continue;

            string info = "";
            if (modifier is PlayerBuffOption pBuff) info = $" (+{pBuff.buffValue}% {pBuff.targetStat})";
            else if (modifier is EnemyBuffOption eBuff) info = $" (+{eBuff.buffValue} {eBuff.targetStat})";
            else if (modifier is LevelRuleOption rule) info = $" ({rule.ruleType}: {rule.ruleValue})";

            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"<b>[{modifier.GetType().Name.Replace("Option", "")}]</b> {modifier.title}{info}", boldLabelStyle);

            if (GUILayout.Button("Remove", GUILayout.Width(70), GUILayout.Height(25)))
            {
                GameManager.Instance.ActiveModifiers.RemoveAt(i);
                ReequipWeaponsOnPlayer();
                Debug.Log($"[CheatMenu] Removed modifier: {modifier.title}");
            }
            GUILayout.EndHorizontal();
        }
    }

    // --- HELPER METHODS FOR SIMULATED LEVEL SKIPS & MODIFIERS ---

    /// <summary>
    /// Simulates exact real gameplay level choices.
    /// Runs 3 selection phases per level, starting with the level's primary category
    /// and cycling sequentially via GetNextSelectionType().
    /// </summary>
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
            int levelToSimulate = startLevel + i;

            // Phase 1 Category: Primary rotated category for this level
            LobbySelectionType currentType = GameManager.Instance.GetSelectionTypeForLevel(levelToSimulate);

            // Execute 3 choice phases per level
            for (int phase = 0; phase < 3; phase++)
            {
                // Fetch up to 3 pedestal choices using GameManager's pool generator
                List<LobbyOption> offeredChoices = GameManager.Instance.FetchOptionPoolForType(currentType);

                if (offeredChoices != null && offeredChoices.Count > 0)
                {
                    // Randomly pick 1 choice out of the 3 offered pedestals
                    LobbyOption pickedOption = offeredChoices[Random.Range(0, offeredChoices.Count)];
                    ApplyModifierDirectly(pickedOption);
                }

                // Advance category to next phase matching GameManager's GetNextSelectionType order
                currentType = GetNextSelectionType(currentType);
            }
        }

        // Advance level counter & map radius
        JumpLevels(levelCount);

        // Apply rules to active gameplay environment if outside lobby
        bool isInLobby = GameManager.Instance.lobbyEnvironmentRoot != null && GameManager.Instance.lobbyEnvironmentRoot.activeInHierarchy;
        if (!isInLobby && LevelRuleManager.Instance != null)
        {
            LevelRuleManager.Instance.ApplyActiveLevelRules();
        }

        // Re-sync player weapon objects
        ReequipWeaponsOnPlayer();

        Debug.Log($"[CheatMenu] Fast-forwarded {levelCount} level(s) using exact gameplay rotation order ({levelCount * 3} choices processed).");
    }

    /// <summary>
    /// Matches GameManager's GetNextSelectionType() sequence (cycling PlayerWeapon -> EnemySelection -> PlayerBuff -> EnemyBuff).
    /// </summary>
    private LobbySelectionType GetNextSelectionType(LobbySelectionType currentType)
    {
        int totalCategories = 4; // Cycles through PlayerWeapon, EnemySelection, PlayerBuff, EnemyBuff
        int nextIndex = ((int)currentType + 1) % totalCategories;

        for (int i = 0; i < totalCategories; i++)
        {
            LobbySelectionType candidate = (LobbySelectionType)((nextIndex + i) % totalCategories);
            if (HasAvailableOptionsInGameManager(candidate))
            {
                return candidate;
            }
        }

        return currentType;
    }

    private bool HasAvailableOptionsInGameManager(LobbySelectionType type)
    {
        if (GameManager.Instance == null) return false;

        switch (type)
        {
            case LobbySelectionType.PlayerWeapon: return GameManager.Instance.playerWeaponOptions != null && GameManager.Instance.playerWeaponOptions.Count > 0;
            case LobbySelectionType.EnemySelection: return GameManager.Instance.enemySelectionOptions != null && GameManager.Instance.enemySelectionOptions.Count > 0;
            case LobbySelectionType.PlayerBuff: return GameManager.Instance.playerBuffOptions != null && GameManager.Instance.playerBuffOptions.Count > 0;
            case LobbySelectionType.EnemyBuff: return GameManager.Instance.enemyBuffOptions != null && GameManager.Instance.enemyBuffOptions.Count > 0;
            case LobbySelectionType.LevelRule: return GameManager.Instance.levelRuleOptions != null && GameManager.Instance.levelRuleOptions.Count > 0;
            default: return false;
        }
    }

    private void JumpLevels(int count)
    {
        if (GameManager.Instance == null || count <= 0) return;

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.mapRadius += count;
        }

        GameManager.Instance.currentLevel += count;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.ResetHealthAndShield();
        }
    }

    private void ApplyModifierDirectly(LobbyOption option)
    {
        if (option == null || GameManager.Instance == null || GameManager.Instance.ActiveModifiers == null) return;

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
        PlayerController player = FindFirstObjectByType<PlayerController>();
        GameObject playerObj = player != null ? player.gameObject : GameObject.FindWithTag("Player");

        if (playerObj != null)
        {
            Transform weaponContainer = playerObj.transform.Find("WeaponContainer");
            if (weaponContainer == null)
            {
                GameObject containerObj = new GameObject("WeaponContainer");
                containerObj.transform.SetParent(playerObj.transform);
                containerObj.transform.localPosition = Vector3.zero;
                containerObj.transform.localRotation = Quaternion.identity;
                weaponContainer = containerObj.transform;
            }

            // Destroy existing weapon objects
            for (int i = weaponContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(weaponContainer.GetChild(i).gameObject);
            }

            if (GameManager.Instance != null && GameManager.Instance.ActiveModifiers != null)
            {
                // Group weapon options by optionID / prefab name
                var weaponGroups = GameManager.Instance.ActiveModifiers
                    .OfType<PlayerWeaponOption>()
                    .Where(w => w.weaponPrefab != null)
                    .GroupBy(w => !string.IsNullOrEmpty(w.optionID) ? w.optionID : w.weaponPrefab.name);

                foreach (var group in weaponGroups)
                {
                    PlayerWeaponOption sampleWeapon = group.First();
                    int weaponLevel = group.Count();

                    GameObject weaponObj = Instantiate(sampleWeapon.weaponPrefab, weaponContainer);

                    if (weaponObj.TryGetComponent<BaseWeapon>(out var weaponScript))
                    {
                        weaponScript.SetWeaponLevel(weaponLevel);
                    }
                }
            }
        }
    }
}