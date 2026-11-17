using System;
using System.Collections.Generic;
using UnityEngine;

// JsonUtility cannot save a bare List, so the profiles go inside this wrapper.
[Serializable]
public class SaveData
{
    public List<UserProfile> profiles = new List<UserProfile>();
}

// Saves every profile (money, squad, coach, admin flag) as one JSON string in PlayerPrefs.
// PlayerPrefs works on Windows, Android and in the WebGL browser build.
//
// How to use:
//   List<UserProfile> all = SaveSystem.LoadAll();     // when the game starts
//   UserProfile me = SaveSystem.Find("wron fc");       // the logged-in profile
//   SaveSystem.Save(me);                               // after anything changes
//
// IMPORTANT: UserProfile and your ClubSquadPlayer class both need [System.Serializable],
// or Unity will silently skip them when saving.
public static class SaveSystem
{
    private const string Key = "liv_football_save_v1";

    private static List<UserProfile> cache;

    public static List<UserProfile> LoadAll()
    {
        if (cache != null) return cache;

        try
        {
            if (PlayerPrefs.HasKey(Key))
            {
                SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                if (data != null && data.profiles != null && data.profiles.Count > 0)
                {
                    cache = data.profiles;
                    AddMissingDefaults();   // profiles added to the game later still show up
                    return cache;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not read the save, starting fresh: " + e.Message);
        }

        cache = DefaultProfiles.Create();
        SaveAll();
        return cache;
    }

    public static UserProfile Find(string username)
    {
        foreach (UserProfile profile in LoadAll())
        {
            if (profile.username == username) return profile;
        }
        return null;
    }

    // Call after every change: a purchase, a hire, a money change.
    public static void Save(UserProfile profile)
    {
        List<UserProfile> all = LoadAll();

        // If this object is not the one in the list (for example a copy), swap it in by username.
        bool found = false;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].username == profile.username)
            {
                all[i] = profile;
                found = true;
                break;
            }
        }
        if (!found) all.Add(profile);

        SaveAll();
    }

    private const string LeagueKey = "liv_football_league_v1";

    // The league season (table, fixtures) is saved separately from the profiles.
    public static void SaveLeague(LeagueState state)
    {
        try
        {
            PlayerPrefs.SetString(LeagueKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }
        catch (Exception e) { Debug.LogWarning("LIV: could not save league: " + e.Message); }
    }

    // Returns the saved league, or null if there is none yet.
    public static LeagueState LoadLeague()
    {
        try
        {
            if (PlayerPrefs.HasKey(LeagueKey))
                return JsonUtility.FromJson<LeagueState>(PlayerPrefs.GetString(LeagueKey));
        }
        catch (Exception e) { Debug.LogWarning("LIV: could not load league: " + e.Message); }
        return null;
    }

    // Wipes the save and goes back to the starting profiles. Handy while testing.
    public static void ResetAll()
    {
        cache = DefaultProfiles.Create();
        SaveAll();
    }

    private static void AddMissingDefaults()
    {
        foreach (UserProfile fresh in DefaultProfiles.Create())
        {
            bool exists = false;
            foreach (UserProfile saved in cache)
            {
                if (saved.username == fresh.username) { exists = true; break; }
            }
            if (!exists) cache.Add(fresh);
        }
    }

    private static void SaveAll()
    {
        try
        {
            SaveData data = new SaveData { profiles = cache };
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not save: " + e.Message);
        }
    }
}
