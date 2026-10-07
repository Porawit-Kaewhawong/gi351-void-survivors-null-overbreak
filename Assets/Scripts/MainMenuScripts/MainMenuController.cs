using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Exact name of your first gameplay or lobby scene in Build Settings.")]
    public string firstLevelSceneName = "MainLevel";

    [Header("UI Panels")]
    public GameObject mainButtonsPanel;
    public GameObject shopPanel;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    // --- BUTTON ACTIONS ---

    public void OnClickStart()
    {
        Time.timeScale = 1f;

        // Check if a LoreDialogueManager exists in scene to handle intro dialogue
        if (LoreDialogueManager.Instance != null)
        {
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);

            LoreDialogueManager.Instance.StartIntroDialogue(() =>
            {
                SceneManager.LoadScene(firstLevelSceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(firstLevelSceneName);
        }
    }

    public void OnClickOpenShop()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);

            if (shopPanel.TryGetComponent<MainMenuShop>(out var shopScript))
            {
                shopScript.UpdateShopUI();
            }
        }
    }

    public void OnClickCloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
    }

    public void OnClickQuit()
    {
        Debug.Log("[MainMenu] Quitting Game...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}