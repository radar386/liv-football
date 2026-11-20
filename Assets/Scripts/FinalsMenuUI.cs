using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Lists the Cup final and the Champions Shield of the season and opens each in LiveFinalUI (DRAFT).
// Inspector: a Text for the list, two Buttons (Watch Cup final, Watch Champions Shield), a Close button, and the LiveFinalUI.
public class FinalsMenuUI : MonoBehaviour
{
    [SerializeField] private Text infoText;
    [SerializeField] private Button watchCupButton;
    [SerializeField] private Button watchSuperCupButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private LiveFinalUI liveFinal;

    private CupState cup;
    private List<UserProfile> profiles;

    public void Open(CupState state, List<UserProfile> all)
    {
        cup = state;
        profiles = all;
        gameObject.SetActive(true);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => gameObject.SetActive(false));

        CupTie final = cup == null ? null : CupEngine.FinalTie(cup);
        watchCupButton.interactable = final != null;
        watchSuperCupButton.interactable = cup != null && cup.superCup != null;
        watchCupButton.onClick.RemoveAllListeners();
        watchSuperCupButton.onClick.RemoveAllListeners();
        watchCupButton.onClick.AddListener(() => Watch(final));
        watchSuperCupButton.onClick.AddListener(() => Watch(cup.superCup));

        string text = "Season " + (cup == null ? 0 : cup.season) + " finals\n";
        text += final == null ? "The Cup has not been played yet.\n" : final.title + ": " + Name(final.home) + " vs " + Name(final.away) + "\n";
        if (cup != null && cup.superCup != null) text += cup.superCup.title + ": " + Name(cup.superCup.home) + " vs " + Name(cup.superCup.away) + "\n";
        infoText.text = text;
    }

    private void Watch(CupTie t)
    {
        if (t == null) return;
        MatchResult r = CupEngine.Replay(t, profiles);
        // The squads are the same as at the final, so the replay gives the same score. If you changed your squad since, it can differ.
        liveFinal.Open(t.title, r, KitCatalog.ForProfile(Find(t.home)), KitCatalog.ForProfile(Find(t.away)), t.seed, null);
    }

    private string Name(string username) { UserProfile p = Find(username); return p == null ? username : p.clubName; }

    private UserProfile Find(string username)
    {
        foreach (UserProfile p in profiles) if (p.username == username) return p;
        return null;
    }
}
