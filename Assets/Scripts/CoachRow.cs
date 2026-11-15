using System;
using UnityEngine;
using UnityEngine.UI;

// Attach to the row prefab: one row per coach in the shop list.
public class CoachRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text formationText;
    [SerializeField] private Text ratingText;
    [SerializeField] private Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Text buyButtonLabel;
    [SerializeField] private Button negotiateButton;

    public void Setup(CoachOffer offer, bool isHired, bool canAfford, Action<CoachOffer> onBuy, Action<CoachOffer> onNegotiate)
    {
        nameText.text = offer.coachName;
        formationText.text = offer.club + "  |  " + offer.formation;   // e.g. "Arsenal  |  4-3-3"
        ratingText.text = offer.rating.ToString();
        priceText.text = offer.price.ToString("N0");

        buyButton.onClick.RemoveAllListeners();

        negotiateButton.onClick.RemoveAllListeners();
        negotiateButton.interactable = !isHired;
        negotiateButton.onClick.AddListener(() => onNegotiate(offer));

        if (isHired)
        {
            buyButtonLabel.text = "Hired";
            buyButton.interactable = false;
        }
        else
        {
            buyButtonLabel.text = "Buy";
            buyButton.interactable = canAfford;
            buyButton.onClick.AddListener(() => onBuy(offer));
        }
    }
}
