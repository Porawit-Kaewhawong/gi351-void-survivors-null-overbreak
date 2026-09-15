using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

public enum LobbySelectionType
{
    PlayerBuff = 0,
    EnemySelection = 1,
    EnemyBuff = 2,
    LevelRule = 3
}

[System.Serializable]
public class LobbyOption
{
    public string title;
    [TextArea] public string description;
    public GameObject prefab3D; // The actual 3D model spawned on the pedestal
    public string optionID;
}

public class LobbyGameManager : MonoBehaviour
{
    public static LobbyGameManager Instance { get; private set; }

    [Header("Lobby Return Setup")]
    [Tooltip("Assign an empty GameObject inside your Lobby room where the player will spawn upon returning.")]
    public Transform lobbySpawnPoint;

    [Header("Lobby Environment Control")]
    [Tooltip("Assign the root parent GameObject containing your 3D Lobby environment/room.")]
    public GameObject lobbyEnvironmentRoot;

    [Header("Progression State")]
    public int currentLevel = 1;

    [Header("3D Spawn Locations in Lobby")]
    [Tooltip("3D positions where selection options will spawn (e.g., Left, Center, Right pedestals).")]
    public Transform[] optionSpawnPoints;

    [Tooltip("3D position where the Start Level Portal/Door will spawn after a choice is selected.")]
    public Transform startPortalSpawnPoint;

    [Header("3D Start Portal Setup")]
    public GameObject startPortalPrefab;

    [Header("Option Pools (Assign 3D Prefabs here)")]
    public List<LobbyOption> playerBuffOptions;
    public List<LobbyOption> enemySelectionOptions;
    public List<LobbyOption> enemyBuffOptions;
    public List<LobbyOption> levelRuleOptions;

    private List<GameObject> activeSpawnedPedestals = new List<GameObject>();
    private GameObject activeStartPortal;
    private LobbyOption lastSelectedOption;

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
    }

    // --- LEVEL SELECTION ROTATION (0 -> 1 -> 2 -> 3 -> 0 ...) ---

    public LobbySelectionType GetSelectionTypeForLevel(int level)
    {
        int patternIndex = (level - 1) % 4;
        return (LobbySelectionType)patternIndex;
    }

    public void OpenLobbyForCurrentLevel()
    {
        ClearLobbyObjects();
        lastSelectedOption = null;

        LobbySelectionType currentType = GetSelectionTypeForLevel(currentLevel);
        Spawn3DSelectionOptions(currentType);
    }

    // --- SPAWN 3D OPTIONS ON PEDESTALS ---

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
                // Fallback primitive if no 3D prefab is assigned
                spawnedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spawnedObj.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                spawnedObj.transform.SetParent(spawnPoint);
            }

            // Ensure interactable pedestal logic exists on the object
            LobbyOptionPedestal pedestal = spawnedObj.GetComponent<LobbyOptionPedestal>();
            if (pedestal == null)
            {
                pedestal = spawnedObj.AddComponent<LobbyOptionPedestal>();
            }

            pedestal.SetupPedestal(optionData, type);
            activeSpawnedPedestals.Add(spawnedObj);
        }
    }

    // --- CHOICE CONFIRMATION & 3D START PORTAL SPAWN ---

    public void ConfirmSelection(LobbyOption chosenOption, LobbySelectionType type)
    {
        lastSelectedOption = chosenOption;

        // Store modifier into active run
        switch (type)
        {
            case LobbySelectionType.PlayerBuff: playerBuffOptions.Add(chosenOption); break;
            case LobbySelectionType.EnemySelection: enemySelectionOptions.Add(chosenOption); break;
            case LobbySelectionType.EnemyBuff: enemyBuffOptions.Add(chosenOption); break;
            case LobbySelectionType.LevelRule: levelRuleOptions.Add(chosenOption); break;
        }

        // Despawn 3D selections
        ClearLobbyObjects();

        // Spawn 3D Start Level Portal/Button
        Spawn3DStartPortal();
    }

    private void Spawn3DStartPortal()
    {
        if (startPortalPrefab != null && startPortalSpawnPoint != null)
        {
            activeStartPortal = Instantiate(startPortalPrefab, startPortalSpawnPoint.position, startPortalSpawnPoint.rotation, startPortalSpawnPoint);

            if (activeStartPortal.GetComponent<Lobby3DStartPortal>() == null)
            {
                activeStartPortal.AddComponent<Lobby3DStartPortal>();
            }
        }
        else
        {
            Debug.LogWarning("[LobbyManager] Start Portal Prefab or Spawn Point missing. Launching directly.");
            StartSelectedLevel();
        }
    }

    // --- LAUNCH LEVEL INTO GAME ---

    public void StartSelectedLevel()
    {
        ClearLobbyObjects();

        // Hide the Lobby environment when transitioning to the level
        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(false);
        }

        // Trigger in-game level generator & teleport
        if (NullscapeSocketGenerator.Instance != null)
        {
            NullscapeSocketGenerator.Instance.GenerateMap();
        }
    }

    public void OnLevelCompleted()
    {
        currentLevel++; // Level 1 -> Level 2 -> Level 3 ...

        // 1. Remove in-game procedural map
        if (NullscapeSocketGenerator.Instance != null)
        {
            NullscapeSocketGenerator.Instance.ClearMap();
        }

        // 2. Re-enable the Lobby 3D environment
        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(true);
        }

        NullscapeSocketGenerator mapGenerator = NullscapeSocketGenerator.Instance;
        mapGenerator.mapRadius++;

        // 3. Teleport player back to the Lobby
        TeleportPlayerToLobby();

        // 4. Open 3D selection pedestals for the new level
        OpenLobbyForCurrentLevel();
    }

    private void TeleportPlayerToLobby()
    {
        if (lobbySpawnPoint == null)
        {
            Debug.LogWarning("[LobbyGameManager] Lobby Spawn Point reference is missing!");
            return;
        }

        Transform playerTransform = (NullscapeSocketGenerator.Instance != null)
            ? NullscapeSocketGenerator.Instance.player
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
        // Select the list matching the current level selection type
        List<LobbyOption> sourcePool = type switch
        {
            LobbySelectionType.PlayerBuff => playerBuffOptions,
            LobbySelectionType.EnemySelection => enemySelectionOptions,
            LobbySelectionType.EnemyBuff => enemyBuffOptions,
            LobbySelectionType.LevelRule => levelRuleOptions,
            _ => new List<LobbyOption>()
        };

        // Randomly pick up to 3 unique options from the pool to offer the player
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