using UnityEngine;
using UnityEngine.UI;

// Academy screen (DRAFT): scout a youngster (20 per season), then sign him or skip.
public class AcademyUI : MonoBehaviour
{
    [SerializeField] private Button scoutButton;
    [SerializeField] private Button signButton;
    [SerializeField] private Text prospectText;
    [SerializeField] private Text scoutsLeftText;
    [SerializeField] private Text messageText;

    private UserProfile profile;
    private Prospect current;
    private readonly System.Random rng = new System.Random();

    public void Open(UserProfile p)
    {
        profile = p;
        current = null;
        gameObject.SetActive(true);
        messageText.text = "";
        scoutButton.onClick.RemoveAllListeners();
        scoutButton.onClick.AddListener(Scout);
        signButton.onClick.RemoveAllListeners();
        signButton.onClick.AddListener(Sign);
        Refresh();
    }

    public void Close() { gameObject.SetActive(false); }

    private void Refresh()
    {
        scoutsLeftText.text = "Scouts left this season: " + AcademyScout.Remaining(profile);
        scoutButton.interactable = AcademyScout.CanScout(profile);
        signButton.interactable = current != null;
        prospectText.text = current == null ? "" :
            current.name + "  " + current.position + "  age " + current.age + "\nOverall " + current.rating + "   Potential " + current.potential;
    }

    private void Scout()
    {
        current = AcademyScout.Scout(profile, rng);
        if (current == null) messageText.text = "No scouts left this season.";
        SaveSystem.Save(profile);
        Refresh();
    }

    private void Sign()
    {
        if (current == null) return;
        AcademyScout.Promote(profile, current);
        messageText.text = current.name + " joined the squad on a " + AcademyScout.ContractSeasons + "-season contract.";
        current = null;
        SaveSystem.Save(profile);
        Refresh();
    }
}
