using UnityEngine;
using UnityEngine.UI;

// Step-by-step first-run tutorial. Call TutorialUI.OpenIfFirstTime() after login;
// the settings screen can reopen it any time with Open().
public class TutorialUI : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Text pageText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button skipButton;

    private static readonly string[][] Pages =
    {
        new[] { "Welcome to LIV Football Mobile",
                "You run a club. Build a squad, pick a coach and a sponsor, play the league and win trophies and money." },
        new[] { "Shops and negotiation",
                "Coaches, players and sponsors all cost money. Make an offer: the closer to the price, the better your chance. After a yes you can ask once for 10 million off." },
        new[] { "Team builder",
                "Pick a formation and fill each position. Your coach's formation and rating boost the team. Injured and suspended players cannot play." },
        new[] { "Tactics",
                "Attacking scores more but concedes more. Defensive is safer but scores less. Balanced is in between." },
        new[] { "Packs and academy",
                "Open Standard, Premium or Elite packs for players. Scout up to 20 academy players a season: your academy and coach decide how good they can be." },
        new[] { "Money and growth",
                "Daily logins and achievements pay rewards. Upgrade your stadium for match income, and train your players so they grow over the seasons." },
        new[] { "Ready?",
                "Open the league and play your first match. You can replay this guide from Settings." },
    };

    private int page;

    public static void OpenIfFirstTime(TutorialUI tutorial)
    {
        if (tutorial != null && !GameSettings.Data.tutorialSeen) tutorial.Open();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        page = 0;
        Wire(nextButton, Next);
        Wire(backButton, Back);
        Wire(skipButton, Finish);
        ShowPage();
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    private void Next()
    {
        SoundManager.Play("click");
        if (page >= Pages.Length - 1) { Finish(); return; }
        page++;
        ShowPage();
    }

    private void Back()
    {
        SoundManager.Play("back");
        if (page > 0) page--;
        ShowPage();
    }

    private void Finish()
    {
        GameSettings.Data.tutorialSeen = true;
        GameSettings.Save();
        gameObject.SetActive(false);
    }

    private void ShowPage()
    {
        titleText.text = Pages[page][0];
        bodyText.text = Pages[page][1];
        pageText.text = (page + 1) + " / " + Pages.Length;
        if (backButton != null) backButton.interactable = page > 0;
        Text label = nextButton.GetComponentInChildren<Text>();
        if (label != null) label.text = page == Pages.Length - 1 ? "Start" : "Next";
    }
}
