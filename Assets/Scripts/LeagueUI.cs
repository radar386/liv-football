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
        }
        newsText.text = news;
        foreach (UserProfile p in profiles) SaveSystem.Save(p);
        SaveSystem.SaveLeague(state);
        Refresh();
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
