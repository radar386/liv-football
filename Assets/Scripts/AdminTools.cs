using System;
using System.Collections.Generic;

// Admin actions (plain C#). The screen (AdminUI) only lets users with isAdmin = true use them.
public static class AdminTools
{
    public static bool IsAdmin(UserProfile p) { return p != null && p.isAdmin; }

    // Positive adds money, negative takes it away. Money never goes below 0. Returns the new amount.
    public static long ChangeMoney(UserProfile target, long amount)
    {
        target.money = Math.Max(0, target.money + amount);
        return target.money;
    }

    public static void SetMoney(UserProfile target, long amount) { target.money = Math.Max(0, amount); }

    // Back to a fresh start: keeps the username, club name, crest and admin flag.
    public static void ResetProfile(UserProfile target)
    {
        target.money = 0;
        target.coachId = "";
        target.sponsorId = "";
        target.sponsorSeasonsLeft = 0;
        target.formationChoice = "";
        target.lineupIds = new List<string>();
        target.squad = new List<ClubSquadPlayer>();
        target.academyContracts = new List<AcademyContract>();
        target.statuses = new List<PlayerStatus>();
        target.ownedPacks = new List<int>();
        target.tactic = "Balanced";
        target.stadiumLevel = 0; target.academyLevel = 0; target.trainingLevel = 0;
        target.loginStreak = 0; target.lastClaimDate = "";
        target.leagueWins = 0; target.leagueTitles = 0;
        target.achievements = new List<string>();
        target.potentials = new List<PlayerPotential>();
        target.season = 1;
        target.scoutedThisSeason = 0;
    }

    public static bool RemovePlayer(UserProfile target, string playerId)
    {
        int removed = target.squad.RemoveAll(c => c.id == playerId);
        target.academyContracts.RemoveAll(c => c.playerId == playerId);
        target.lineupIds.Remove(playerId);
        target.statuses.RemoveAll(s => s.playerId == playerId);
        target.potentials.RemoveAll(x => x.playerId == playerId);
        return removed > 0;
    }

    // Adds a catalog player for free with a random 1-4 season contract. Returns false if already owned.
    public static bool AddPlayer(UserProfile target, Footballer f, Random rng)
    {
        if (TeamBuilder.FindSquadPlayer(target, f.id) != null) return false;
        target.squad.Add(new ClubSquadPlayer { id = f.id, playerName = f.playerName, position = f.position, rating = f.rating, hasRating = true });
        AcademyScout.AddBoughtContract(target, f.id, rng);
        return true;
    }

    // Puts unopened packs of one type into the user's pack inventory (typeIndex = position in PackEngine.Types).
    public static void GivePacks(UserProfile target, int typeIndex, int count)
    {
        if (typeIndex < 0 || typeIndex >= PackEngine.Types.Length || count <= 0) return;
        PackEngine.AddOwned(target, typeIndex, count);
    }

    // ---- Weekly pack drops: each admin can give packs to EVERYONE twice per week (Mon-Sun). ----
    public const int DropsPerWeek = 2;

    private static string WeekKey(DateTime d)
    {
        int diff = ((int)d.DayOfWeek + 6) % 7;            // days since Monday
        return d.Date.AddDays(-diff).ToString("yyyy-MM-dd");
    }

    public static int DropsThisWeek(UserProfile admin, DateTime now)
    {
        string wk = WeekKey(now);
        int n = 0;
        foreach (string s in admin.packDropLog)
        {
            DateTime d;
            if (DateTime.TryParse(s, out d) && WeekKey(d) == wk) n++;
        }
        return n;
    }

    public static int DropsLeft(UserProfile admin, DateTime now) { return Math.Max(0, DropsPerWeek - DropsThisWeek(admin, now)); }

    // Gives `count` packs of one type to every user. Returns false if this admin has used both drops this week.
    public static bool WeeklyDrop(UserProfile admin, List<UserProfile> everyone, int typeIndex, int count, DateTime now)
    {
        if (!IsAdmin(admin) || count <= 0 || DropsLeft(admin, now) <= 0) return false;
        foreach (UserProfile p in everyone) GivePacks(p, typeIndex, count);
        admin.packDropLog.Add(now.ToString("yyyy-MM-dd"));
        return true;
    }

    // Free coach or sponsor (no money taken). Pass null to remove.
    public static void SetCoach(UserProfile target, CoachOffer coach) { target.coachId = coach == null ? "" : coach.id; }

    public static void SetSponsor(UserProfile target, SponsorOffer sponsor)
    {
        target.sponsorId = sponsor == null ? "" : sponsor.id;
        target.sponsorSeasonsLeft = sponsor == null ? 0 : SponsorCatalog.ContractSeasons;
    }

    // Matching players from the catalog for a search box (name or club), at most `max` results.
    public static List<Footballer> SearchPlayers(string text, int max)
    {
        List<Footballer> found = new List<Footballer>();
        if (string.IsNullOrEmpty(text)) return found;
        string q = text.Trim().ToLower();
        foreach (Footballer f in PlayerCatalog.Create())
        {
            if (f.playerName.ToLower().Contains(q) || PlayerCatalog.ClubOf(f.id).ToLower().Contains(q))
            {
                found.Add(f);
                if (found.Count >= max) break;
            }
        }
        return found;
    }

    // A new league season from scratch for every user (season 1 table, no results).
    public static LeagueState RestartLeague(List<UserProfile> profiles)
    {
        LeagueState state = LeagueEngine.Create(profiles, 1);
        SaveSystem.SaveLeague(state);
        return state;
    }
}
