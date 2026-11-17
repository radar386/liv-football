using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Sponsor screen (DRAFT). Fields to assign in the Inspector: a Dropdown with the sponsors, a Sign button, two Texts.
public class SponsorShopUI : MonoBehaviour
{
    [SerializeField] private Dropdown sponsorDropdown;
    [SerializeField] private Button signButton;
    [SerializeField] private Text infoText;
    [SerializeField] private Text messageText;

    private UserProfile profile;
    private List<SponsorOffer> offers;

    public void Open(UserProfile p)
    {
        profile = p;
        offers = SponsorCatalog.Create();
        gameObject.SetActive(true);
        messageText.text = "";
        List<string> names = new List<string>();
        foreach (SponsorOffer s in offers) names.Add(s.sponsorName + "  +" + s.income.ToString("N0") + " / season");
        sponsorDropdown.ClearOptions();
        sponsorDropdown.AddOptions(names);
        signButton.onClick.RemoveAllListeners();
        signButton.onClick.AddListener(Sign);
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private void Refresh()
    {
        SponsorOffer cur = SponsorCatalog.Find(profile.sponsorId);
        infoText.text = cur == null ? "No sponsor." : cur.sponsorName + ": " + profile.sponsorSeasonsLeft + " seasons left.";
    }

    private void Sign()
    {
        if (profile.sponsorSeasonsLeft > 0) { messageText.text = "You already have a sponsor until the deal ends."; return; }
        SponsorOffer o = offers[Mathf.Clamp(sponsorDropdown.value, 0, offers.Count - 1)];
        profile.sponsorId = o.id;
        profile.sponsorSeasonsLeft = SponsorCatalog.ContractSeasons;
        SaveSystem.Save(profile);
        messageText.text = "Signed " + o.sponsorName + " for " + SponsorCatalog.ContractSeasons + " seasons.";
        Refresh();
    }
}
