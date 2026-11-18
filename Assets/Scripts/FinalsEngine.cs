using System;
using System.Collections.Generic;

// Plain C# (DRAFT). Turns a finished match into a live replay script: the goals by minute, and a
// penalty shootout with named takers if the match was level. The result is decided first, so the live
// view only replays it and skipping never changes the winner.
public class FinalKick
{
    public int side;          // 0 = home, 1 = away
    public string player;
    public bool scored;
    public string how;        // for a miss: "is saved by the keeper", "hits the post", ...
}

public class LiveFinalScript
{
    public List<MatchEvent> goals = new List<MatchEvent>();   // type Goal only, in time order
    public List<FinalKick> kicks = new List<FinalKick>();     // home, away, home, away ...
    public int[] penalties = new int[2];
    public bool hasShootout;
    public int winner;        // 0 or 1
}

public static class FinalsEngine
{
    public const float LiveSeconds = 60f;       // the whole 90 minutes, including half-time
    public const float HalfTimeSeconds = 3f;
    public const float PenaltySeconds = 15f;    // the shootout comes on top

    private static readonly string[] Misses =
        { "is saved by the keeper", "hits the post", "shoots wide", "blazes over the bar" };

    public static LiveFinalScript Build(MatchResult r, int seed)
    {
        Random rng = new Random(seed);
        LiveFinalScript s = new LiveFinalScript();
        foreach (MatchEvent e in r.events) if (e.type == MatchEventType.Goal) s.goals.Add(e);

        if (r.goals[0] != r.goals[1]) { s.winner = r.goals[0] > r.goals[1] ? 0 : 1; return s; }

        s.hasShootout = true;
        double hs = Strength(r.home), aw = Strength(r.away);
        double pHome = Math.Max(0.35, Math.Min(0.65, 0.5 + (hs - aw) / 200.0));
        s.winner = rng.NextDouble() < pHome ? 0 : 1;

        int winnerScores = rng.Next(2) == 0 ? 4 : 5;
        int loserScores = Math.Max(2, winnerScores - 1 - (rng.NextDouble() < 0.3 ? 1 : 0));
        List<FinalKick> home = Kicks(r.home, 0, s.winner == 0 ? winnerScores : loserScores, rng);
        List<FinalKick> away = Kicks(r.away, 1, s.winner == 1 ? winnerScores : loserScores, rng);
        for (int i = 0; i < 5; i++)
        {
            s.kicks.Add(home[i]);
            s.kicks.Add(away[i]);
            if (home[i].scored) s.penalties[0]++;
            if (away[i].scored) s.penalties[1]++;
        }
        return s;
    }

    private static List<FinalKick> Kicks(SimTeam team, int side, int scores, Random rng)
    {
        List<string> names = new List<string>();
        foreach (SimPlayer p in team.players) if (p.position != "GK") names.Add(p.name);
        if (names.Count == 0) names.Add("the captain");
        for (int i = names.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); string t = names[i]; names[i] = names[j]; names[j] = t; }

        bool[] flags = new bool[5];
        List<int> idx = new List<int> { 0, 1, 2, 3, 4 };
        for (int i = idx.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = idx[i]; idx[i] = idx[j]; idx[j] = t; }
        for (int i = 0; i < scores && i < 5; i++) flags[idx[i]] = true;

        List<FinalKick> list = new List<FinalKick>();
        for (int i = 0; i < 5; i++)
            list.Add(new FinalKick { side = side, player = names[i % names.Count], scored = flags[i], how = flags[i] ? "scores" : Misses[rng.Next(Misses.Length)] });
        return list;
    }

    private static double Strength(SimTeam t) { return t.Attack + t.Midfield + t.Defense + t.Keeper; }

    // "23'" -> 23, "45+2'" -> 45. Used to place goals on the clock.
    public static int MinuteOf(MatchEvent e)
    {
        int m = 0;
        foreach (char c in e.clock ?? "")
        {
            if (c < '0' || c > '9') break;
            m = m * 10 + (c - '0');
        }
        return m > 0 ? m : e.tick + 1;
    }
}
