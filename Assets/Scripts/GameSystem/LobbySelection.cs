using TMPro;
using UnityEngine;

public class LobbySelection : MonoBehaviour
{
    [Header("3D Pedestal Data")]
    public LobbyOption data;
    public LobbySelectionType selectionType;

    [Header("Floating Text Setup")]
    public TMP_Text titleText;
    public TMP_Text descriptionText;

    private bool hasBeenSelected = false;

    public void SetupPedestal(LobbyOption optionData, LobbySelectionType type)
    {
        data = optionData;
        selectionType = type;
        hasBeenSelected = false; // Reset state when pedestal is initialized

        if (titleText != null) titleText.text = data?.title ?? string.Empty;
        if (descriptionText != null) descriptionText.text = data?.description ?? string.Empty;
    }

    private void OnMouseDown()
    {
        SelectThisOption();
    }

    public void SelectThisOption()
    {
        // Guard against rapid double-clicks
        if (hasBeenSelected) return;

        if (GameManager.Instance != null && data != null)
        {
            hasBeenSelected = true;
            GameManager.Instance.ConfirmSelection(data, selectionType);
        }
    }
}