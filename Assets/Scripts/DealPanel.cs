using System;
using UnityEngine;
using UnityEngine.UI;

// A small pop-up shown after the player (or coach) ACCEPTS the buyer's offer.
// The buyer can ask once for a further 10 million off, or just confirm the deal.
// Put this on a Panel object (inactive by default) inside each shop canvas.
public class DealPanel : MonoBehaviour
{
    public const long DiscountAsk = 10_000_000;   // how much extra the buyer asks to take off

    [SerializeField] private Text titleText;
    [SerializeField] private Text resultText;
    [SerializeField] private Button askDiscountButton;   // label it: "Ask for 10M off"
    [SerializeField] private Button confirmButton;       // label it: "Confirm deal"

    private long currentPrice;
    private Action<long> onFinish;

    public void Show(string subjectName, long agreedPrice, Action<long> finish)
    {
        currentPrice = agreedPrice;
        onFinish = finish;

        gameObject.SetActive(true);
        titleText.text = subjectName + " accepted your offer of " + agreedPrice.ToString("N0") + ".";
        resultText.text = "";

        // Only allow the ask if 10M off is still a realistic price (same odds rule as the first round).
        long reduced = agreedPrice - DiscountAsk;
        askDiscountButton.interactable = reduced > 0 && Negotiation.GetChance(reduced, agreedPrice) > 0f;

        askDiscountButton.onClick.RemoveAllListeners();
        askDiscountButton.onClick.AddListener(AskDiscount);

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(Confirm);
    }

    private void AskDiscount()
    {
        askDiscountButton.interactable = false;   // one ask only

        long reduced = currentPrice - DiscountAsk;
        if (Negotiation.Roll(reduced, currentPrice))
        {
            currentPrice = reduced;
            resultText.text = "He accepted! 10 million off. New price: " + currentPrice.ToString("N0");
        }
        else
        {
            // Assumption: a refused ask does NOT cancel the deal; the price just stays.
            resultText.text = "He declined. The price stays at " + currentPrice.ToString("N0");
        }
    }

    private void Confirm()
    {
        gameObject.SetActive(false);
        onFinish?.Invoke(currentPrice);
    }
}
