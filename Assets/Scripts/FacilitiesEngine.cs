using System;

// Facilities rules (no Unity code): stadium, academy and training ground, levels 0 to 5.
// DRAFT written from how the other scripts call it; compare with your own version.
public static class FacilitiesEngine
{
    public const int MaxLevel = 5;
    public static readonly string[] Kinds = { "Stadium", "Academy", "Training ground" };   // kind 0, 1, 2

    // Cost to go from level n to n+1.
    private static readonly long[] Costs = { 50_000_000, 120_000_000, 250_000_000, 500_000_000, 900_000_000 };
    private const long GatePerLevel = 4_000_000;   // extra home gate receipts per stadium level
    private const long GateBase = 2_000_000;

    public static int GetLevel(UserProfile p, int kind)
    {
        return kind == 0 ? p.stadiumLevel : kind == 1 ? p.academyLevel : p.trainingLevel;
    }

    private static void SetLevel(UserProfile p, int kind, int level)
    {
        if (kind == 0) p.stadiumLevel = level; else if (kind == 1) p.academyLevel = level; else p.trainingLevel = level;
    }

    // -1 when already at the top level.
    public static long NextCost(UserProfile p, int kind)
    {
        int level = GetLevel(p, kind);
        return level >= MaxLevel ? -1 : Costs[level];
    }

    // Returns null on success, otherwise the reason it failed.
    public static string Upgrade(UserProfile p, int kind)
    {
        if (kind < 0 || kind >= Kinds.Length) return "Unknown facility.";
        long cost = NextCost(p, kind);
        if (cost < 0) return Kinds[kind] + " is already at the top level.";
        if (p.money < cost) return "Not enough money. Need " + cost.ToString("N0") + ".";
        p.money -= cost;
        SetLevel(p, kind, GetLevel(p, kind) + 1);
        return null;
    }

    // Gate receipts for the home club after each league match.
    public static long MatchdayIncome(UserProfile p) { return GateBase + GatePerLevel * p.stadiumLevel; }

    // Raises the lowest academy scouting roll (0 to 10 points).
    public static int ScoutFloorBonus(UserProfile p) { return p.academyLevel * 2; }

    // Extra possible training points (0 to 3, see TrainingEngine).
    public static int TrainingBonus(UserProfile p) { return Math.Min(3, (p.trainingLevel + 1) / 2); }

    public static string Effect(UserProfile p, int kind)
    {
        if (kind == 0) return "Home gate: " + MatchdayIncome(p).ToString("N0") + " per match";
        if (kind == 1) return "Scouts start at " + (AcademyScout.MinOvr + ScoutFloorBonus(p)) + " overall";
        return "Training: up to +" + TrainingBonus(p) + " extra points";
    }
}
