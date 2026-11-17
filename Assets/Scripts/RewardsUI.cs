using UnityEngine;
using UnityEngine.UI;

// Rewards screen (DRAFT): claim the daily reward and collect achievements.
public class RewardsUI : MonoBehaviour
{
    [SerializeField] private Button claimButton;
    [SerializeField] private Text streakText;
    [SerializeField] private Text messageText;

    private UserProfile profile;

    public void Open(UserProfile p)
    {
        profile = p;
        gameObject.SetActive(true);
        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(Claim);
        messageText.text = "";
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private static string Today() { return System.DateTime.Now.ToString("yyyy-MM-dd"); }

    private void Refresh()
    {
        streakText.text = "Daily streak: " + profile.loginStreak + "   Achievements: " + profile.achievements.Count;
        claimButton.interactable = profile.lastClaimDate != Today();
    }

    private void Claim()
    {
        long pay = RewardsEngine.ClaimDaily(profile, Today());
        string msg = pay < 0 ? "Already claimed today." : "Daily reward: +" + pay.ToString("N0");
        foreach (string line in RewardsEngine.CheckAchievements(profile)) msg += "\n" + line;
        messageText.text = msg;
        SaveSystem.Save(profile);
        Refresh();
    }
}
