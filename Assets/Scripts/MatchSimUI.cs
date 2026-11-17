using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Friendly match screen (DRAFT): your club against a catalog club, shows the score and key events.
public class MatchSimUI : MonoBehaviour
{
    [SerializeField] private Dropdown opponentDropdown;
    [SerializeField] private Button playButton;
    [SerializeField] private Text resultText;

    private UserProfile profile;
    private List<string> clubs;

    public void Open(UserProfile p)
    {
        profile = p;
        clubs = TeamBuilder.CatalogClubs();
        gameObject.SetActive(true);
        resultText.text = "";
        opponentDropdown.ClearOptions();
        opponentDropdown.AddOptions(clubs);
        playButton.onClick.RemoveAllListeners();
        playButton.onClick.AddListener(Play);
    }

    public void Close() { gameObject.SetActive(false); }

    private void Play()
    {
        if (clubs.Count == 0) return;
        string opp = clubs[Mathf.Clamp(opponentDropdown.value, 0, clubs.Count - 1)];
        MatchResult r = MatchEngine.Simulate(TeamBuilder.FromProfile(profile), TeamBuilder.FromClub(opp));
        string text = r.ScoreLine + "\nShots " + r.shots[0] + "-" + r.shots[1] + "   Possession " + r.possession[0] + "%-" + r.possession[1] + "%\n";
        foreach (MatchEvent e in r.events)
            if (e.type == MatchEventType.Goal || e.type == MatchEventType.RedCard || e.type == MatchEventType.Injury)
                text += e.clock + " " + e.text + "\n";
        resultText.text = text;
    }
}
