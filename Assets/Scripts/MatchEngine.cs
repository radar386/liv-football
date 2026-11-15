using System;
using System.Collections.Generic;

// The simulation engine. Plain C#, no Unity code, so it also runs in tests and on a server.
// It plays a 90 minute match minute by minute and returns the score, stats and a commentary log.

public class SimPlayer
{
    public string id;
    public string name;
    public string position;   // GK, RB, CB, LB, CDM, CM, CAM, LW, RW, ST
    public int rating;
}

public class SimTeam
{
    public string name = "Team";
    public string formation = "4-4-2";
    public int coachRating = 70;
    public string tactic = "Balanced";   // Attacking, Balanced or Defensive
    public List<SimPlayer> players = new List<SimPlayer>();

    // Filled in by Recalculate(): the strength of each part of the team.
    public double Attack;
    public double Midfield;
    public double Defense;
    public double Keeper;

    // "4-2-3-1" -> defenders 4, midfielders 5 (2+3), forwards 1.
    public static bool ParseFormation(string formation, out int defenders, out int midfielders, out int forwards)
    {
        defenders = 4; midfielders = 4; forwards = 2;
        if (string.IsNullOrEmpty(formation)) return false;
        string[] parts = formation.Split('-');
        if (parts.Length < 3) return false;
        int[] n = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out n[i])) return false;
        }
        int d = n[0];
        int f = n[n.Length - 1];
        int m = 0;
        for (int i = 1; i < n.Length - 1; i++) m += n[i];
        if (d + m + f != 10) return false;
        defenders = d; midfielders = m; forwards = f;
        return true;
    }

    private static double Weighted(List<SimPlayer> list, Dictionary<string, double> weights, double fallback)
    {
        double sum = 0, total = 0;
        foreach (SimPlayer p in list)
        {
            double w;
            if (weights.TryGetValue(p.position, out w)) { sum += p.rating * w; total += w; }
        }
        return total > 0 ? sum / total : fallback;
    }

    public void Recalculate()
    {
        Keeper = Weighted(players, new Dictionary<string, double> { { "GK", 1.0 } }, 55);
        Defense = Weighted(players, new Dictionary<string, double> { { "CB", 1.0 }, { "LB", 0.8 }, { "RB", 0.8 }, { "CDM", 0.5 } }, 55);
        Midfield = Weighted(players, new Dictionary<string, double> { { "CM", 1.0 }, { "CDM", 0.7 }, { "CAM", 0.7 } }, 55);
        Attack = Weighted(players, new Dictionary<string, double> { { "ST", 1.0 }, { "LW", 0.8 }, { "RW", 0.8 }, { "CAM", 0.4 } }, 55);

        // The coach's effect is added to each player's rating by TeamBuilder (+1, +2 or +3).
        double coach = 0;

        // Formation: more defenders = safer, more forwards = sharper in attack.
        int d, m, f;
        ParseFormation(formation, out d, out m, out f);
        Defense += coach + (d - 4) * 1.5;
        if (tactic == "Attacking") { Attack += 3; Defense -= 5; }
        else if (tactic == "Defensive") { Attack -= 3; Defense += 6; }
        Midfield += coach;
        Attack += coach + (f - 2) * 1.2;

        // Fewer than 11 players is a handicap.
        int missing = 11 - players.Count;
        if (missing > 0)
        {
            Defense -= missing * 3; Midfield -= missing * 3; Attack -= missing * 3;
        }
    }
}

public enum MatchEventType { KickOff, Goal, Save, Blocked, Wide, YellowCard, RedCard, HalfTime, FullTime, Injury }

public class MatchEvent
{
    public int tick;            // 0,1,2... one per simulated minute, used to pace the replay
    public string clock;        // "23'", "45+2'", "HT", "FT"
    public MatchEventType type;
    public int side;            // 0 = home, 1 = away, -1 = neither
    public string player;
    public string playerId;     // SimPlayer.id (null for filler players)
    public int matchesOut;      // Injury: matches missed. RedCard: ban length (1 for a second yellow, 2 for a straight red)
    public string text;
}

public class MatchResult
{
    public SimTeam home;
    public SimTeam away;
    public int[] goals = new int[2];
    public int[] shots = new int[2];
    public int[] onTarget = new int[2];
    public int[] yellows = new int[2];
    public int[] reds = new int[2];
    public int[] possession = new int[2];   // percent
    public List<MatchEvent> events = new List<MatchEvent>();

    // 0 = home won, 1 = away won, -1 = draw
    public int Winner { get { return goals[0] > goals[1] ? 0 : (goals[1] > goals[0] ? 1 : -1); } }
    public string ScoreLine { get { return home.name + " " + goals[0] + " - " + goals[1] + " " + away.name; } }
}

public static class MatchEngine
{
    // Tuning knobs. Change these to make matches higher or lower scoring.
    public const double HomeAdvantage = 2.0;        // rating points for the home side
    public const double ShotsPerMinute = 0.135;     // per team, for evenly matched sides
    public const double GoalBase = 0.105;           // chance a typical shot is a goal
    public const double GoalOffset = 0.0;           // shift so equal attack/keeper gives GoalBase
    public const double YellowPerMinute = 0.019;    // per team
    public const double RedPerMinute = 0.0001;      // per team, straight red
    public const double InjuryPerMinute = 0.0016;   // per team: about 0.15 injuries per match
    public const double InjuryLinePenalty = 3.0;    // rating points a team loses for each injured player (a weaker sub comes on)

    private static readonly string[] GoalLines =
    {
        "GOAL! {0} scores for {1}!", "{0} finds the net! {1} score!",
        "{0} fires it home for {1}!", "What a finish from {0}! {1} score!"
    };
    private static readonly string[] SaveLines =
    {
        "{0} shoots, but {1} makes the save.", "Great save by {1} to deny {0}!",
        "{1} gets down well to stop {0}'s effort."
    };
    private static readonly string[] BlockedLines =
    {
        "{0}'s shot is blocked.", "{0} tries his luck but a defender gets in the way."
    };
    private static readonly string[] WideLines =
    {
        "{0} fires wide.", "{0} drags the shot just past the post.", "{0} puts it over the bar."
    };
    private static readonly string[] YellowLines =
    {
        "Yellow card for {0} ({1}).", "{0} ({1}) goes into the book."
    };

    public static MatchResult Simulate(SimTeam home, SimTeam away)
    {
        return Simulate(home, away, Environment.TickCount);
    }

    // The same teams and the same seed always give the same match.
    public static MatchResult Simulate(SimTeam home, SimTeam away, int seed)
    {
        home.Recalculate();
        away.Recalculate();
        Random rng = new Random(seed);
        SimTeam[] team = { home, away };
        MatchResult res = new MatchResult { home = home, away = away };

        int[] redCount = new int[2];
        Dictionary<string, int>[] yellowCount = { new Dictionary<string, int>(), new Dictionary<string, int>() };
        HashSet<string>[] sentOff = { new HashSet<string>(), new HashSet<string>() };   // players no longer on the pitch (sent off or injured)
        int[] weakened = new int[2];
        double possessionSum = 0;

        // Build the list of minutes, including stoppage time.
        List<string> labels = new List<string>();
        int extra1 = rng.Next(1, 4);
        int extra2 = rng.Next(2, 6);
        for (int m = 1; m <= 45; m++) labels.Add(m + "'");
        for (int k = 1; k <= extra1; k++) labels.Add("45+" + k + "'");
        int firstHalf = labels.Count;
        for (int m = 46; m <= 90; m++) labels.Add(m + "'");
        for (int k = 1; k <= extra2; k++) labels.Add("90+" + k + "'");

        AddEvent(res, 0, "0'", MatchEventType.KickOff, -1, null, "Kick off: " + home.name + " v " + away.name + ".");

        for (int i = 0; i < labels.Count; i++)
        {
            int tick = i + 1;
            string clock = labels[i];
            if (i == firstHalf)
            {
                AddEvent(res, tick, "HT", MatchEventType.HalfTime, -1, null,
                    "Half time: " + home.name + " " + res.goals[0] + " - " + res.goals[1] + " " + away.name + ".");
            }

            double homeMid = home.Midfield + HomeAdvantage - 6 * redCount[0] - InjuryLinePenalty * weakened[0];
            double awayMid = away.Midfield - 6 * redCount[1] - InjuryLinePenalty * weakened[1];
            double share = 1.0 / (1.0 + Math.Exp(-(homeMid - awayMid) / 60.0));
            possessionSum += share;
            double[] shareOf = { share, 1.0 - share };

            for (int s = 0; s < 2; s++)
            {
                int o = 1 - s;
                double attack = team[s].Attack - 5 * redCount[s] - InjuryLinePenalty * weakened[s];
                double defenceOpp = team[o].Defense - 5 * redCount[o] - InjuryLinePenalty * weakened[o];
                double factor = Clamp(Math.Exp((attack - defenceOpp) / 70.0), 0.5, 2.2);
                double rate = ShotsPerMinute * Clamp(2 * shareOf[s], 0.3, 1.7) * factor;
                if (rng.NextDouble() >= rate) continue;

                res.shots[s]++;
                SimPlayer shooter = PickShooter(team[s], rng, sentOff[s]);
                SimPlayer keeper = FindKeeper(team[o]);
                string keeperName = keeper != null ? keeper.name : "the keeper";
                double pGoal = Clamp(GoalBase * Math.Exp((attack - team[o].Keeper - GoalOffset) / 55.0), 0.03, 0.35);
                double r = rng.NextDouble();
                if (r < pGoal)
                {
                    res.shots[s] += 0; res.onTarget[s]++; res.goals[s]++;
                    AddEvent(res, tick, clock, MatchEventType.Goal, s, shooter.name,
                        string.Format(Pick(GoalLines, rng), shooter.name, team[s].name));
                }
                else if (r < pGoal + 0.22)
                {
                    res.onTarget[s]++;
                    AddEvent(res, tick, clock, MatchEventType.Save, s, shooter.name,
                        string.Format(Pick(SaveLines, rng), shooter.name, keeperName));
                }
                else if (r < pGoal + 0.44)
                {
                    AddEvent(res, tick, clock, MatchEventType.Blocked, s, shooter.name,
                        string.Format(Pick(BlockedLines, rng), shooter.name));
                }
                else
                {
                    AddEvent(res, tick, clock, MatchEventType.Wide, s, shooter.name,
                        string.Format(Pick(WideLines, rng), shooter.name));
                }
            }

            // Injuries: the player leaves the pitch, a weaker substitute comes on, and he misses some matches.
            for (int s = 0; s < 2; s++)
            {
                if (rng.NextDouble() >= InjuryPerMinute) continue;
                SimPlayer hurt = PickOutfielder(team[s], rng, sentOff[s]);
                if (hurt == null) continue;
                sentOff[s].Add(hurt.name);
                weakened[s]++;
                int out_ = InjuryLength(rng);
                MatchEvent ev = AddEvent(res, tick, clock, MatchEventType.Injury, s, hurt.name,
                    hurt.name + " (" + team[s].name + ") goes down injured and has to come off. Expected to miss " + out_ + (out_ == 1 ? " match." : " matches."));
                ev.playerId = hurt.id;
                ev.matchesOut = out_;
            }

            // Cards.
            for (int s = 0; s < 2; s++)
            {
                double c = rng.NextDouble();
                if (c >= RedPerMinute + YellowPerMinute) continue;
                SimPlayer culprit = PickOutfielder(team[s], rng, sentOff[s]);
                if (culprit == null) continue;
                // Players already on a yellow play more carefully: usually pick someone else.
                if (yellowCount[s].ContainsKey(culprit.name) && rng.NextDouble() < 0.75)
                {
                    culprit = PickOutfielder(team[s], rng, sentOff[s]) ?? culprit;
                }

                if (c < RedPerMinute)
                {
                    SendOff(res, team, s, culprit, tick, clock, redCount, sentOff, "RED CARD! {0} ({1}) is sent off!", 2);
                    continue;
                }

                res.yellows[s]++;
                int already;
                yellowCount[s].TryGetValue(culprit.name, out already);
                yellowCount[s][culprit.name] = already + 1;
                if (already + 1 >= 2)
                {
                    SendOff(res, team, s, culprit, tick, clock, redCount, sentOff, "Second yellow! {0} ({1}) is sent off!", 1);
                }
                else
                {
                    AddEvent(res, tick, clock, MatchEventType.YellowCard, s, culprit.name,
                        string.Format(Pick(YellowLines, rng), culprit.name, team[s].name)).playerId = culprit.id;
                }
            }
        }

        res.possession[0] = (int)Math.Round(possessionSum / labels.Count * 100.0);
        res.possession[1] = 100 - res.possession[0];
        AddEvent(res, labels.Count + 1, "FT", MatchEventType.FullTime, -1, null, "Full time: " + res.ScoreLine + ".");
        return res;
    }

    private static void SendOff(MatchResult res, SimTeam[] team, int s, SimPlayer p, int tick, string clock,
        int[] redCount, HashSet<string>[] sentOff, string format, int banMatches)
    {
        redCount[s]++;
        res.reds[s]++;
        sentOff[s].Add(p.name);
        MatchEvent ev = AddEvent(res, tick, clock, MatchEventType.RedCard, s, p.name, string.Format(format, p.name, team[s].name));
        ev.playerId = p.id;
        ev.matchesOut = banMatches;
    }

    private static MatchEvent AddEvent(MatchResult res, int tick, string clock, MatchEventType type, int side, string player, string text)
    {
        MatchEvent ev = new MatchEvent { tick = tick, clock = clock, type = type, side = side, player = player, text = text };
        res.events.Add(ev);
        return ev;
    }

    // Matches missed: usually 1-2, sometimes a long injury.
    private static int InjuryLength(Random rng)
    {
        int r = rng.Next(100);
        if (r < 40) return 1;
        if (r < 65) return 2;
        if (r < 80) return 3;
        if (r < 95) return rng.Next(4, 7);
        return rng.Next(7, 11);
    }

    private static SimPlayer FindKeeper(SimTeam t)
    {
        foreach (SimPlayer p in t.players) if (p.position == "GK") return p;
        return null;
    }

    private static SimPlayer PickOutfielder(SimTeam t, Random rng, HashSet<string> sentOff)
    {
        List<SimPlayer> pool = new List<SimPlayer>();
        foreach (SimPlayer p in t.players)
        {
            if (p.position != "GK" && !sentOff.Contains(p.name)) pool.Add(p);
        }
        return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
    }

    // Forwards shoot most, then attacking midfielders; better players shoot a bit more.
    private static SimPlayer PickShooter(SimTeam t, Random rng, HashSet<string> sentOff)
    {
        double total = 0;
        List<SimPlayer> pool = new List<SimPlayer>();
        List<double> weights = new List<double>();
        foreach (SimPlayer p in t.players)
        {
            if (sentOff.Contains(p.name)) continue;
            double w = ShotWeight(p.position) * (p.rating / 100.0);
            if (w <= 0) continue;
            pool.Add(p); weights.Add(w); total += w;
        }
        if (pool.Count == 0)
        {
            SimPlayer any = PickOutfielder(t, rng, sentOff);
            return any ?? new SimPlayer { name = "A player", position = "ST", rating = 60 };
        }
        double roll = rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return pool[i];
        }
        return pool[pool.Count - 1];
    }

    private static double ShotWeight(string pos)
    {
        switch (pos)
        {
            case "ST": return 4.0;
            case "LW": case "RW": return 3.0;
            case "CAM": return 2.5;
            case "CM": return 1.0;
            case "CDM": return 0.5;
            case "LB": case "RB": return 0.5;
            case "CB": return 0.4;
            default: return 0;
        }
    }

    private static string Pick(string[] lines, Random rng) { return lines[rng.Next(lines.Length)]; }

    private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
}
