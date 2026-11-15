using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Put this on a Canvas object. Assign the fields in the Inspector:
//  - listContainer: the Content object of a Scroll View (with a Vertical Layout Group)
//  - rowPrefab: a prefab with the PlayerRow script
//  - positionDropdown / sortDropdown: two UI Dropdowns above the list
public class PlayerShopUI : MonoBehaviour
{
    [SerializeField] private Transform listContainer;
    [SerializeField] private PlayerRow rowPrefab;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text messageText;
    [SerializeField] private Dropdown positionDropdown;
    [SerializeField] private Dropdown sortDropdown;
    [SerializeField] private InputField offerInput;   // the player types their offer here
    [SerializeField] private DealPanel dealPanel;     // pop-up for the extra 10M-off ask

    private UserProfile currentProfile;
    private List<Footballer> market = new List<Footballer>();
    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private readonly HashSet<string> negotiatedIds = new HashSet<string>();   // one try per player per visit

    // Call when the player opens the market. Pass the logged-in profile and
    // the list of players for sale (the Footballer objects from your CreatePlayer helper).
    public void Open(UserProfile profile, List<Footballer> playersForSale)
    {
        currentProfile = profile;
        market = playersForSale;
        negotiatedIds.Clear();
        BuildFilters();
        gameObject.SetActive(true);
        messageText.text = "";
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void BuildFilters()
    {
        List<string> positions = new List<string> { "All" };
        foreach (Footballer player in market)
        {
            if (!positions.Contains(player.position)) positions.Add(player.position);
        }

        positionDropdown.ClearOptions();
        positionDropdown.AddOptions(positions);
        positionDropdown.onValueChanged.RemoveAllListeners();
        positionDropdown.onValueChanged.AddListener(_ => Refresh());

        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string>
        {
            "Price: low to high",
            "Price: high to low",
            "Rating: high to low"
        });
        sortDropdown.onValueChanged.RemoveAllListeners();
        sortDropdown.onValueChanged.AddListener(_ => Refresh());
    }

    private void Refresh()
    {
        foreach (GameObject row in spawnedRows) Destroy(row);
        spawnedRows.Clear();

        moneyText.text = "Money: " + currentProfile.money.ToString("N0");

        string positionFilter = positionDropdown.options[positionDropdown.value].text;
        List<Footballer> visible = new List<Footballer>();
        foreach (Footballer player in market)
        {
            if (positionFilter == "All" || player.position == positionFilter) visible.Add(player);
        }

        switch (sortDropdown.value)
        {
            case 0: visible.Sort((a, b) => a.marketValue.CompareTo(b.marketValue)); break;
            case 1: visible.Sort((a, b) => b.marketValue.CompareTo(a.marketValue)); break;
            case 2: visible.Sort((a, b) => b.rating.CompareTo(a.rating)); break;
        }

        foreach (Footballer player in visible)
        {
            PlayerRow row = Instantiate(rowPrefab, listContainer);
            bool isOwned = Owns(player.id);
            bool canAfford = currentProfile.money >= player.marketValue;
            row.Setup(player, isOwned, canAfford, Buy, Negotiate);
            spawnedRows.Add(row.gameObject);
        }
    }

    private bool Owns(string playerId)
    {
        foreach (ClubSquadPlayer owned in currentProfile.squad)
        {
            if (owned.id == playerId) return true;
        }
        return false;
    }

    // Calculator-style negotiation: type an offer, press Negotiate, the game rolls 1 or 2.
    // 1 = the player accepts and you sign him at YOUR offer. 2 = he declines.
    private void Negotiate(Footballer player)
    {
        if (!long.TryParse(offerInput.text, out long amount) || amount <= 0)
        {
            messageText.text = "Type your offer first.";
            return;
        }

        if (amount > player.marketValue)
        {
            messageText.text = "Your offer is above the asking price. Just press Buy.";
            return;
        }

        if (currentProfile.money < amount)
        {
            messageText.text = "Not enough money for that offer.";
            return;
        }

        if (Owns(player.id))
        {
            messageText.text = "You already own " + player.playerName + ".";
            return;
        }

        if (negotiatedIds.Contains(player.id))
        {
            messageText.text = "He won't talk to you again this visit. Try another player.";
            return;
        }
        negotiatedIds.Add(player.id);

        if (Negotiation.Roll(amount, player.marketValue))
        {
            // Offer accepted. Before paying, the buyer may ask once for a further 10 million off.
            dealPanel.Show(player.playerName, amount, finalPrice => CompleteSigning(player, finalPrice));
        }
        else
        {
            messageText.text = player.playerName + " declined your offer of " + amount.ToString("N0") + ".";
        }
    }

    private void CompleteSigning(Footballer player, long price)
    {
        currentProfile.money -= price;
        currentProfile.squad.Add(new ClubSquadPlayer
        {
            id = player.id,
            playerName = player.playerName,
            position = player.position,
            rating = player.rating,
            hasRating = player.rating > 0
        });
        messageText.text = "Deal! Signed " + player.playerName + " for " + price.ToString("N0") + ".";

        // TODO: save the profile here with your own save system.
        Refresh();
    }

    private void Buy(Footballer player)
    {
        if (Owns(player.id))
        {
            messageText.text = "You already own " + player.playerName + ".";
            return;
        }

        if (currentProfile.money < player.marketValue)
        {
            messageText.text = "Not enough money.";
            return;
        }

        currentProfile.money -= player.marketValue;
        currentProfile.squad.Add(new ClubSquadPlayer
        {
            id = player.id,
            playerName = player.playerName,
            position = player.position,
            rating = player.rating,
            hasRating = player.rating > 0
        });
        messageText.text = "Signed " + player.playerName + "!";

        // TODO: save the profile here with your own save system.
        Refresh();
    }
}
