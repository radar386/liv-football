using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Pack screen: open 1 pack (x1) or 10 packs (x10) for in-game money.
// Assign the texts and the two buttons in the Inspector.
public class PackShopUI : MonoBehaviour
{
    [SerializeField] private Dropdown packDropdown;   // Standard / Premium / Elite
    [SerializeField] private Text moneyText;
    [SerializeField] private Text oddsText;       // shows the price and the chances
    [SerializeField] private Text resultsText;    // the players you pulled
    [SerializeField] private Text messageText;
    [SerializeField] private Button open1Button;  // label: "Open x1"
    [SerializeField] private Button open10Button; // label: "Open x10"
    [SerializeField] private Button openOwned1Button;    // optional: opens 1 pack you own (free)
    [SerializeField] private Button openOwned10Button;   // optional: opens 10 packs you own (free)
    [SerializeField] private Text ownedText;             // optional: "You own: 2 Premium"

    [SerializeField] private float revealDelay = 0.6f;   // seconds between players (pack-opening animation)

    private UserProfile profile;
    private Coroutine revealing;
    private readonly System.Random rng = new System.Random();

    public void Open(UserProfile p)
    {
        profile = p;
        gameObject.SetActive(true);
        resultsText.text = "";
        messageText.text = "";

        List<string> names = new List<string>();
        foreach (PackType t in PackEngine.Types) names.Add(t.name);
        packDropdown.ClearOptions();
        packDropdown.AddOptions(names);
        packDropdown.value = 0;
        packDropdown.onValueChanged.RemoveAllListeners();
        packDropdown.onValueChanged.AddListener(_ => Refresh());

        open1Button.onClick.RemoveAllListeners();
        open1Button.onClick.AddListener(() => OpenPacks(1));
        open10Button.onClick.RemoveAllListeners();
        open10Button.onClick.AddListener(() => OpenPacks(10));
        Wire(openOwned1Button, () => OpenOwned(1));
        Wire(openOwned10Button, () => OpenOwned(10));
        Refresh();
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    public void Close() { gameObject.SetActive(false); }

    private void OpenPacks(int count) { Show(PackEngine.Open(profile, Selected(), count, rng), Selected(), count, false); }

    private void OpenOwned(int count) { Show(PackEngine.OpenOwned(profile, Selected(), count, rng), Selected(), count, true); }

    private void Show(List<PackPull> pulls, PackType type, int count, bool owned)
    {
        if (pulls == null)
        {
            SoundManager.Play("error");
            messageText.text = owned
                ? "You do not own " + count + " " + type.name + (count == 1 ? "" : "s") + "."
                : "You need " + PackEngine.Cost(type, count).ToString("N0") + " for " + type.name + " x" + count + ".";
            return;
        }
        SaveSystem.Save(profile);
        SoundManager.Play("buy");

        long refunds = 0;
        foreach (PackPull pull in pulls) if (pull.duplicate) refunds += pull.refund;
        messageText.text = "Opened " + type.name + " x" + count + (owned ? " from your packs" : " for " + PackEngine.Cost(type, count).ToString("N0")) +
            (refunds > 0 ? ". Duplicates paid back " + refunds.ToString("N0") + "." : ".");

        if (revealing != null) StopCoroutine(revealing);
        revealing = StartCoroutine(Reveal(pulls));
        moneyText.text = "Money: " + profile.money.ToString("N0");
    }

    // Pack-opening animation: shake, then show one player at a time, worst to best, with a sound per tier.
    private IEnumerator Reveal(List<PackPull> pulls)
    {
        List<PackPull> order = new List<PackPull>(pulls);
        order.Sort((a, b) => PackEngine.TierIndex(a.player.rating).CompareTo(PackEngine.TierIndex(b.player.rating)));

        bool fast = GameSettings.Data.fastReveal;
        SetButtons(false);
        resultsText.text = "";
        if (!fast)
        {
            SoundManager.Play("pack_shake");
            resultsText.text = "Opening...";
            yield return new WaitForSeconds(0.9f);
        }

        StringBuilder sb = new StringBuilder();
        foreach (PackPull pull in order)
        {
            sb.Append("<color=" + TierColor(pull.tier) + ">[" + pull.tier + "] " + pull.player.playerName + "  " +
                      pull.player.position + "  " + pull.player.rating + "</color>");
            if (pull.duplicate) sb.Append("  (duplicate, +" + pull.refund.ToString("N0") + ")");
            else sb.Append("  (" + pull.seasons + " season" + (pull.seasons == 1 ? "" : "s") + ")");
            sb.AppendLine();
            resultsText.supportRichText = true;
            resultsText.text = sb.ToString();
            SoundManager.PlayReveal(pull.tier);
            if (!fast) yield return new WaitForSeconds(PackEngine.TierIndex(pull.player.rating) >= 2 ? revealDelay * 1.8f : revealDelay);
        }
        revealing = null;
        Refresh();
    }

    private void SetButtons(bool on)
    {
        open1Button.interactable = on;
        open10Button.interactable = on;
        if (openOwned1Button != null) openOwned1Button.interactable = on;
        if (openOwned10Button != null) openOwned10Button.interactable = on;
    }

    private static string TierColor(string tier)
    {
        switch (tier)
        {
            case "Elite": return "#C77DFF";
            case "Gold": return "#FFC83D";
            case "Silver": return "#C0C0C0";
            default: return "#CD7F32";
        }
    }

    private PackType Selected() { return PackEngine.Types[packDropdown.value]; }

    private void Refresh()
    {
        moneyText.text = "Money: " + profile.money.ToString("N0");
        PackType type = Selected();
        open1Button.interactable = PackEngine.CanAfford(profile, type, 1);
        open10Button.interactable = PackEngine.CanAfford(profile, type, 10);
        int owned = PackEngine.Owned(profile, PackEngine.TypeIndex(type));
        if (openOwned1Button != null) openOwned1Button.interactable = owned >= 1;
        if (openOwned10Button != null) openOwned10Button.interactable = owned >= 10;
        if (ownedText != null)
            ownedText.text = "You own: Standard " + PackEngine.Owned(profile, 0) + "  Premium " + PackEngine.Owned(profile, 1) + "  Elite " + PackEngine.Owned(profile, 2);
        oddsText.text = type.name + ": " + type.price.ToString("N0") + " each (x10 = " + PackEngine.Cost(type, 10).ToString("N0") + ")" +
            "\n" + type.description +
            "\nChances:  Bronze " + type.chances[0] + "%   Silver " + type.chances[1] + "%   Gold " + type.chances[2] + "%   Elite " + type.chances[3] + "%" +
            "\nDuplicates pay back " + (int)(PackEngine.DuplicateRefundShare * 100) + "% of the player's value.";
    }
}
