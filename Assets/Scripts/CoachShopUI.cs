using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Put this on a Canvas object. Assign the fields in the Inspector:
//  - listContainer: the Content object of a Scroll View (give it a Vertical Layout Group)
//  - rowPrefab: a prefab with the CoachRow script
public class CoachShopUI : MonoBehaviour
{
    [SerializeField] private Transform listContainer;
    [SerializeField] private CoachRow rowPrefab;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text messageText;
    [SerializeField] private InputField offerInput;   // the player types their offer here
    [SerializeField] private DealPanel dealPanel;     // pop-up for the extra 10M-off ask

    private UserProfile currentProfile;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private readonly HashSet<string> negotiatedIds = new HashSet<string>();   // one try per coach per visit

    // Call this when the player opens the shop, passing the logged-in profile.
    public void Open(UserProfile profile)
    {
        currentProfile = profile;
        negotiatedIds.Clear();
        gameObject.SetActive(true);
        messageText.text = "";
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        foreach (GameObject row in spawnedRows) Destroy(row);
        spawnedRows.Clear();

        moneyText.text = "Money: " + currentProfile.money.ToString("N0");

        foreach (CoachOffer offer in CoachCatalog.Create())
        {
            CoachRow row = Instantiate(rowPrefab, listContainer);
            bool isHired = currentProfile.coachId == offer.id;
            bool canAfford = currentProfile.money >= offer.price;
            row.Setup(offer, isHired, canAfford, Buy, Negotiate);
            spawnedRows.Add(row.gameObject);
        }
    }

    // Calculator-style negotiation: type an offer, press Negotiate, the game rolls 1 or 2.
    // 1 = the coach accepts and you hire him at YOUR offer. 2 = he declines.
    private void Negotiate(CoachOffer offer)
    {
        if (!long.TryParse(offerInput.text, out long amount) || amount <= 0)
        {
            messageText.text = "Type your offer first.";
            return;
        }

        if (amount > offer.price)
        {
            messageText.text = "Your offer is above the asking price. Just press Buy.";
            return;
        }

        if (currentProfile.money < amount)
        {
            messageText.text = "Not enough money for that offer.";
            return;
        }

        if (currentProfile.coachId == offer.id)
        {
            messageText.text = "You already have this coach.";
            return;
        }

        if (negotiatedIds.Contains(offer.id))
        {
            messageText.text = "He won't talk again this visit. Try another coach.";
            return;
        }
        negotiatedIds.Add(offer.id);

        if (Negotiation.Roll(amount, offer.price))
        {
            // Offer accepted. Before paying, the buyer may ask once for a further 10 million off.
            dealPanel.Show(offer.coachName, amount, finalPrice => CompleteHiring(offer, finalPrice));
        }
        else
        {
            messageText.text = offer.coachName + " declined your offer of " + amount.ToString("N0") + ".";
        }
    }

    private void CompleteHiring(CoachOffer offer, long price)
    {
        currentProfile.money -= price;
        currentProfile.coachId = offer.id;
        messageText.text = "Deal! Hired " + offer.coachName + " for " + price.ToString("N0") + ".";

        // TODO: save the profile here with your own save system.
        Refresh();
    }

    private void Buy(CoachOffer offer)
    {
        if (currentProfile.coachId == offer.id)
        {
            messageText.text = "You already have this coach.";
            return;
        }

        if (currentProfile.money < offer.price)
        {
            messageText.text = "Not enough money.";
            return;
        }

        currentProfile.money -= offer.price;
        currentProfile.coachId = offer.id;   // hiring replaces the previous coach
        messageText.text = "Hired " + offer.coachName + "!";

        // TODO: save the profile here with your own save system.
        Refresh();
    }
}
