using UnityEngine;
using UnityEngine.UI;

// "Buy money" screen. Assign one button + label per bundle (same order as MoneyShop.Bundles).
// Set useTestPayments ON only while testing; a shipped game needs a real payment provider.
public class MoneyShopUI : MonoBehaviour
{
    [SerializeField] private Button[] bundleButtons;   // one per bundle
    [SerializeField] private Text[] bundleLabels;      // e.g. "500,000  -  $0.99"
    [SerializeField] private Text moneyText;
    [SerializeField] private Text messageText;
    [SerializeField] private bool useTestPayments = true;

    private UserProfile profile;
    private IPaymentProvider provider;

    // Call this with your real provider before Open() when you have one.
    public void SetProvider(IPaymentProvider p) { provider = p; }

    public void Open(UserProfile p)
    {
        profile = p;
        gameObject.SetActive(true);
        messageText.text = "";
        if (provider == null) provider = useTestPayments ? new FakePaymentProvider() : null;

        for (int i = 0; i < bundleButtons.Length && i < MoneyShop.Bundles.Length; i++)
        {
            MoneyBundle bundle = MoneyShop.Bundles[i];
            if (i < bundleLabels.Length) bundleLabels[i].text = bundle.label + "  -  $" + bundle.priceUsd.ToString("0.00");
            bundleButtons[i].onClick.RemoveAllListeners();
            bundleButtons[i].onClick.AddListener(() => Buy(bundle));
        }
        Refresh();
    }

    public void Close() { SoundManager.Play("back"); gameObject.SetActive(false); }

    private void Buy(MoneyBundle bundle)
    {
        if (provider == null) { messageText.text = "Payments are not set up yet."; SoundManager.Play("error"); return; }
        messageText.text = "Waiting for payment...";
        provider.Purchase(bundle, profile, result =>
        {
            if (result.success && MoneyShop.Credit(profile, bundle, result.transactionId))
            {
                SaveSystem.Save(profile);
                SoundManager.Play("reward");
                messageText.text = "Added " + bundle.money.ToString("N0") + " to your club.";
            }
            else
            {
                SoundManager.Play("error");
                messageText.text = "Payment failed" + (string.IsNullOrEmpty(result.error) ? "." : ": " + result.error);
            }
            Refresh();
        });
    }

    private void Refresh() { moneyText.text = "Money: " + profile.money.ToString("N0"); }
}
