using System;
using System.Collections.Generic;

// Academy scouting rules (no Unity code): up to 20 prospects per season.
[System.Serializable]
public class Prospect
{
    public string name;
    public string position;
    public int age;
    public int rating;      // current overall
    public int potential;   // what he can become
}

public static class AcademyScout
{
    public const int MaxPerSeason = 20;
    public const int MinBoughtSeasons = 1;   // bought players get a random contract of 1 to 4 seasons
    public const int MaxBoughtSeasons = 4;
    public const int ContractSeasons = 4;   // a scouted player signs for 4 seasons
    public const int MinOvr = 40;   // lowest scouting roll

    private static readonly string[] Positions = { "GK", "CB", "CB", "LB", "RB", "CDM", "CM", "CM", "CAM", "LW", "RW", "ST", "ST" };

    // Your overall = the average rating of your best 11 squad players (70 for a player with no rating yet).
    // With an empty squad it falls back to 60.
    public static int UserOvr(UserProfile p)
    {
        List<int> ratings = new List<int>();
        foreach (ClubSquadPlayer c in p.squad) ratings.Add(c.hasRating ? c.rating : 70);
        if (ratings.Count == 0) return 60;
        ratings.Sort((x, y) => y.CompareTo(x));
        int n = Math.Min(11, ratings.Count);
        int sum = 0;
        for (int i = 0; i < n; i++) sum += ratings[i];
        return (int)Math.Round(sum / (double)n);
    }

    public static int Remaining(UserProfile p) { return Math.Max(0, MaxPerSeason - p.scoutedThisSeason); }

    public static bool CanScout(UserProfile p) { return p.scoutedThisSeason < MaxPerSeason; }

    // Uses one of the season's 20 scouts. Returns null when none are left.
    // The overall is a random roll from 40 up to your own overall; a better hired coach adds +1..+3.
    public static Prospect Scout(UserProfile p, Random rng)
    {
        if (!CanScout(p)) return null;
        p.scoutedThisSeason++;

        int boost = 0;
        if (!string.IsNullOrEmpty(p.coachId))
        {
            CoachOffer c = CoachCatalog.Find(p.coachId);
            if (c != null) boost = TeamBuilder.CoachBoost(c.rating);
        }

        int age = rng.Next(15, 20);

        // Calculator style roll: every whole number from MinOvr up to YOUR overall is equally likely.
        // The coach then adds +1, +2 or +3, but a scout can never be better than your overall.
        int floor = MinOvr + FacilitiesEngine.ScoutFloorBonus(p);   // a better academy raises the lowest roll
        int cap = Math.Max(floor, UserOvr(p));
        int rating = Math.Min(cap, rng.Next(floor, cap + 1) + boost);

        int potential = rating + rng.Next(4, 31);   // potential may go above your overall
        return new Prospect
        {
            name = FictionalNames.ScoutName(rng),   // scouted youngsters are always invented names
            position = Positions[rng.Next(Positions.Length)],
            age = age,
            rating = rating,
            potential = potential
        };
    }

    // Adds the prospect to the squad.
    public static void Promote(UserProfile p, Prospect prospect)
    {
        string id = "academy_" + p.season + "_" + p.scoutedThisSeason + "_" + prospect.name.Replace(" ", "_").ToLower();
        p.academyContracts.Add(new AcademyContract { playerId = id, seasonsLeft = ContractSeasons });
        p.potentials.Add(new PlayerPotential { playerId = id, potential = prospect.potential });
        p.squad.Add(new ClubSquadPlayer
        {
            id = id,
            playerName = prospect.name,
            position = prospect.position,
            rating = prospect.rating,
            hasRating = true
        });
    }

    // Call when a new season starts: the 20 scouts come back and contracts tick down.
    // Scouted players lose one season of contract; when it reaches 0 they leave the squad.
    // Returns the names of the players whose contracts ended.
    public static List<string> NewSeason(UserProfile p)
    {
        p.season++;
        p.scoutedThisSeason = 0;

        List<string> left = new List<string>();
        for (int i = p.academyContracts.Count - 1; i >= 0; i--)
        {
            AcademyContract c = p.academyContracts[i];
            c.seasonsLeft--;
            if (c.seasonsLeft > 0) continue;
            for (int k = p.squad.Count - 1; k >= 0; k--)
            {
                if (p.squad[k].id == c.playerId) { left.Add(p.squad[k].playerName); p.squad.RemoveAt(k); }
            p.lineupIds.Remove(c.playerId);
            p.potentials.RemoveAll(x => x.playerId == c.playerId);   // a player who left cannot stay in your saved lineup
            }
            p.academyContracts.RemoveAt(i);
        }
        Wages.Pay(p);   // players are paid at the end of every season
        return left;
    }

    // For players bought in the market: a random contract of 1 to 4 seasons (like a 1-4 dice roll).
    // They tick down in NewSeason with the scouted players, and leave the squad when it reaches 0.
    public static int AddBoughtContract(UserProfile p, string playerId, System.Random rng)
    {
        int seasons = rng.Next(MinBoughtSeasons, MaxBoughtSeasons + 1);
        p.academyContracts.Add(new AcademyContract { playerId = playerId, seasonsLeft = seasons });
        return seasons;
    }

    public static bool HasContract(UserProfile p, string playerId)
    {
        foreach (AcademyContract c in p.academyContracts) if (c.playerId == playerId) return true;
        return false;
    }

    public static int SeasonsLeft(UserProfile p, string playerId)
    {
        foreach (AcademyContract c in p.academyContracts) if (c.playerId == playerId) return c.seasonsLeft;
        return 0;
    }
}
