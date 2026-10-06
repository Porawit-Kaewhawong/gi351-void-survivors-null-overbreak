using TMPro;
using UnityEngine;

public class LobbySelection : MonoBehaviour
{
    [Header("Data")]
    public LobbyOption data;
    public LobbySelectionType selectionType;

    [Header("Always Visible Visuals")]
    [Tooltip("SpriteRenderer for the icon that stays visible continuously above the pedestal.")]
    public SpriteRenderer iconRenderer;

    [Header("World Space UI Popup (Hover Only)")]
    [Tooltip("World Space Canvas or UI root containing text details, hidden until player looks at pedestal.")]
    public GameObject worldSpaceCanvas;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI interactPromptText;

    private void Awake()
    {
        // Hide details canvas by default until hovered
        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.SetActive(false);
        }
    }

    public void SetupPedestal(LobbyOption optionData, LobbySelectionType type)
    {
        data = optionData;
        selectionType = type;

        // Configure always-visible icon
        if (iconRenderer != null)
        {
            iconRenderer.sprite = (data != null) ? data.icon : null;
            iconRenderer.gameObject.SetActive(data != null && data.icon != null);
        }

        // Configure hover detail texts
        if (data != null)
        {
            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;
        }

        if (interactPromptText != null)
        {
            interactPromptText.text = "[E] CHOOSE";
        }
    }

    public void SetHoverState(bool isHovered)
    {
        // Only toggle the detail text canvas
        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.SetActive(isHovered);
        }
    }

    public void SelectThisOption()
    {
        if (GameManager.Instance != null && data != null)
        {
            GameManager.Instance.ConfirmSelection(data, selectionType);
        }
    }
}