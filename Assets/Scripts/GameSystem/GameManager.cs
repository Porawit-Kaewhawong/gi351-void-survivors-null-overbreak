using System.Collections.Generic;
using TMPro;
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
    public GameObject prefab3D;
    public string optionID;
}

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

    [Header("Progression State")]
    public int currentLevel = 1;

    [Header("Option Pools")]
    public List<LobbyOption> playerBuffOptions = new List<LobbyOption>();
    public List<LobbyOption> enemySelectionOptions = new List<LobbyOption>();
    public List<LobbyOption> enemyBuffOptions = new List<LobbyOption>();
    public List<LobbyOption> levelRuleOptions = new List<LobbyOption>();

    private int totalItems = 0;
    private int collectedItems = 0;
    private bool isFinished = false;

    private GameObject activeFinishPortal;
    private GameObject activeStartPortal;
    private readonly List<GameObject> activeSpawnedPedestals = new List<GameObject>();

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

    // --- LOBBY SELECTION & ROTATION ---

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

    // --- LEVEL TRANSITION FLOW ---

    public void StartSelectedLevel()
    {
        ClearLobbyObjects();

        if (lobbyEnvironmentRoot != null)
        {
            lobbyEnvironmentRoot.SetActive(false);
        }

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.GenerateMap();
        }
    }

    public void OnLevelCompleted()
    {
        currentLevel++;

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
        List<LobbyOption> sourcePool = type switch
        {
            LobbySelectionType.PlayerBuff => playerBuffOptions,
            LobbySelectionType.EnemySelection => enemySelectionOptions,
            LobbySelectionType.EnemyBuff => enemyBuffOptions,
            LobbySelectionType.LevelRule => levelRuleOptions,
            _ => new List<LobbyOption>()
        };

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