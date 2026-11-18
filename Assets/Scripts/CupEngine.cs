using System;
using System.Collections.Generic;

// Cup and Super Cup for the user clubs (plain C#, DRAFT). Played when the league season ends.
// - The Cup: every club is drawn into a knockout. Odd numbers get a bye. A drawn tie goes to penalties.
// - The Super Cup: league champion against cup winner (the league runner-up if they are the same club).
// Each tie stores its simulation seed, so the live view can replay the same match. Watch it before changing your squad.
[Serializable]
public class CupTie
{
    public string title;      // "Cup round 2", "Cup final", "Super Cup"
    public string home;       // username
    public string away;
    public int seed;
    public int homeGoals;
    public int awayGoals;
    public bool penalties;
    public string winner;     // username
}

[Serializable]
public class CupState
{
    public int season = 1;
    public int round;
    public string winner = "";
    public string runnerUp = "";
    public bool prizesPaid;
    public List<string> alive = new List<string>();
    public List<CupTie> ties = new List<CupTie>();
    public CupTie superCup;
}

public static class CupEngine
{
    public const long CupWinnerPrize = 100_000_000;
    public const long CupFinalistPrize = 40_000_000;
    public const long SuperCupPrize = 30_000_000;

    public static CupState Create(List<UserProfile> profiles, int season, Random rng)
    {
        CupState s = new CupState { season = season };
        foreach (UserProfile p in profiles) s.alive.Add(p.username);
        for (int i = s.alive.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            string t = s.alive[i]; s.alive[i] = s.alive[j]; s.alive[j] = t;
        }
        return s;
    }

    public static bool IsFinished(CupState s) { return !string.IsNullOrEmpty(s.winner); }

    public static void PlayAll(CupState s, List<UserProfile> profiles, Random rng)
    {
        int guard = 0;
        while (!IsFinished(s) && guard++ < 20) PlayRound(s, profiles, rng);
    }

    public static void PlayRound(CupState s, List<UserProfile> profiles, Random rng)
    {
        if (IsFinished(s)) return;
        if (s.alive.Count == 1) { s.winner = s.alive[0]; return; }
        s.round++;
        bool final = s.alive.Count == 2;
        string title = final ? "Cup final" : "Cup round " + s.round;
        List<string> next = new List<string>();
        for (int i = 0; i + 1 < s.alive.Count; i += 2)
        {
            CupTie t = Play(title, s.alive[i], s.alive[i + 1], profiles, rng.Next());
            s.ties.Add(t);
            next.Add(t.winner);
            if (final) s.runnerUp = t.winner == t.home ? t.away : t.home;
        }
        if (s.alive.Count % 2 == 1) next.Add(s.alive[s.alive.Count - 1]);   // a bye
        s.alive = next;
        if (s.alive.Count == 1) s.winner = s.alive[0];
    }

    public static CupTie Play(string title, string home, string away, List<UserProfile> profiles, int seed)
    {
        MatchResult r = Replay(home, away, profiles, seed);
        CupTie t = new CupTie { title = title, home = home, away = away, seed = seed, homeGoals = r.goals[0], awayGoals = r.goals[1] };
        if (r.goals[0] != r.goals[1]) t.winner = r.goals[0] > r.goals[1] ? home : away;
        else
        {
            t.penalties = true;
            t.winner = FinalsEngine.Build(r, seed).winner == 0 ? home : away;
        }
        return t;
    }

    public static MatchResult Replay(string home, string away, List<UserProfile> profiles, int seed)
    {
        return MatchEngine.Simulate(TeamBuilder.FromProfile(Find(profiles, home)), TeamBuilder.FromProfile(Find(profiles, away)), seed);
    }

    public static MatchResult Replay(CupTie t, List<UserProfile> profiles) { return Replay(t.home, t.away, profiles, t.seed); }

    public static CupTie FinalTie(CupState s)
    {
        for (int i = s.ties.Count - 1; i >= 0; i--) if (s.ties[i].title == "Cup final") return s.ties[i];
        return null;
    }

    // Champion and runner-up come from the league table (usernames).
    public static void PlaySuperCup(CupState s, string leagueChampion, string leagueRunnerUp, List<UserProfile> profiles, Random rng)
    {
        if (s.superCup != null || string.IsNullOrEmpty(s.winner)) return;
        string other = s.winner != leagueChampion ? leagueChampion : leagueRunnerUp;
        if (string.IsNullOrEmpty(other) || other == s.winner) return;
        s.superCup = Play("Super Cup", leagueChampion == s.winner ? s.winner : leagueChampion, s.winner == leagueChampion ? other : s.winner, profiles, rng.Next());
    }

    public static List<string> PayPrizes(CupState s, List<UserProfile> profiles)
    {
        List<string> lines = new List<string>();
        if (s.prizesPaid || !IsFinished(s)) return lines;
        s.prizesPaid = true;
        Pay(profiles, s.winner, CupWinnerPrize, "wins the Cup", lines);
        if (!string.IsNullOrEmpty(s.runnerUp)) Pay(profiles, s.runnerUp, CupFinalistPrize, "reaches the Cup final", lines);
        if (s.superCup != null) Pay(profiles, s.superCup.winner, SuperCupPrize, "wins the Super Cup", lines);
        return lines;
    }

    private static void Pay(List<UserProfile> profiles, string username, long amount, string what, List<string> lines)
    {
        UserProfile p = Find(profiles, username);
        if (p == null) return;
        p.money += amount;
        lines.Add(p.clubName + " " + what + " and wins " + amount.ToString("N0"));
    }

    private static UserProfile Find(List<UserProfile> profiles, string username)
    {
        foreach (UserProfile p in profiles) if (p.username == username) return p;
        return null;
    }
}
