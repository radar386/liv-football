using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// League screen (DRAFT): table, play next matchday, pay prizes, start next season.
public class LeagueUI : MonoBehaviour
{
    [SerializeField] private Text tableText;
    [SerializeField] private Text newsText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button nextSeasonButton;
    [SerializeField] private Button finalsButton;        // optional: opens the Cup final and Super Cup
    [SerializeField] private FinalsMenuUI finalsMenu;    // optional
    private CupState cup;

    private LeagueState state;
    private List<UserProfile> profiles;
    private readonly System.Random rng = new System.Random();

    public void Open()
    {
        profiles = SaveSystem.LoadAll();
        state = SaveSystem.LoadLeague();
        if (state == null) { state = LeagueEngine.Create(profiles, 1); SaveSystem.SaveLeague(state); }
        gameObject.SetActive(true);
        playButton.onClick.RemoveAllListeners();
        playButton.onClick.AddListener(PlayNext);
        nextSeasonButton.onClick.RemoveAllListeners();
        nextSeasonButton.onClick.AddListener(NextSeason);
        newsText.text = "";
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private void Refresh()
    {
        bool done = LeagueEngine.IsFinished(state);
        playButton.interactable = !done;
        nextSeasonButton.interactable = done && state.prizesPaid;
        string t = "Season " + state.season + "  matchday " + Mathf.Min(state.nextMatchday + 1, LeagueEngine.TotalMatchdays(state)) + "/" + LeagueEngine.TotalMatchdays(state) + "\n";
        int pos = 1;
        foreach (TableRow r in LeagueEngine.Table(state, profiles))
            t += (pos++) + ". " + r.club + "  P" + r.played + "  " + r.won + "-" + r.drawn + "-" + r.lost + "  GD " + r.GoalDifference + "  " + r.points + " pts\n";
        tableText.text = t;
    }

    private void PlayNext()
    {
        LeagueEngine.PlayMatchday(state, profiles, rng);
        string news = string.Join("\n", LeagueEngine.LastNews.ToArray());
        if (LeagueEngine.IsFinished(state) && !state.prizesPaid)
        {
            news += "\n" + string.Join("\n", LeagueEngine.PayPrizes(state, profiles).ToArray());
            state.prizesPaid = true;
            news += "\n" + string.Join("\n", PlayFinals().ToArray());
        }
        newsText.text = news;
        foreach (UserProfile p in profiles) SaveSystem.Save(p);
        SaveSystem.SaveLeague(state);
        Refresh();
    }

    // The Cup and the Super Cup are played when the league ends; prizes are paid with the rest.
    private System.Collections.Generic.List<string> PlayFinals()
    {
        cup = CupEngine.Create(profiles, state.season, rng);
        CupEngine.PlayAll(cup, profiles, rng);
        System.Collections.Generic.List<TableRow> table = LeagueEngine.Table(state, profiles);
        string champ = table.Count > 0 ? FindUsername(table[0].club) : null;
        string second = table.Count > 1 ? FindUsername(table[1].club) : null;
        CupEngine.PlaySuperCup(cup, champ, second, profiles, rng);
        System.Collections.Generic.List<string> lines = CupEngine.PayPrizes(cup, profiles);
        SaveSystem.SaveCup(cup);
        if (finalsButton != null)
        {
            finalsButton.gameObject.SetActive(true);
            finalsButton.onClick.RemoveAllListeners();
            finalsButton.onClick.AddListener(() => { if (finalsMenu != null) finalsMenu.Open(cup, profiles); });
        }
        return lines;
    }

    private string FindUsername(string club)
    {
        foreach (UserProfile p in profiles) if (p.clubName == club) return p.username;
        return null;
    }

    private void NextSeason()
    {
        state = LeagueEngine.StartNextSeason(state, profiles);
        foreach (UserProfile p in profiles) SaveSystem.Save(p);
        SaveSystem.SaveLeague(state);
        newsText.text = "Season " + state.season + " begins.";
        Refresh();
    }
}
