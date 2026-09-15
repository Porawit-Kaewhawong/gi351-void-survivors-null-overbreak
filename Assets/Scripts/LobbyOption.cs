using UnityEngine;
using TMPro; // Needed for TMP_Text

public class LobbyOptionPedestal : MonoBehaviour
{
    [Header("3D Pedestal Data")]
    public LobbyOption data;
    public LobbySelectionType selectionType;

    [Header("Floating Text (Supports Canvas UI & 3D World Text)")]
    public TMP_Text titleText;       // Accepts both TextMeshPro and TextMeshProUGUI
    public TMP_Text descriptionText; // Accepts both TextMeshPro and TextMeshProUGUI

    public void SetupPedestal(LobbyOption optionData, LobbySelectionType type)
    {
        data = optionData;
        selectionType = type;

        if (titleText != null) titleText.text = data.title;
        if (descriptionText != null) descriptionText.text = data.description;
    }

    private void OnMouseDown()
    {
        SelectThisOption();
    }

    public void SelectThisOption()
    {
        if (LobbyGameManager.Instance != null)
        {
            LobbyGameManager.Instance.ConfirmSelection(data, selectionType);
        }
    }
}