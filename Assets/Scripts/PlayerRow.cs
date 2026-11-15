using System;
using UnityEngine;
using UnityEngine.UI;

// Attach to the row prefab: one row per player in the market list.
public class PlayerRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text positionText;
    [SerializeField] private Text ratingText;
    [SerializeField] private Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Text buyButtonLabel;
    [SerializeField] private Button negotiateButton;

    public void Setup(Footballer player, bool isOwned, bool canAfford, Action<Footballer> onBuy, Action<Footballer> onNegotiate)
    {
        nameText.text = player.playerName;
        positionText.text = player.position;
        ratingText.text = player.rating.ToString();
        priceText.text = player.marketValue.ToString("N0");

        buyButton.onClick.RemoveAllListeners();

        negotiateButton.onClick.RemoveAllListeners();
        negotiateButton.interactable = !isOwned;
        negotiateButton.onClick.AddListener(() => onNegotiate(player));

        if (isOwned)
        {
            buyButtonLabel.text = "Owned";
            buyButton.interactable = false;
        }
        else
        {
            buyButtonLabel.text = "Buy";
            buyButton.interactable = canAfford;
            buyButton.onClick.AddListener(() => onBuy(player));
        }
    }
}
