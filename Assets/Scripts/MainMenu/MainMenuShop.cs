using UnityEngine;
using TMPro;

public class MainMenuShop : MonoBehaviour
{
    [Header("UI Text Display")]
    public TextMeshProUGUI totalCoinsText;
    public TextMeshProUGUI hpUpgradeText;
    public TextMeshProUGUI speedUpgradeText;
    public TextMeshProUGUI radiusUpgradeText;

    [Header("Base Upgrade Costs")]
    public int baseHpCost = 50;
    public int baseSpeedCost = 50;
    public int baseRadiusCost = 50;

    private void Start()
    {
        UpdateShopUI();
    }

    public void UpdateShopUI()
    {
        int totalCoins = PlayerPrefs.GetInt("TotalPermanentCoins", 0);
        int hpLvl = PlayerPrefs.GetInt("Upgrade_MaxHP_Level", 0);
        int speedLvl = PlayerPrefs.GetInt("Upgrade_MoveSpeed_Level", 0);
        int radiusLvl = PlayerPrefs.GetInt("Upgrade_PickupRadius_Level", 0);

        if (totalCoinsText != null)
            totalCoinsText.text = $"Coins: {totalCoins}";

        if (hpUpgradeText != null)
            hpUpgradeText.text = $"Max HP LVL {hpLvl} (+{hpLvl * 10}%)\nCost: {baseHpCost * (hpLvl + 1)} Coins";

        if (speedUpgradeText != null)
            speedUpgradeText.text = $"Move Speed LVL {speedLvl} (+{speedLvl * 5}%)\nCost: {baseSpeedCost * (speedLvl + 1)} Coins";

        if (radiusUpgradeText != null)
            radiusUpgradeText.text = $"Pickup Radius LVL {radiusLvl} (+{radiusLvl * 15}%)\nCost: {baseRadiusCost * (radiusLvl + 1)} Coins";
    }

    public void BuyHpUpgrade()
    {
        int hpLvl = PlayerPrefs.GetInt("Upgrade_MaxHP_Level", 0);
        int cost = baseHpCost * (hpLvl + 1);
        TryPurchaseUpgrade("Upgrade_MaxHP_Level", hpLvl, cost);
    }

    public void BuySpeedUpgrade()
    {
        int speedLvl = PlayerPrefs.GetInt("Upgrade_MoveSpeed_Level", 0);
        int cost = baseSpeedCost * (speedLvl + 1);
        TryPurchaseUpgrade("Upgrade_MoveSpeed_Level", speedLvl, cost);
    }

    public void BuyRadiusUpgrade()
    {
        int radiusLvl = PlayerPrefs.GetInt("Upgrade_PickupRadius_Level", 0);
        int cost = baseRadiusCost * (radiusLvl + 1);
        TryPurchaseUpgrade("Upgrade_PickupRadius_Level", radiusLvl, cost);
    }

    private void TryPurchaseUpgrade(string preferenceKey, int currentLevel, int cost)
    {
        int totalCoins = PlayerPrefs.GetInt("TotalPermanentCoins", 0);

        if (totalCoins >= cost)
        {
            PlayerPrefs.SetInt("TotalPermanentCoins", totalCoins - cost);
            PlayerPrefs.SetInt(preferenceKey, currentLevel + 1);
            PlayerPrefs.Save();
            UpdateShopUI();
        }
        else
        {
            Debug.Log("[Shop] Not enough coins!");
        }
    }
}