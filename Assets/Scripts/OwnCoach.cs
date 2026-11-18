using System;
using System.Collections.Generic;

// A coach the player makes up (DRAFT). Free, no contract, and NO bonus: it plays at the no-coach rating (55), boost 0,
// and no scouting bonus. It is only a name, a formation label and (optionally) a photo.
[Serializable]
public class OwnCoach
{
    public string id;          // always starts with "own_"
    public string coachName;
    public string formation;
}

public static class OwnCoachService
{
    public const int MaxPerClub = 5;
    public const int MaxNameLength = 24;
    public const string Prefix = "own_";

    public static bool IsOwn(string coachId) { return !string.IsNullOrEmpty(coachId) && coachId.StartsWith(Prefix); }

    public static OwnCoach Create(UserProfile p, string name, string formation, out string problem)
    {
        problem = null;
        name = (name ?? "").Trim();
        if (name.Length == 0) { problem = "Type a name for the coach."; return null; }
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
        if (p.ownCoaches == null) p.ownCoaches = new List<OwnCoach>();
        if (p.ownCoaches.Count >= MaxPerClub) { problem = "You already have " + MaxPerClub + " own coaches."; return null; }
        if (Array.IndexOf(TeamBuilder.Formations, formation) < 0) formation = "4-3-3";

        OwnCoach c = new OwnCoach
        {
            id = Prefix + p.username + "_" + DateTime.UtcNow.Ticks.ToString("x"),
            coachName = name,
            formation = formation
        };
        p.ownCoaches.Add(c);
        return c;
    }

    public static OwnCoach Find(UserProfile p, string id)
    {
        if (p == null || p.ownCoaches == null) return null;
        foreach (OwnCoach c in p.ownCoaches) if (c.id == id) return c;
        return null;
    }

    public static bool Hire(UserProfile p, string id)
    {
        if (Find(p, id) == null) return false;
        p.coachId = id;
        return true;
    }

    public static void Delete(UserProfile p, string id)
    {
        if (p.ownCoaches == null) return;
        p.ownCoaches.RemoveAll(c => c.id == id);
        if (p.coachId == id) p.coachId = "";
    }

    // The match engine and the screens only know CoachOffer, so an own coach is shown to them as a no-coach-level
    // coach (rating 55, the same as having no coach): boost 0, scouting 0.
    public const int NoCoachRating = 55;
    public static CoachOffer AsOffer(UserProfile p)
    {
        OwnCoach c = IsOwn(p.coachId) ? Find(p, p.coachId) : null;
        if (c == null) return null;
        return new CoachOffer { id = c.id, coachName = c.coachName, club = "Your own coach", formation = c.formation, rating = NoCoachRating, price = 0 };
    }
}
