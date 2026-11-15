using UnityEngine;
using UnityEngine.UI;

// Facilities screen: three upgrade buttons (stadium, academy, training ground).
// Assign: moneyText, messageText, and for each facility an info Text and an upgrade Button.
public class FacilitiesUI : MonoBehaviour
{
    [SerializeField] private Text moneyText;
    [SerializeField] private Text messageText;
    [SerializeField] private Text stadiumText;
    [SerializeField] private Button stadiumButton;
    [SerializeField] private Text academyText;
    [SerializeField] private Button academyButton;
    [SerializeField] private Text trainingText;
    [SerializeField] private Button trainingButton;

    private UserProfile profile;

    public void Open(UserProfile p)
    {
        profile = p;
        gameObject.SetActive(true);
        messageText.text = "";
        Wire(stadiumButton, 0);
        Wire(academyButton, 1);
        Wire(trainingButton, 2);
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private void Wire(Button b, int kind)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() =>
        {
            string problem = FacilitiesEngine.Upgrade(profile, kind);
            if (problem != null) { messageText.text = problem; return; }
            SaveSystem.Save(profile);
            messageText.text = FacilitiesEngine.Kinds[kind] + " upgraded to level " + FacilitiesEngine.GetLevel(profile, kind) + ".";
            Refresh();
        });
    }

    private void Show(Text text, Button button, int kind)
    {
        int level = FacilitiesEngine.GetLevel(profile, kind);
        long cost = FacilitiesEngine.NextCost(profile, kind);
        text.text = FacilitiesEngine.Kinds[kind] + "  level " + level + " / " + FacilitiesEngine.MaxLevel + "\n" + FacilitiesEngine.Effect(profile, kind);
        button.GetComponentInChildren<Text>().text = cost < 0 ? "Max level" : "Upgrade: " + cost.ToString("N0");
        button.interactable = cost >= 0 && profile.money >= cost;
    }

    private void Refresh()
    {
        moneyText.text = "Money: " + profile.money.ToString("N0");
        Show(stadiumText, stadiumButton, 0);
        Show(academyText, academyButton, 1);
        Show(trainingText, trainingButton, 2);
    }
}
