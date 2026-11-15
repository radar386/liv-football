using UnityEngine;
using UnityEngine.UI;

// Settings screen: sound volume, music volume, mute, fast pack reveal, replay tutorial.
// Wire the controls in the Inspector. tutorial is optional.
public class SettingsUI : MonoBehaviour
{
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Toggle muteToggle;
    [SerializeField] private Toggle fastRevealToggle;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TutorialUI tutorial;

    public void Open()
    {
        gameObject.SetActive(true);
        SettingsData s = GameSettings.Data;

        sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
        musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
        sfxSlider.SetValueWithoutNotify(s.sfxVolume);
        musicSlider.SetValueWithoutNotify(s.musicVolume);
        muteToggle.SetIsOnWithoutNotify(s.muted);
        fastRevealToggle.SetIsOnWithoutNotify(s.fastReveal);

        sfxSlider.onValueChanged.RemoveAllListeners();
        sfxSlider.onValueChanged.AddListener(v => { GameSettings.Data.sfxVolume = v; GameSettings.Save(); });
        musicSlider.onValueChanged.RemoveAllListeners();
        musicSlider.onValueChanged.AddListener(v => { GameSettings.Data.musicVolume = v; GameSettings.Save(); });
        muteToggle.onValueChanged.RemoveAllListeners();
        muteToggle.onValueChanged.AddListener(v => { GameSettings.Data.muted = v; GameSettings.Save(); });
        fastRevealToggle.onValueChanged.RemoveAllListeners();
        fastRevealToggle.onValueChanged.AddListener(v => { GameSettings.Data.fastReveal = v; GameSettings.Save(); });

        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveAllListeners();
            tutorialButton.onClick.AddListener(() => { SoundManager.Play("click"); if (tutorial != null) tutorial.Open(); });
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Close() { SoundManager.Play("back"); gameObject.SetActive(false); }
}
