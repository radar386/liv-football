using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Admin screen. Only works for admin users (F1FC and wron fc). Every field is optional except the ones you use.
//  - User dropdown + info text
//  - Money: amount box + Give / Take / Set buttons
//  - Squad: dropdown of the user's players + Remove button
//  - Add player: search box (name or club) + results dropdown + Add button
//  - Coach / Sponsor: dropdown + Set button (free), and "None" to remove
//  - Reset user, Restart league, Reset EVERYTHING (the last two press twice to confirm)
public class AdminUI : MonoBehaviour
{
    [SerializeField] private Dropdown userDropdown;
    [SerializeField] private Text infoText;
    [SerializeField] private Text messageText;

    [SerializeField] private InputField amountInput;
    [SerializeField] private Button giveButton;
    [SerializeField] private Button takeButton;
    [SerializeField] private Button setMoneyButton;

    [SerializeField] private Dropdown squadDropdown;
    [SerializeField] private Button removePlayerButton;

    [SerializeField] private InputField searchInput;
    [SerializeField] private Dropdown searchResultsDropdown;
    [SerializeField] private Button addPlayerButton;

    [SerializeField] private Dropdown packTypeDropdown;   // Standard / Premium / Elite
    [SerializeField] private InputField packCountInput;   // how many packs (default 1)
    [SerializeField] private Button givePacksButton;
    [SerializeField] private Button dropToAllButton;     // optional: weekly pack drop for everyone (2 per week per admin)
    [SerializeField] private Text dropInfoText;          // optional: "Drops left this week: 2"

    [SerializeField] private Dropdown coachDropdown;
    [SerializeField] private Button setCoachButton;
    [SerializeField] private Dropdown sponsorDropdown;
    [SerializeField] private Button setSponsorButton;

    [SerializeField] private Button resetUserButton;
    [SerializeField] private Button restartLeagueButton;
    [SerializeField] private Button resetAllButton;

    private UserProfile admin;
    private List<UserProfile> profiles;
    private UserProfile target;
    private List<Footballer> searchResults = new List<Footballer>();
    private List<CoachOffer> coaches;
    private List<SponsorOffer> sponsors;
    private string pendingConfirm = "";
    private readonly System.Random rng = new System.Random();

    // Returns false (and does nothing) if the user is not an admin.
    public bool Open(UserProfile who)
    {
        if (!AdminTools.IsAdmin(who)) return false;
        admin = who;
        gameObject.SetActive(true);
        profiles = SaveSystem.LoadAll();
        messageText.text = "";
        pendingConfirm = "";

        List<string> users = new List<string>();
        foreach (UserProfile p in profiles) users.Add(p.username);
        userDropdown.ClearOptions();
        userDropdown.AddOptions(users);
        userDropdown.onValueChanged.RemoveAllListeners();
        userDropdown.onValueChanged.AddListener(_ => SelectUser());

        coaches = CoachCatalog.Create();
        List<string> coachNames = new List<string> { "None" };
        foreach (CoachOffer c in coaches) coachNames.Add(c.coachName + " (" + c.club + ")");
        Fill(coachDropdown, coachNames);

        sponsors = SponsorCatalog.Create();
        List<string> sponsorNames = new List<string> { "None" };
        foreach (SponsorOffer s in sponsors) sponsorNames.Add(s.sponsorName);
        Fill(sponsorDropdown, sponsorNames);

        Wire(giveButton, () => Money(+1));
        Wire(takeButton, () => Money(-1));
        Wire(setMoneyButton, SetMoney);
        Wire(removePlayerButton, RemovePlayer);
        Wire(addPlayerButton, AddPlayer);
        List<string> packNames = new List<string>();
        foreach (PackType t in PackEngine.Types) packNames.Add(t.name);
        Fill(packTypeDropdown, packNames);
        Wire(givePacksButton, GivePacks);
        Wire(dropToAllButton, DropToAll);
        ShowDropInfo();
        Wire(setCoachButton, SetCoach);
        Wire(setSponsorButton, SetSponsor);
        Wire(resetUserButton, () => Confirm("user", "Press again to wipe " + target.username + " (money, squad, coach, sponsor).", ResetUser));
        Wire(restartLeagueButton, () => Confirm("league", "Press again to restart the league from season 1.", RestartLeague));
        Wire(resetAllButton, () => Confirm("all", "Press again to ERASE ALL SAVED DATA for every user.", ResetAll));
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveAllListeners();
            searchInput.onValueChanged.AddListener(_ => Search());
        }

        SelectUser();
        return true;
    }

    public void Close() { gameObject.SetActive(false); }

    private static void Wire(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    private static void Fill(Dropdown d, List<string> items)
    {
        if (d == null) return;
        d.ClearOptions();
        d.AddOptions(items);
        d.value = 0;
    }

    // Dangerous buttons need two presses in a row.
    private void Confirm(string key, string prompt, System.Action action)
    {
        if (pendingConfirm == key) { pendingConfirm = ""; action(); return; }
        pendingConfirm = key;
        messageText.text = prompt;
    }

    private void SelectUser()
    {
        target = profiles[userDropdown.value];
        pendingConfirm = "";
        RefreshSquad();
        Refresh();
    }

    private void Refresh()
    {
        CoachOffer coach = CoachCatalog.Find(target.coachId);
        SponsorOffer sponsor = SponsorCatalog.Find(target.sponsorId);
        infoText.text = target.clubName + " (" + target.username + ")" + (target.isAdmin ? "  [admin]" : "") +
            "\nMoney: " + target.money.ToString("N0") +
            "\nSquad: " + target.squad.Count + " players   Season " + target.season +
            "\nUnopened packs: Standard " + PackEngine.Owned(target, 0) + ", Premium " + PackEngine.Owned(target, 1) + ", Elite " + PackEngine.Owned(target, 2) +
            "\nCoach: " + (coach != null ? coach.coachName : "none") +
            "\nSponsor: " + (sponsor != null ? sponsor.sponsorName + " (" + target.sponsorSeasonsLeft + " seasons left)" : "none");
    }

    private void RefreshSquad()
    {
        if (squadDropdown == null) return;
        List<string> names = new List<string>();
        foreach (ClubSquadPlayer c in target.squad) names.Add(c.playerName + "  " + c.position + "  " + (c.hasRating ? c.rating.ToString() : "?"));
        if (names.Count == 0) names.Add("(no players)");
        Fill(squadDropdown, names);
    }

    private void Saved(string message)
    {
        SaveSystem.Save(target);
        messageText.text = message;
        RefreshSquad();
        Refresh();
    }

    private bool ReadAmount(out long amount)
    {
        amount = 0;
        string text = amountInput == null ? "" : amountInput.text.Replace(",", "").Replace(" ", "");
        if (long.TryParse(text, out amount) && amount >= 0) return true;
        messageText.text = "Type an amount first (a whole number, 0 or more).";
        return false;
    }

    private void Money(int sign)
    {
        long amount;
        if (!ReadAmount(out amount)) return;
        long now = AdminTools.ChangeMoney(target, sign * amount);
        Saved((sign > 0 ? "Gave " : "Took ") + amount.ToString("N0") + ". " + target.username + " now has " + now.ToString("N0") + ".");
    }

    private void SetMoney()
    {
        long amount;
        if (!ReadAmount(out amount)) return;
        AdminTools.SetMoney(target, amount);
        Saved(target.username + " money set to " + amount.ToString("N0") + ".");
    }

    private void RemovePlayer()
    {
        if (target.squad.Count == 0) { messageText.text = "No players to remove."; return; }
        ClubSquadPlayer c = target.squad[Mathf.Max(0, squadDropdown.value)];
        string name = c.playerName;
        AdminTools.RemovePlayer(target, c.id);
        Saved("Removed " + name + ".");
    }

    private void Search()
    {
        searchResults = AdminTools.SearchPlayers(searchInput.text, 30);
        List<string> names = new List<string>();
        foreach (Footballer f in searchResults) names.Add(f.playerName + "  " + f.position + "  " + f.rating + "  (" + PlayerCatalog.ClubOf(f.id) + ")");
        if (names.Count == 0) names.Add("(no matches)");
        Fill(searchResultsDropdown, names);
    }

    private void AddPlayer()
    {
        if (searchResults.Count == 0) { messageText.text = "Search for a player first."; return; }
        Footballer f = searchResults[Mathf.Max(0, searchResultsDropdown.value)];
        Saved(AdminTools.AddPlayer(target, f, rng) ? "Added " + f.playerName + " to " + target.username + "." : target.username + " already has " + f.playerName + ".");
    }

    private void GivePacks()
    {
        int count = 1;
        if (packCountInput != null && !string.IsNullOrEmpty(packCountInput.text) && !int.TryParse(packCountInput.text.Trim(), out count)) count = 0;
        if (count < 1 || count > 1000) { messageText.text = "Type how many packs (1 to 1000)."; return; }
        int type = packTypeDropdown.value;
        AdminTools.GivePacks(target, type, count);
        Saved("Gave " + count + " x " + PackEngine.Types[type].name + " to " + target.username + ". They now own " + PackEngine.Owned(target, type) + ".");
    }

    private void ShowDropInfo()
    {
        if (dropInfoText != null)
            dropInfoText.text = "Pack drops for everyone left this week: " + AdminTools.DropsLeft(admin, System.DateTime.Now) + " of " + AdminTools.DropsPerWeek;
    }

    private void DropToAll()
    {
        int count = 1;
        if (packCountInput != null && !string.IsNullOrEmpty(packCountInput.text) && !int.TryParse(packCountInput.text.Trim(), out count)) count = 0;
        if (count < 1 || count > 1000) { messageText.text = "Type how many packs (1 to 1000)."; return; }
        int type = packTypeDropdown.value;
        if (!AdminTools.WeeklyDrop(admin, profiles, type, count, System.DateTime.Now))
        {
            messageText.text = "You already did " + AdminTools.DropsPerWeek + " pack drops this week. The week restarts on Monday.";
            ShowDropInfo();
            return;
        }
        foreach (UserProfile p in profiles) SaveSystem.Save(p);
        SaveSystem.Save(admin);
        messageText.text = "Dropped " + count + " x " + PackEngine.Types[type].name + " to all " + profiles.Count + " users.";
        ShowDropInfo();
        Refresh();
    }

    private void SetCoach()
    {
        int i = coachDropdown.value;
        AdminTools.SetCoach(target, i == 0 ? null : coaches[i - 1]);
        Saved(i == 0 ? "Coach removed." : "Coach set to " + coaches[i - 1].coachName + ".");
    }

    private void SetSponsor()
    {
        int i = sponsorDropdown.value;
        AdminTools.SetSponsor(target, i == 0 ? null : sponsors[i - 1]);
        Saved(i == 0 ? "Sponsor removed." : "Sponsor set to " + sponsors[i - 1].sponsorName + ".");
    }

    private void ResetUser()
    {
        AdminTools.ResetProfile(target);
        Saved(target.username + " was reset.");
    }

    private void RestartLeague()
    {
        AdminTools.RestartLeague(profiles);
        messageText.text = "League restarted from season 1.";
    }

    private void ResetAll()
    {
        SaveSystem.ResetAll();
        profiles = SaveSystem.LoadAll();
        admin = SaveSystem.Find(admin.username) ?? admin;
        messageText.text = "Everything was reset to the starting profiles.";
        userDropdown.value = 0;
        SelectUser();
    }
}
