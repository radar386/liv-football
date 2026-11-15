using System.Collections.Generic;

// DRAFT: field names are assumptions. Rename to match your real profile class.
[System.Serializable]
public class UserProfile
{
    public string username;
    public string clubName;
    public string crestId;   // sprite name in Assets/Resources/Crests/
    public long money;
    public bool isAdmin;
    public int sponsorSeasonsLeft;   // seasons left on the sponsor deal
    public string sponsorId; // id from SponsorCatalog; empty = no sponsor
    public string formationChoice;                        // chosen formation, empty = use the coach's
    public List<string> lineupIds = new List<string>();   // chosen starting 11 (squad player ids), empty = automatic
    public string coachId;   // id from CoachCatalog; empty = no coach hired
    public List<ClubSquadPlayer> squad = new List<ClubSquadPlayer>();
    public int season = 1;            // current season number
    public int scoutedThisSeason;     // academy scouts used this season (max 20)
    public List<string> processedPurchases = new List<string>();   // payment transaction ids already credited (stops double-crediting)
    public List<string> packDropLog = new List<string>();   // dates (yyyy-MM-dd) this admin ran a pack drop for everyone
    public List<int> ownedPacks = new List<int>();   // unopened packs, one number per pack type (Standard, Premium, Elite)
    public string tactic = "Balanced";   // Attacking, Balanced or Defensive
    public int stadiumLevel;             // facilities, 0 to 5
    public int academyLevel;
    public int trainingLevel;
    public int loginStreak;              // daily reward streak
    public string lastClaimDate = "";    // yyyy-MM-dd of the last daily reward
    public int leagueWins;               // league matches won (for achievements)
    public int leagueTitles;             // league seasons won
    public List<string> achievements = new List<string>();   // ids of achievements already paid
    public List<PlayerPotential> potentials = new List<PlayerPotential>();   // how good scouted academy players can become
    public List<PlayerStatus> statuses = new List<PlayerStatus>();   // injuries, bans and yellow cards
    public List<AcademyContract> academyContracts = new List<AcademyContract>();   // seasons left for scouted players
}

[System.Serializable]
public class AcademyContract
{
    public string playerId;
    public int seasonsLeft;
}

public static class DefaultProfiles
{
    public static List<UserProfile> Create()
    {
        List<UserProfile> profiles = new List<UserProfile>();

        profiles.Add(CreateProfile("Crimson",   "King Crimson",     "crimson"));
        profiles.Add(CreateProfile("JAI",       "JAI Football Club", "jai"));
        profiles.Add(CreateProfile("fantacy",   "Fantacy Club",     "fantacy"));
        profiles.Add(CreateProfile("adams",     "Adams Club",       "adams"));
        profiles.Add(CreateProfile("calc .fc",  "CALC FC",          "calc_fc"));
        // Crest image itself still reads "SYLFOID FC"; fix the artwork to say Sylford.
        profiles.Add(CreateProfile("sylford.fc", "Sylford FC",      "sylford_fc"));
        profiles.Add(CreateProfile("manshine city", "Man Shine City", "manshine_city"));
        profiles.Add(CreateProfile("F1FC", "F1FC", "f1fc", isAdmin: true));
        profiles.Add(CreateProfile("Winnie FC", "Winnie FC", "winnie_fc"));
        profiles.Add(CreateProfile("wron fc", "Wron FC", "wron_fc", isAdmin: true));
        profiles.Add(CreateProfile("sporty fc", "Sporty.FC", "sporty_fc"));
        profiles.Add(CreateProfile("dark warriors", "Dark Warriors Football Club", "dark_warriors"));
        profiles.Add(CreateProfile("bastard fc", "Bastard Football Club", "bastard_fc"));
        profiles.Add(CreateProfile("galaxy 11", "Galaxy 11 FC", "galaxy_11"));
        // Crest image itself still reads "UMENDED FC"; fix the artwork to say Unmended.
        profiles.Add(CreateProfile("unmended fc", "Unmended FC", "unmended_fc"));
        // Crest image itself reads "BLACK ROVERS"; full club name is Black Steel Rovers.
        profiles.Add(CreateProfile("black steel rovers", "Black Steel Rovers", "black_steel_rovers"));
        profiles.Add(CreateProfile("never loose to fools", "NLTF FC", "nltf_fc"));
        profiles.Add(CreateProfile("nightmare fc", "Nightmare FC", "nightmare_fc"));
        profiles.Add(CreateProfile("salvator fc", "Salvator FC", "salvator_fc"));

        return profiles;
    }

    private static UserProfile CreateProfile(string username, string clubName, string crestId, long money = 0, bool isAdmin = false)
    {
        return new UserProfile
        {
            username = username,
            clubName = clubName,
            crestId = crestId,
            money = money,
            isAdmin = isAdmin
        };
    }
}

// Injury / suspension record for one squad player (only players with something to track have one).
[System.Serializable]
public class PlayerStatus
{
    public string playerId;
    public int injuredMatches;   // matches still to miss because of injury
    public int bannedMatches;    // matches still to miss because of a suspension
    public int yellows;          // yellow cards collected (5 = one match ban)
}

// The most a scouted academy player can grow to (his "potential"). Market and pack players have none.
[System.Serializable]
public class PlayerPotential
{
    public string playerId;
    public int potential;
}
