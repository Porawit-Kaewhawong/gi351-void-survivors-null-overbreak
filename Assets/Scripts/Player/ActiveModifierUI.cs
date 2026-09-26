using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActiveModifiersUI : MonoBehaviour
{
    public static ActiveModifiersUI Instance { get; private set; }

    [Header("Toggle Settings")]
    [Tooltip("Key used to open/close the modifier display HUD.")]
    public KeyCode toggleKey = KeyCode.M;

    [Tooltip("The parent GameObject containing the UI background/grid (to hide when closed).")]
    public GameObject hudPanel;

    [Header("Container & Prefab")]
    public Transform slotContainer;
    public GameObject slotPrefab;

    [Header("Tooltip Panel References")]
    public GameObject tooltipContainer;
    public TextMeshProUGUI tooltipTitleText;
    public TextMeshProUGUI tooltipDescriptionText;

    private int lastKnownModifierCount = -1;
    private bool isHUDVisible = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        HideTooltip();
        SetHUDVisibility(false); // Default to closed on game start
    }

    private void Update()
    {
        // Toggle HUD on keypress
        if (Input.GetKeyDown(toggleKey))
        {
            SetHUDVisibility(!isHUDVisible);
        }

        // Auto-refresh when modifier count changes
        if (GameManager.Instance != null && GameManager.Instance.ActiveModifiers.Count != lastKnownModifierCount)
        {
            RefreshUI();
        }
    }

    public void SetHUDVisibility(bool visible)
    {
        isHUDVisible = visible;

        if (hudPanel != null)
        {
            hudPanel.SetActive(isHUDVisible);
        }

        if (isHUDVisible)
        {
            // Unlock cursor for hovering over icons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshUI();
        }
        else
        {
            // Re-lock cursor for gameplay
            HideTooltip();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void RefreshUI()
    {
        if (GameManager.Instance == null || slotContainer == null || slotPrefab == null) return;

        lastKnownModifierCount = GameManager.Instance.ActiveModifiers.Count;

        // Clear existing slots
        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }

        // Group modifiers by optionID (or title fallback)
        var groupedModifiers = GameManager.Instance.ActiveModifiers
            .GroupBy(m => !string.IsNullOrEmpty(m.optionID) ? m.optionID : m.title)
            .Select(group => new
            {
                FirstItem = group.First(),
                Count = group.Count()
            });

        // Spawn grouped icons with stack multipliers
        foreach (var group in groupedModifiers)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotContainer);
            ModifierSlotUI slotUI = newSlot.GetComponent<ModifierSlotUI>();

            if (slotUI != null)
            {
                slotUI.SetupSlot(group.FirstItem, group.Count, this);
            }
        }
    }

    public void ShowTooltip(string title, string description, int stackCount, Vector3 slotPosition)
    {
        if (tooltipContainer == null || !isHUDVisible) return;

        if (tooltipTitleText != null)
        {
            tooltipTitleText.text = stackCount > 1 ? $"{title} (x{stackCount})" : title;
        }

        if (tooltipDescriptionText != null)
        {
            tooltipDescriptionText.text = description;
        }

        tooltipContainer.SetActive(true);

        // Position tooltip slightly above the slot
        tooltipContainer.transform.position = slotPosition + new Vector3(0f, 40f, 0f);
    }

    public void HideTooltip()
    {
        if (tooltipContainer != null)
        {
            tooltipContainer.SetActive(false);
        }
    }
}