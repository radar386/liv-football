using System;

// Daily login rewards and achievements (no Unity code). DRAFT; compare with your own.
public static class RewardsEngine
{
    public const long DailyBase = 5_000_000;
    public const long DailyPerStreakDay = 1_000_000;
    public const int MaxStreakBonus = 7;

    // Returns the money paid, or -1 if today's reward was already claimed. today is "yyyy-MM-dd".
    public static long ClaimDaily(UserProfile p, string today)
    {
        if (p.lastClaimDate == today) return -1;
        string yesterday = DateTime.Parse(today).AddDays(-1).ToString("yyyy-MM-dd");
        p.loginStreak = p.lastClaimDate == yesterday ? p.loginStreak + 1 : 1;
        p.lastClaimDate = today;
        long pay = DailyBase + DailyPerStreakDay * Math.Min(p.loginStreak, MaxStreakBonus);
        p.money += pay;
        return pay;
    }

    // Pays every achievement reached and not yet paid. Returns a line for each new one.
    public static System.Collections.Generic.List<string> CheckAchievements(UserProfile p)
    {
        var lines = new System.Collections.Generic.List<string>();
        Award(p, lines, "first_win", p.leagueWins >= 1, 10_000_000, "First league win");
        Award(p, lines, "ten_wins", p.leagueWins >= 10, 50_000_000, "10 league wins");
        Award(p, lines, "champion", p.leagueTitles >= 1, 150_000_000, "League champion");
        Award(p, lines, "big_squad", p.squad.Count >= 25, 20_000_000, "25 players in the squad");
        Award(p, lines, "streak7", p.loginStreak >= 7, 30_000_000, "7 day login streak");
        return lines;
    }

    private static void Award(UserProfile p, System.Collections.Generic.List<string> lines, string id, bool reached, long reward, string label)
    {
        if (!reached || p.achievements.Contains(id)) return;
        p.achievements.Add(id);
        p.money += reward;
        lines.Add(label + ": +" + reward.ToString("N0"));
    }
}
