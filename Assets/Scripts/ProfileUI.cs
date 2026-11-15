using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// User profile screen. Pick a user in the dropdown to see their club, then open every other screen from here.
// All the panel references are optional: leave any empty and its button does nothing.
public class ProfileUI : MonoBehaviour
{
    [SerializeField] private Dropdown userDropdown;
    [SerializeField] private Image crestImage;          // crest sprites in Assets/Resources/Crests/ named like crestId
    [SerializeField] private Text clubText;
    [SerializeField] private Text infoText;             // money, season, ovr, coach, sponsor, squad size

    [SerializeField] private Button coachShopButton;
    [SerializeField] private Button playerShopButton;
    [SerializeField] private Button sponsorShopButton;
    [SerializeField] private Button academyButton;
    [SerializeField] private Button teamBuilderButton;
    [SerializeField] private Button matchButton;
    [SerializeField] private Button leagueButton;
    [SerializeField] private Button packsButton;
    [SerializeField] private Button adminButton;       // only shown for admin users
    [SerializeField] private Button transfersButton;
    [SerializeField] private Button rewardsButton;
    [SerializeField] private Button facilitiesButton;
    [SerializeField] private Button namesButton;       // toggles real names / fictional names
    [SerializeField] private Text namesButtonLabel;

    [SerializeField] private CoachShopUI coachShop;
    [SerializeField] private PlayerShopUI playerShop;
    [SerializeField] private SponsorShopUI sponsorShop;
    [SerializeField] private AcademyUI academy;
    [SerializeField] private TeamBuilderUI teamBuilder;
    [SerializeField] private MatchSimUI matchScreen;
    [SerializeField] private LeagueUI leagueScreen;
    [SerializeField] private PackShopUI packShop;
    [SerializeField] private AdminUI adminScreen;
    [SerializeField] private TransferMarketUI transferScreen;
    [SerializeField] private RewardsUI rewardsScreen;
    [SerializeField] private FacilitiesUI facilitiesScreen;

    private List<UserProfile> profiles;
    private UserProfile current;

    public UserProfile Current { get { return current; } }

    private void Start() { Open(); }

    public void Open()
    {
        gameObject.SetActive(true);
        profiles = SaveSystem.LoadAll();

        List<string> labels = new List<string>();
        foreach (UserProfile p in profiles) labels.Add(p.username + (p.isAdmin ? " (admin)" : ""));
        userDropdown.ClearOptions();
        userDropdown.AddOptions(labels);
        userDropdown.onValueChanged.RemoveAllListeners();
        userDropdown.onValueChanged.AddListener(Select);

        Wire(coachShopButton, () => { if (coachShop != null) coachShop.Open(current); });
        Wire(playerShopButton, () => { if (playerShop != null) playerShop.Open(current, PlayerCatalog.Create()); });
        Wire(sponsorShopButton, () => { if (sponsorShop != null) sponsorShop.Open(current); });
        Wire(academyButton, () => { if (academy != null) academy.Open(current); });
        Wire(teamBuilderButton, () => { if (teamBuilder != null) teamBuilder.Open(current); });
        Wire(matchButton, () => { if (matchScreen != null) matchScreen.Open(current); });

        Wire(namesButton, () =>
        {
            FictionalNames.SetEnabled(!FictionalNames.Enabled);
            foreach (UserProfile p in profiles)
            {
                FictionalNames.SyncProfileNames(p);   // players already in squads get the other set of names
                SaveSystem.Save(p);
            }
            Refresh();
        });
        Wire(rewardsButton, () => { if (rewardsScreen != null && current != null) rewardsScreen.Open(current); });
        Wire(facilitiesButton, () => { if (facilitiesScreen != null && current != null) facilitiesScreen.Open(current); });
        Wire(transfersButton, () => { if (transferScreen != null && current != null) transferScreen.Open(current); });
        Wire(adminButton, () => { if (adminScreen != null && current != null) adminScreen.Open(current); });
        Wire(packsButton, () => { if (packShop != null) packShop.Open(current); });
        Wire(leagueButton, () => { if (leagueScreen != null) leagueScreen.Open(); });

        if (profiles.Count > 0) Select(0);
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    private void Select(int index)
    {
        current = profiles[index];
        Refresh();
    }

    // Call again after anything changes (for example when a shop closes) to update the numbers.
    public void Refresh()
    {
        if (namesButtonLabel != null) namesButtonLabel.text = FictionalNames.Enabled ? "Names: Fictional" : "Names: Real";
        if (current == null) return;
        if (adminButton != null) adminButton.gameObject.SetActive(current.isAdmin);

        clubText.text = current.clubName + "\n(" + current.username + ")";
        if (crestImage != null)
        {
            Sprite crest = Resources.Load<Sprite>("Crests/" + current.crestId);
            crestImage.sprite = crest;
            crestImage.enabled = crest != null;
        }

        CoachOffer coach = CoachCatalog.Find(current.coachId);
        SponsorOffer sponsor = SponsorCatalog.Find(current.sponsorId);
        string formation = !string.IsNullOrEmpty(current.formationChoice) ? current.formationChoice
            : (coach != null ? coach.formation : "4-4-2");

        infoText.text =
            "Money: " + current.money.ToString("N0") +
            "\nSeason: " + current.season +
            "\nTeam ovr: " + AcademyScout.UserOvr(current) +
            "\nSquad: " + current.squad.Count + " players" + (Availability.UnavailableCount(current) > 0 ? " (" + Availability.UnavailableCount(current) + " injured or suspended)" : "") +
            "\nFormation: " + formation + (current.lineupIds != null && current.lineupIds.Count == 11 ? " (your lineup)" : " (automatic)") +
            "\nCoach: " + (coach != null ? coach.coachName + " (rating " + coach.rating + ", +" + TeamBuilder.CoachBoost(coach.rating) + " to every player)" : "none") +
            "\nSponsor: " + (sponsor != null ? sponsor.sponsorName + " (+" + sponsor.income.ToString("N0") + " / season, " + current.sponsorSeasonsLeft + " seasons left)" : "none") +
            "\nFacilities: stadium " + current.stadiumLevel + ", academy " + current.academyLevel + ", training " + current.trainingLevel +
            "\nTactic: " + (string.IsNullOrEmpty(current.tactic) ? "Balanced" : current.tactic) + "   Daily streak: " + current.loginStreak +
            "\nAcademy scouts left: " + AcademyScout.Remaining(current) + " / " + AcademyScout.MaxPerSeason;
    }
}
