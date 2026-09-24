using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ModifierSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public TextMeshProUGUI stackCountText; // Or UnityEngine.UI.Text if not using TMPro

    private LobbyOption modifierData;
    private int stackCount;
    private ActiveModifiersUI parentUI;

    public void SetupSlot(LobbyOption option, int count, ActiveModifiersUI uiController)
    {
        modifierData = option;
        stackCount = count;
        parentUI = uiController;

        // Display icon if assigned, otherwise fallback to default tint/sprite
        if (iconImage != null && option.icon != null)
        {
            iconImage.sprite = option.icon;
            iconImage.enabled = true;
        }

        // Show multiplier badge only if stacked 2 or more times
        if (stackCountText != null)
        {
            if (stackCount > 1)
            {
                stackCountText.text = $"x{stackCount}";
                stackCountText.gameObject.SetActive(true);
            }
            else
            {
                stackCountText.gameObject.SetActive(false);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (parentUI != null && modifierData != null)
        {
            string title = modifierData.title;
            string description = GetFormattedDescription(modifierData);

            parentUI.ShowTooltip(title, description, stackCount, transform.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (parentUI != null)
        {
            parentUI.HideTooltip();
        }
    }

    private string GetFormattedDescription(LobbyOption option)
    {
        // Build detailed description based on type
        if (option is PlayerBuffOption buff)
        {
            return $"Increases {buff.targetStat} by {buff.buffValue}%.";
        }
        else if (option is EnemyBuffOption enemyBuff)
        {
            return $"Increases enemy {enemyBuff.targetStat} by {enemyBuff.buffValue}.";
        }
        else if (option is LevelRuleOption rule)
        {
            return $"Rule ({rule.ruleType}): {rule.ruleValue}";
        }
        else if (option is PlayerWeaponOption weapon)
        {
            return $"Equipped weapon: {weapon.title}";
        }
        else if (option is EnemySelectionOption enemy)
        {
            return $"Spawns {enemy.enemyCount}x {enemy.title} in levels.";
        }

        return string.IsNullOrEmpty(option.description) ? "No description available." : option.description;
    }
}