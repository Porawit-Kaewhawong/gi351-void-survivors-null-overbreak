using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class RunSummaryUI : MonoBehaviour
{
    private static RunSummaryUI _instance;
    public static RunSummaryUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RunSummaryUI>(FindObjectsInactive.Include);
            }
            return _instance;
        }
    }

    [Header("UI Panel & Text Components")]
    public GameObject summaryPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI coinsEarnedText;
    public TextMeshProUGUI collapseLevelText;
    public TextMeshProUGUI survivalTimeText;
    public TextMeshProUGUI itemsCollectedText;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(false);
        }
    }

    public void ShowDeathSummary()
    {
        // Ensure the script's game object is active so Coroutines and UI calls function
        gameObject.SetActive(true);

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(true);
        }

        Time.timeScale = 0f; // Pause game time

        // Unlock mouse cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (titleText != null)
            titleText.text = "YOU DIED";

        if (GameManager.Instance != null)
        {
            int coins = Mathf.FloorToInt(GameManager.Instance.PermanentCoinsEarned);
            int collapseLvl = GameManager.Instance.CollapseLevel;
            float survivalTime = GameManager.Instance.PostExitSurvivalTime;
            int collected = GameManager.Instance.CollectedItems;
            int total = GameManager.Instance.TotalItems;

            if (coinsEarnedText != null) coinsEarnedText.text = $"Coins Earned: +{coins}";
            if (collapseLevelText != null) collapseLevelText.text = $"Max Collapse Level: LVL {collapseLvl}";
            if (survivalTimeText != null) survivalTimeText.text = $"Overtime Survived: {survivalTime:F1}s";
            if (itemsCollectedText != null) itemsCollectedText.text = $"Items Gathered: {collected} / {total}";

            GameManager.Instance.SavePermanentCoins();
        }
    }

    public void OnClickRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnClickMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}