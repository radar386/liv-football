using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Team building screen: choose a formation and your starting 11.
// Assign in the Inspector:
//  - listContainer + rowPrefab (LineupRow): your squad list (Scroll View content)
//  - formationDropdown, countText, messageText, saveButton, autoButton
public class TeamBuilderUI : MonoBehaviour
{
    [SerializeField] private Transform listContainer;
    [SerializeField] private LineupRow rowPrefab;
    [SerializeField] private Text headerPrefab;   // optional: a Text prefab used as the heading for each position group
    [SerializeField] private Dropdown formationDropdown;
    [SerializeField] private Dropdown tacticDropdown;     // optional: Attacking / Balanced / Defensive
    [SerializeField] private Text countText;      // "Starting 11/11 | GK 1/1  DEF 4/4  MID 3/3  FWD 3/3"
    [SerializeField] private Text messageText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button autoButton;

    public static readonly string[] Tactics = { "Attacking", "Balanced", "Defensive" };

    private UserProfile profile;
    private readonly List<string> selected = new List<string>();
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    public void Open(UserProfile p)
    {
        profile = p;
        gameObject.SetActive(true);
        messageText.text = "";

        formationDropdown.ClearOptions();
        formationDropdown.AddOptions(new List<string>(TeamBuilder.Formations));
        string current = !string.IsNullOrEmpty(p.formationChoice) ? p.formationChoice : DefaultFormation(p);
        int index = System.Array.IndexOf(TeamBuilder.Formations, current);
        formationDropdown.value = index >= 0 ? index : 0;
        formationDropdown.onValueChanged.RemoveAllListeners();
        formationDropdown.onValueChanged.AddListener(_ => Refresh());

        if (tacticDropdown != null)
        {
            tacticDropdown.ClearOptions();
            tacticDropdown.AddOptions(new List<string>(Tactics));
            int ti = System.Array.IndexOf(Tactics, string.IsNullOrEmpty(p.tactic) ? "Balanced" : p.tactic);
            tacticDropdown.value = ti >= 0 ? ti : 1;
        }

        selected.Clear();
        if (p.lineupIds != null && p.lineupIds.Count > 0) selected.AddRange(p.lineupIds);
        else selected.AddRange(TeamBuilder.AutoLineup(p, Formation()));

        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(OnSave);
        autoButton.onClick.RemoveAllListeners();
        autoButton.onClick.AddListener(OnAuto);
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private static string DefaultFormation(UserProfile p)
    {
        CoachOffer coach = string.IsNullOrEmpty(p.coachId) ? null : CoachCatalog.Find(p.coachId);
        return coach != null && !string.IsNullOrEmpty(coach.formation) ? coach.formation : "4-4-2";
    }

    private string Formation() { return TeamBuilder.Formations[formationDropdown.value]; }

    private void OnAuto()
    {
        selected.Clear();
        selected.AddRange(TeamBuilder.AutoLineup(profile, Formation()));
        messageText.text = "Best XI picked for " + Formation() + ".";
        Refresh();
    }

    private void OnSave()
    {
        string problem = TeamBuilder.LineupProblem(profile, selected, Formation()) ?? TeamBuilder.AvailabilityProblem(profile, selected);
        if (problem != null) { messageText.text = problem + " Bench him or pick a fit player."; return; }
        profile.formationChoice = Formation();
        profile.lineupIds = new List<string>(selected);
        if (tacticDropdown != null) profile.tactic = Tactics[tacticDropdown.value];
        SaveSystem.Save(profile);
        messageText.text = "Lineup saved: " + Formation() + ".";
    }

    private void Toggle(ClubSquadPlayer player)
    {
        if (!selected.Remove(player.id))
        {
            if (!Availability.IsAvailable(profile, player.id)) { messageText.text = player.playerName + " is " + Availability.Reason(profile, player.id).ToLower() + " and cannot start."; return; }
            if (selected.Count >= 11) { messageText.text = "Already 11 starters. Bench someone first."; return; }
            selected.Add(player.id);
        }
        messageText.text = "";
        Refresh();
    }

    private void Refresh()
    {
        foreach (GameObject row in spawnedRows) Destroy(row);
        spawnedRows.Clear();

        // Organised by position: goalkeepers, defenders, midfielders, forwards. Best rated first in each position.
        List<ClubSquadPlayer> sorted = new List<ClubSquadPlayer>(profile.squad);
        sorted.Sort((a, b) =>
        {
            int pa = PositionOrder(a.position), pb = PositionOrder(b.position);
            if (pa != pb) return pa.CompareTo(pb);
            return (b.hasRating ? b.rating : 0).CompareTo(a.hasRating ? a.rating : 0);
        });

        string lastGroup = "";
        foreach (ClubSquadPlayer c in sorted)
        {
            string group = TeamBuilder.Group(c.position);
            if (group != lastGroup && headerPrefab != null)
            {
                Text header = Instantiate(headerPrefab, listContainer);
                header.text = GroupTitle(group) + " (" + CountInGroup(group) + ")";
                spawnedRows.Add(header.gameObject);
            }
            lastGroup = group;

            LineupRow row = Instantiate(rowPrefab, listContainer);
            row.Setup(c, selected.Contains(c.id), Toggle, Availability.Reason(profile, c.id));
            spawnedRows.Add(row.gameObject);
        }

        int d, m, f;
        SimTeam.ParseFormation(Formation(), out d, out m, out f);
        int gk = 0, de = 0, mi = 0, fw = 0;
        foreach (string id in selected)
        {
            ClubSquadPlayer c = TeamBuilder.FindSquadPlayer(profile, id);
            if (c == null) continue;
            string g = TeamBuilder.Group(c.position);
            if (g == "GK") gk++; else if (g == "DEF") de++; else if (g == "MID") mi++; else fw++;
        }
        countText.text = "Starting " + selected.Count + "/11  |  GK " + gk + "/1  DEF " + de + "/" + d + "  MID " + mi + "/" + m + "  FWD " + fw + "/" + f;
    }

    private static readonly string[] PositionList = { "GK", "CB", "LB", "RB", "CDM", "CM", "CAM", "LW", "RW", "ST" };

    private static int PositionOrder(string pos)
    {
        int i = System.Array.IndexOf(PositionList, pos);
        return i >= 0 ? i : PositionList.Length;
    }

    private static string GroupTitle(string group)
    {
        switch (group)
        {
            case "GK": return "GOALKEEPERS";
            case "DEF": return "DEFENDERS";
            case "MID": return "MIDFIELDERS";
            default: return "FORWARDS";
        }
    }

    private int CountInGroup(string group)
    {
        int n = 0;
        foreach (ClubSquadPlayer c in profile.squad) if (TeamBuilder.Group(c.position) == group) n++;
        return n;
    }
}
