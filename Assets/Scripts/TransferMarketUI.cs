using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Transfers screen (DRAFT): sell a squad player back to the market for 30% of his value.
public class TransferMarketUI : MonoBehaviour
{
    public const float SellRatio = 0.3f;

    [SerializeField] private Dropdown squadDropdown;
    [SerializeField] private Button sellButton;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text messageText;

    private UserProfile profile;

    public void Open(UserProfile p)
    {
        profile = p;
        gameObject.SetActive(true);
        messageText.text = "";
        sellButton.onClick.RemoveAllListeners();
        sellButton.onClick.AddListener(Sell);
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private long PriceOf(ClubSquadPlayer c) { return (long)(PlayerCatalog.ValueOf(c.rating) * SellRatio); }

    private void Refresh()
    {
        moneyText.text = "Money: " + profile.money.ToString("N0");
        List<string> names = new List<string>();
        foreach (ClubSquadPlayer c in profile.squad) names.Add(c.playerName + "  " + c.position + "  " + c.rating + "  +" + PriceOf(c).ToString("N0"));
        squadDropdown.ClearOptions();
        squadDropdown.AddOptions(names);
        sellButton.interactable = profile.squad.Count > 11;   // keep at least a starting eleven
    }

    private void Sell()
    {
        if (profile.squad.Count <= 11) { messageText.text = "You need at least 11 players."; return; }
        int i = Mathf.Clamp(squadDropdown.value, 0, profile.squad.Count - 1);
        ClubSquadPlayer c = profile.squad[i];
        profile.money += PriceOf(c);
        profile.squad.RemoveAt(i);
        profile.lineupIds.Remove(c.id);
        profile.statuses.RemoveAll(s => s.playerId == c.id);
        profile.academyContracts.RemoveAll(a => a.playerId == c.id);
        profile.potentials.RemoveAll(x => x.playerId == c.id);
        SaveSystem.Save(profile);
        messageText.text = "Sold " + c.playerName + " for " + PriceOf(c).ToString("N0") + ".";
        Refresh();
    }
}
