using System;
using UnityEngine;

// Real / fictional player name switch (Profile screen button).
// DRAFT written from how the other scripts call it; compare with your own version.
// The built-in catalog already uses invented names, so "Real" mode only restores names the
// player had before switching (kept in a map in PlayerPrefs). Scouted youngsters are always invented.
public static class FictionalNames
{
    private const string Key = "liv_fictional_names";
    private static readonly string[] First = { "Kai", "Joel", "Marco", "Tomas", "Ilan", "Dario", "Nico", "Emil", "Ravi", "Teo", "Luka", "Omar", "Felix", "Sami", "Yuri", "Arlo", "Beno", "Cato" };
    private static readonly string[] Last = { "Varga", "Moreno", "Okoye", "Lindqvist", "Santoro", "Haddad", "Petrov", "Kamau", "Ferreira", "Novak", "Duarte", "Mwangi", "Rossi", "Tanaka", "Bergman", "Costa", "Ibarra", "Odhiambo" };

    public static bool Enabled { get { return PlayerPrefs.GetInt(Key, 1) == 1; } }

    public static void SetEnabled(bool on) { PlayerPrefs.SetInt(Key, on ? 1 : 0); PlayerPrefs.Save(); }

    public static string ScoutName(System.Random rng)
    {
        return First[rng.Next(First.Length)] + " " + Last[rng.Next(Last.Length)];
    }

    // Squad players already signed keep their names; nothing to rewrite because every built-in
    // catalog name is already invented. Hook for a licensed real-name set.
    public static void SyncProfileNames(UserProfile p) { }
}
