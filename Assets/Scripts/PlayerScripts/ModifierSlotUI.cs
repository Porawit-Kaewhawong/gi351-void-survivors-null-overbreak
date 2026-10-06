using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class ModifierSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;
    public Image borderImage; // Assign border frame Image in prefab
    public Image backgroundImage; // Optional background tint Image
    public TextMeshProUGUI stackCountText;

    [Header("Animation Settings")]
    public float hoverScaleMultiplier = 1.12f;
    public float animationSpeed = 16f;
    public float popEntranceScale = 0.6f;

    private LobbyOption modifierData;
    private List<LobbyOption> groupItems;
    private int stackCount;
    private ActiveModifiersUI parentUI;

    private Vector3 baseScale;
    private Vector3 targetScale;

    private void Awake()
    {
        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    private void OnEnable()
    {
        // Entrance Pop Effect
        transform.localScale = baseScale * popEntranceScale;
    }

    private void Update()
    {
        // Smooth scale interpolation (unscaledDeltaTime allows UI animation while paused)
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
    }

    public void SetupSlot(LobbyOption option, int count, ActiveModifiersUI uiController, List<LobbyOption> items = null)
    {
        modifierData = option;
        stackCount = count;
        parentUI = uiController;
        groupItems = items;

        Color categoryColor = GetCategoryColor(option);

        // Icon Setup
        if (iconImage != null && option.icon != null)
        {
            iconImage.sprite = option.icon;
            iconImage.enabled = true;
        }

        // Color-Coded Category Border & Background
        if (borderImage != null)
        {
            borderImage.color = categoryColor;
        }

        if (backgroundImage != null)
        {
            // Subtle translucent background tint
            backgroundImage.color = new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.25f);
        }

        // Stack Multiplier Text
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
        targetScale = baseScale * hoverScaleMultiplier;

        if (parentUI != null && modifierData != null)
        {
            Color categoryColor = GetCategoryColor(modifierData);
            string hexColor = ColorUtility.ToHtmlStringRGB(categoryColor);

            string title = $"<color=#{hexColor}>{modifierData.title}</color>";
            string description = GetFormattedDescription(modifierData);

            parentUI.ShowTooltip(title, description, stackCount, transform.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = baseScale;

        if (parentUI != null)
        {
            parentUI.HideTooltip();
        }
    }

    private Color GetCategoryColor(LobbyOption option)
    {
        if (option is PlayerWeaponOption) return new Color(1f, 0.84f, 0f);       // Gold (#FFD700)
        if (option is PlayerBuffOption) return new Color(0.18f, 0.8f, 0.44f);     // Emerald Green (#2ECC71)
        if (option is EnemySelectionOption) return new Color(0.9f, 0.3f, 0.23f);  // Crimson Red (#E74C3C)
        if (option is EnemyBuffOption) return new Color(0.6f, 0.35f, 0.71f);     // Purple (#9B59B6)
        if (option is LevelRuleOption) return new Color(0.1f, 0.74f, 0.61f);     // Cyan (#1ABC9C)
        return Color.white;
    }

    private string GetFormattedDescription(LobbyOption option)
    {
        if (option is PlayerBuffOption buff)
        {
            float total = groupItems != null ? groupItems.OfType<PlayerBuffOption>().Sum(b => b.buffValue) : buff.buffValue * stackCount;
            return $"Increases <b>{buff.targetStat}</b> by <color=#2ECC71>+{total}%</color> across {stackCount} tier(s).";
        }
        else if (option is EnemyBuffOption enemyBuff)
        {
            float total = groupItems != null ? groupItems.OfType<EnemyBuffOption>().Sum(b => b.buffValue) : enemyBuff.buffValue * stackCount;
            return $"Increases enemy <b>{enemyBuff.targetStat}</b> by <color=#E74C3C>+{total}</color> across {stackCount} tier(s).";
        }
        else if (option is LevelRuleOption rule)
        {
            return $"Rule (<color=#1ABC9C>{rule.ruleType}</color>): <b>{rule.ruleValue}</b>";
        }
        else if (option is PlayerWeaponOption weapon)
        {
            return $"Equipped weapon: <b>{weapon.title}</b> (<color=#FFD700>Lvl {stackCount}</color>)";
        }
        else if (option is EnemySelectionOption enemy)
        {
            int total = groupItems != null ? groupItems.OfType<EnemySelectionOption>().Sum(e => e.enemyCount) : enemy.enemyCount * stackCount;
            return $"Spawns <color=#E74C3C>{total}x</color> <b>{enemy.title}</b> in levels.";
        }

        return string.IsNullOrEmpty(option.description) ? "No description available." : option.description;
    }
}