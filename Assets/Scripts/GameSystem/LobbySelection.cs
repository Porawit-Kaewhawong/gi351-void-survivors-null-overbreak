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

    public void SetupPedestal(LobbyOption optionData, LobbySelectionType type)
    {
        data = optionData;
        selectionType = type;

        if (titleText != null) titleText.text = data?.title ?? string.Empty;
        if (descriptionText != null) descriptionText.text = data?.description ?? string.Empty;
    }

    private void OnMouseDown()
    {
        SelectThisOption();
    }

    public void SelectThisOption()
    {
        if (GameManager.Instance != null && data != null)
        {
            GameManager.Instance.ConfirmSelection(data, selectionType);
        }
    }
}