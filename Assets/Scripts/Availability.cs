using System.Collections.Generic;

// Injuries, suspensions and yellow cards (no Unity code). Uses UserProfile.statuses.
// DRAFT written from how the other scripts call it; compare with your own version.
public static class Availability
{
    public const int YellowsForBan = 5;

    private static PlayerStatus Get(UserProfile p, string id, bool create)
    {
        foreach (PlayerStatus s in p.statuses) if (s.playerId == id) return s;
        if (!create) return null;
        PlayerStatus n = new PlayerStatus { playerId = id };
        p.statuses.Add(n);
        return n;
    }

    public static bool IsAvailable(UserProfile p, string id)
    {
        PlayerStatus s = Get(p, id, false);
        return s == null || (s.injuredMatches <= 0 && s.bannedMatches <= 0);
    }

    // "Injured", "Suspended" or "" when the player can play.
    public static string Reason(UserProfile p, string id)
    {
        PlayerStatus s = Get(p, id, false);
        if (s == null) return "";
        if (s.injuredMatches > 0) return "Injured";
        if (s.bannedMatches > 0) return "Suspended";
        return "";
    }

    public static int UnavailableCount(UserProfile p)
    {
        int n = 0;
        foreach (ClubSquadPlayer c in p.squad) if (!IsAvailable(p, c.id)) n++;
        return n;
    }

    // Call once per club after a match. Players who sat out serve one match first, then the match's
    // injuries, red cards and yellow cards are added. side: 0 = home, 1 = away. Returns news lines.
    public static List<string> ApplyMatch(UserProfile p, MatchResult r, int side)
    {
        List<string> news = new List<string>();

        foreach (PlayerStatus s in p.statuses)
        {
            if (s.injuredMatches > 0) s.injuredMatches--;
            if (s.bannedMatches > 0) s.bannedMatches--;
        }

        foreach (MatchEvent e in r.events)
        {
            if (e.side != side || string.IsNullOrEmpty(e.playerId)) continue;
            ClubSquadPlayer c = p.squad.Find(x => x.id == e.playerId);
            if (c == null) continue;
            if (e.type == MatchEventType.Injury)
            {
                PlayerStatus s = Get(p, c.id, true);
                s.injuredMatches = e.matchesOut;
                news.Add(c.playerName + " (" + p.clubName + ") is injured for " + e.matchesOut + (e.matchesOut == 1 ? " match." : " matches."));
            }
            else if (e.type == MatchEventType.RedCard)
            {
                PlayerStatus s = Get(p, c.id, true);
                s.bannedMatches = e.matchesOut;
                news.Add(c.playerName + " (" + p.clubName + ") is banned for " + e.matchesOut + (e.matchesOut == 1 ? " match." : " matches."));
            }
            else if (e.type == MatchEventType.YellowCard)
            {
                PlayerStatus s = Get(p, c.id, true);
                s.yellows++;
                if (s.yellows >= YellowsForBan)
                {
                    s.yellows = 0;
                    s.bannedMatches = 1;
                    news.Add(c.playerName + " (" + p.clubName + ") is banned for 1 match after " + YellowsForBan + " yellow cards.");
                }
            }
        }

        p.statuses.RemoveAll(s => s.injuredMatches <= 0 && s.bannedMatches <= 0 && s.yellows <= 0);
        return news;
    }
}
