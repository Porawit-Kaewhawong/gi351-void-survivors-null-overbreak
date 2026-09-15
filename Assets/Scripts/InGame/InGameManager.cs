using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Settings")]
    public TextMeshProUGUI itemCounterText;
    public string displayFormat = "Items: {0} / {1}";

    [Header("Finish Settings")]
    [Tooltip("Prefab spawned when all items are collected")]
    public GameObject finishPrefab;
    [Tooltip("Vertical height offset to spawn above the starting chunk")]
    public Vector3 finishOffsetAboveStart = new Vector3(0f, 5f, 0f);

    [Header("Runtime Data")]
    private int totalItems = 0;
    private int collectedItems = 0;
    private bool isFinished = false;

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

    private void TriggerFinish()
    {
        isFinished = true;
        Debug.Log("All items collected! Level Complete.");

        if (finishPrefab != null && NullscapeSocketGenerator.Instance != null)
        {
            // Get position of the starting chunk from NullscapeSocketGenerator
            Vector3 startChunkPos = NullscapeSocketGenerator.Instance.StartChunkWorldPosition;
            Vector3 spawnPosition = startChunkPos + finishOffsetAboveStart;

            Instantiate(finishPrefab, spawnPosition, Quaternion.identity);
        }
    }
}