using System;
using UnityEngine;

// Player settings, saved separately from the club profiles so resetting a profile never changes them.
[Serializable]
public class SettingsData
{
    public float sfxVolume = 0.8f;
    public float musicVolume = 0.4f;
    public bool muted = false;
    public bool fastReveal = false;     // skip the slow pack-opening animation
    public bool tutorialSeen = false;
}

public static class GameSettings
{
    private const string Key = "liv_football_settings_v1";
    private static SettingsData data;

    public static SettingsData Data
    {
        get
        {
            if (data != null) return data;
            try
            {
                if (PlayerPrefs.HasKey(Key)) data = JsonUtility.FromJson<SettingsData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e) { Debug.LogWarning("Could not read settings: " + e.Message); }
            if (data == null) data = new SettingsData();
            return data;
        }
    }

    public static void Save()
    {
        try
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }
        catch (Exception e) { Debug.LogWarning("Could not save settings: " + e.Message); }
        if (SoundManager.Instance != null) SoundManager.Instance.ApplyVolumes();
    }
}
