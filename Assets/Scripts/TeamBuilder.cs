using System;
using System.Collections.Generic;

// Turns your profiles and the real clubs in the catalogs into SimTeams for the MatchEngine.
// Opponents (NPC teams) are the real clubs from PlayerCatalog, coached by the real coach from CoachCatalog.
public static class TeamBuilder
{
    // The coach lifts every player's overall: +1 (good), +2 (very good), +3 (elite).
    public static int CoachBoost(int coachRating)
    {
        if (coachRating >= 88) return 3;
        if (coachRating >= 75) return 2;
        if (coachRating >= 60) return 1;
        return 0;
    }

    // The formations a player can choose in the team builder.
    public static readonly string[] Formations =
    {
        "4-4-2", "4-3-3", "4-2-3-1", "4-1-4-1", "4-5-1", "3-5-2", "3-4-3", "5-3-2", "5-4-1"
    };

    // Your club: your saved starting 11 and formation if you built one, otherwise the best XI automatically.
    public static SimTeam FromProfile(UserProfile p)
    {
        SimTeam team = new SimTeam { name = p.clubName };
        CoachOffer coach = string.IsNullOrEmpty(p.coachId) ? null : (CoachCatalog.Find(p.coachId) ?? OwnCoachService.AsOffer(p));
        ApplyCoach(team, coach, "4-4-2", 55);
        if (!string.IsNullOrEmpty(p.formationChoice)) team.formation = p.formationChoice;
        team.tactic = string.IsNullOrEmpty(p.tactic) ? "Balanced" : p.tactic;

        int boost = CoachBoost(team.coachRating);
        if (p.lineupIds != null && p.lineupIds.Count == 11 && LineupProblem(p, p.lineupIds, team.formation) == null)
        {
            // Your saved lineup. Injured or suspended starters are swapped for the best fit player in the same area.
            List<string> ids = ReplaceUnavailable(p, p.lineupIds);
            if (ids != null && LineupProblem(p, ids, team.formation) == null)
            {
                foreach (string id in ids)
                {
                    ClubSquadPlayer c = FindSquadPlayer(p, id);
                    team.players.Add(new SimPlayer { id = c.id, name = c.playerName, position = c.position, rating = (c.hasRating ? c.rating : 70) + boost });
                }
                return team;
            }
        }

        List<SimPlayer> pool = new List<SimPlayer>();
        foreach (ClubSquadPlayer c in p.squad)
        {
            if (!Availability.IsAvailable(p, c.id)) continue;   // injured / suspended players do not play
            pool.Add(new SimPlayer { id = c.id, name = c.playerName, position = c.position, rating = c.hasRating ? c.rating : 70 });
        }
        PickEleven(team, pool);
        return team;
    }

    // Swaps unavailable starters for the best available player of the same area (GK / DEF / MID / FWD). Null if impossible.
    private static List<string> ReplaceUnavailable(UserProfile p, List<string> lineup)
    {
        List<string> ids = new List<string>(lineup);
        for (int i = 0; i < ids.Count; i++)
        {
            if (Availability.IsAvailable(p, ids[i])) continue;
            string group = Group(FindSquadPlayer(p, ids[i]).position);
            ClubSquadPlayer best = null;
            foreach (ClubSquadPlayer c in p.squad)
            {
                if (ids.Contains(c.id) || !Availability.IsAvailable(p, c.id) || Group(c.position) != group) continue;
                if (best == null || (c.hasRating ? c.rating : 70) > (best.hasRating ? best.rating : 70)) best = c;
            }
            if (best == null) return null;
            ids[i] = best.id;
        }
        return ids;
    }

    // Returns null if all chosen players are fit, otherwise a message naming the first one who is not.
    public static string AvailabilityProblem(UserProfile p, List<string> ids)
    {
        foreach (string id in ids)
        {
            if (Availability.IsAvailable(p, id)) continue;
            ClubSquadPlayer c = FindSquadPlayer(p, id);
            return (c != null ? c.playerName : id) + " is " + Availability.Reason(p, id).ToLower() + ".";
        }
        return null;
    }

    // The automatic best XI for a formation, as squad player ids (reserve fillers are skipped).
    public static List<string> AutoLineup(UserProfile p, string formation)
    {
        SimTeam team = new SimTeam { name = p.clubName, formation = formation, coachRating = 0 };
        List<SimPlayer> pool = new List<SimPlayer>();
        foreach (ClubSquadPlayer c in p.squad)
        {
            if (!Availability.IsAvailable(p, c.id)) continue;
            pool.Add(new SimPlayer { id = c.id, name = c.playerName, position = c.position, rating = c.hasRating ? c.rating : 70 });
        }
        PickEleven(team, pool);
        List<string> ids = new List<string>();
        foreach (SimPlayer s in team.players) if (!string.IsNullOrEmpty(s.id) && !ids.Contains(s.id)) ids.Add(s.id);
        return ids;
    }

    // Returns null when the lineup is valid, otherwise a message saying what is wrong.
    // Rules: exactly 11 different squad players, exactly 1 goalkeeper, and the defenders / midfielders /
    // forwards must match the formation (4-3-3 = 4 DEF, 3 MID, 3 FWD).
    public static string LineupProblem(UserProfile p, List<string> ids, string formation)
    {
        int d, m, f;
        if (!SimTeam.ParseFormation(formation, out d, out m, out f)) return "Pick a formation first.";
        if (ids.Count != 11) return "Pick exactly 11 players (you have " + ids.Count + ").";
        int gk = 0, de = 0, mi = 0, fw = 0;
        HashSet<string> seen = new HashSet<string>();
        foreach (string id in ids)
        {
            ClubSquadPlayer c = FindSquadPlayer(p, id);
            if (c == null || !seen.Add(id)) return "A chosen player is not in your squad.";
            string g = Group(c.position);
            if (g == "GK") gk++; else if (g == "DEF") de++; else if (g == "MID") mi++; else fw++;
        }
        if (gk != 1 || de != d || mi != m || fw != f)
            return formation + " needs 1 GK, " + d + " DEF, " + m + " MID, " + f + " FWD. You have " + gk + " GK, " + de + " DEF, " + mi + " MID, " + fw + " FWD.";
        return null;
    }

    public static ClubSquadPlayer FindSquadPlayer(UserProfile p, string id)
    {
        foreach (ClubSquadPlayer c in p.squad) if (c.id == id) return c;
        return null;
    }

    // A real club from the player catalog (the NPC teams).
    public static SimTeam FromClub(string club)
    {
        SimTeam team = new SimTeam { name = club };
        CoachOffer found = null;
        foreach (CoachOffer c in CoachCatalog.Create())
        {
            if (c.club != null && string.Equals(c.club, club, StringComparison.OrdinalIgnoreCase)) { found = c; break; }
        }
        ApplyCoach(team, found, "4-3-3", 70);

        List<SimPlayer> pool = new List<SimPlayer>();
        foreach (Footballer f in PlayerCatalog.Create())
        {
            if (PlayerCatalog.ClubOf(f.id) == club)
                pool.Add(new SimPlayer { name = f.playerName, position = f.position, rating = f.rating });
        }
        PickEleven(team, pool);
        return team;
    }

    public static List<string> CatalogClubs()
    {
        List<string> clubs = new List<string>();
        foreach (Footballer f in PlayerCatalog.Create())
        {
            string c = PlayerCatalog.ClubOf(f.id);
            if (!string.IsNullOrEmpty(c) && !clubs.Contains(c)) clubs.Add(c);
        }
        clubs.Sort();
        return clubs;
    }

    private static void ApplyCoach(SimTeam team, CoachOffer coach, string defaultFormation, int defaultRating)
    {
        team.formation = coach != null && !string.IsNullOrEmpty(coach.formation) ? coach.formation : defaultFormation;
        team.coachRating = coach != null ? coach.rating : defaultRating;
    }

    public static string Group(string pos)
    {
        switch (pos)
        {
            case "GK": return "GK";
            case "CB": case "LB": case "RB": return "DEF";
            case "CDM": case "CM": case "CAM": return "MID";
            default: return "FWD";
        }
    }

    // Best 1 GK + the formation's defenders/midfielders/forwards. Gaps are filled with leftovers or a reserve.
    private static void PickEleven(SimTeam team, List<SimPlayer> pool)
    {
        int d, m, f;
        SimTeam.ParseFormation(team.formation, out d, out m, out f);
        pool.Sort((a, b) => b.rating.CompareTo(a.rating));
        int boost = CoachBoost(team.coachRating);
        string[] groups = { "GK", "DEF", "MID", "FWD" };
        int[] counts = { 1, d, m, f };
        HashSet<SimPlayer> taken = new HashSet<SimPlayer>();
        List<SimPlayer> chosen = new List<SimPlayer>();
        List<string> gaps = new List<string>();

        for (int g = 0; g < groups.Length; g++)
        {
            int left = counts[g];
            foreach (SimPlayer p in pool)
            {
                if (left == 0) break;
                if (Group(p.position) != groups[g]) continue;
                chosen.Add(Copy(p, p.position, 0, boost));
                taken.Add(p);
                left--;
            }
            for (int i = 0; i < left; i++) gaps.Add(groups[g]);
        }

        foreach (string g in gaps)
        {
            string pos = g == "GK" ? "GK" : (g == "DEF" ? "CB" : (g == "MID" ? "CM" : "ST"));
            SimPlayer spare = null;
            foreach (SimPlayer p in pool) { if (!taken.Contains(p)) { spare = p; break; } }
            if (spare != null) { taken.Add(spare); chosen.Add(Copy(spare, pos, 12, boost)); }
            else chosen.Add(new SimPlayer { name = "Reserve", position = pos, rating = 55 });
        }
        team.players = chosen;
    }

    private static SimPlayer Copy(SimPlayer p, string position, int penalty, int boost)
    {
        return new SimPlayer { id = p.id, name = p.name, position = position, rating = p.rating - penalty + boost };
    }
}
