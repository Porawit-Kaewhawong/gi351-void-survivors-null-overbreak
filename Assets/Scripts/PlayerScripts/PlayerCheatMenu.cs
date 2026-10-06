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
    public float windowWidth = 450f;
    public float windowHeight = 600f;
    [Tooltip("Pause gameplay time when the cheat menu is active.")]
    public bool pauseTimeWhenOpen = true;

    // God Mode State
    public static bool GodMode { get; private set; } = false;

    private bool showMenu = false;
    private Rect windowRect;
    private Vector2 scrollPosition = Vector2.zero;
    private int selectedTab = 0;
    private readonly string[] tabNames = new string[] {
        "Level",
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

    // --- TAB 1: LEVEL CONTROLS ---
    private void DrawLevelControls()
    {
        GUILayout.Label("<b>Level Navigation</b>", headerStyle);

        if (GUILayout.Button("⏩ Skip Current Level (Trigger Finish Portal)", buttonStyle, GUILayout.MinHeight(32)))
        {
            GameManager.Instance.TriggerFinish();
        }

        if (GUILayout.Button("🌀 Fast-Forward +1 Level & Radius", buttonStyle, GUILayout.MinHeight(32)))
        {
            JumpLevels(1);
        }

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 Level (+1 Radius)", buttonStyle, GUILayout.MinHeight(30))) JumpLevels(1);
        if (GUILayout.Button("+5 Levels (+5 Radius)", buttonStyle, GUILayout.MinHeight(30))) JumpLevels(5);
        if (GUILayout.Button("+10 Levels (+10 Radius)", buttonStyle, GUILayout.MinHeight(30))) JumpLevels(10);
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label("<b>Simulated Level Skips</b>", headerStyle);

        if (GUILayout.Button("🎲 Skip +10 Levels with 3-Choice Progression\n<i>(Selection 1: Rotated Category | Selections 2 & 3: Random Category Choices)</i>", buttonStyle, GUILayout.MinHeight(48)))
        {
            SkipLevelsWithSimulatedChoices(levelCount: 10, choicesPerLevel: 3);
        }

        GUILayout.Space(15);
        GUILayout.Label("<b>Player Cheats & Utilities</b>", headerStyle);

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

        // SECTION 2: PLAYER BUFFS (TIERED)
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Player Buff Options (Tiered Progression)</b>", headerStyle);
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
                    (!string.IsNullOrEmpty(m.optionID) ? m.optionID == key : m.title == key));

                if (acquiredCount < sortedBuffs.Count)
                {
                    PlayerBuffOption nextBuff = sortedBuffs[acquiredCount];
                    string labelText = $"Apply Tier {acquiredCount + 1}/{sortedBuffs.Count}: <b>{nextBuff.title}</b> (+{nextBuff.buffValue}% {nextBuff.targetStat})";

                    if (GUILayout.Button(labelText, buttonStyle, GUILayout.MinHeight(36)))
                    {
                        ApplyModifierDirectly(nextBuff);
                    }
                }
                else
                {
                    PlayerBuffOption maxBuff = sortedBuffs.Last();
                    GUI.enabled = false;
                    GUILayout.Button($"<b>{maxBuff.title}</b> [MAX TIER REACHED ({sortedBuffs.Count}/{sortedBuffs.Count})]", buttonStyle, GUILayout.MinHeight(32));
                    GUI.enabled = true;
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

        // SECTION 2: ENEMY BUFFS (TIERED)
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Enemy Buff Options (Tiered Progression)</b>", headerStyle);
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
                    (!string.IsNullOrEmpty(m.optionID) ? m.optionID == key : m.title == key));

                if (acquiredCount < sortedBuffs.Count)
                {
                    EnemyBuffOption nextBuff = sortedBuffs[acquiredCount];
                    string labelText = $"Apply Tier {acquiredCount + 1}/{sortedBuffs.Count}: <b>{nextBuff.title}</b> (+{nextBuff.buffValue} {nextBuff.targetStat})";

                    if (GUILayout.Button(labelText, buttonStyle, GUILayout.MinHeight(36)))
                    {
                        ApplyModifierDirectly(nextBuff);
                    }
                }
                else
                {
                    EnemyBuffOption maxBuff = sortedBuffs.Last();
                    GUI.enabled = false;
                    GUILayout.Button($"<b>{maxBuff.title}</b> [MAX TIER REACHED ({sortedBuffs.Count}/{sortedBuffs.Count})]", buttonStyle, GUILayout.MinHeight(32));
                    GUI.enabled = true;
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

    private void SkipLevelsWithSimulatedChoices(int levelCount, int choicesPerLevel = 3)
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

            for (int choiceIndex = 0; choiceIndex < choicesPerLevel; choiceIndex++)
            {
                if (choiceIndex == 0)
                {
                    // 1st Choice: Rotated Primary Category for Level
                    LobbySelectionType primaryType = GameManager.Instance.GetSelectionTypeForLevel(simulatedLevel);
                    ApplyRandomFromType(primaryType);
                }
                else
                {
                    // 2nd & 3rd Choices: Random Non-Rule Category (Weapon, EnemyType, PlayerBuff, EnemyBuff)
                    LobbySelectionType secondaryType = (LobbySelectionType)Random.Range(0, 4);
                    ApplyRandomFromType(secondaryType);
                }
            }
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

        Debug.Log($"[CheatMenu] Simulated {levelCount} levels with {choicesPerLevel}-choice progression.");
    }

    private void ApplyRandomFromType(LobbySelectionType selectionType)
    {
        List<LobbyOption> pool = GameManager.Instance.FetchOptionPoolForType(selectionType);

        if (pool == null || pool.Count == 0) return;

        int randomIndex = Random.Range(0, pool.Count);
        ApplyModifierDirectly(pool[randomIndex]);
    }

    private void JumpLevels(int count)
    {
        if (GameManager.Instance == null || count <= 0) return;

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.mapRadius += count;
        }

        GameManager.Instance.currentLevel += (count - 1);
        GameManager.Instance.OnLevelCompleted();
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
                // Group weapon options by optionID / prefab name (matching GameManager.cs)
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