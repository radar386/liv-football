using System;
using System.Collections.Generic;

// League mode (plain C#): every user club plays every other club once per season.
// The state is saved inside the save file, so a half-played season survives closing the game.

[Serializable]
public class LeagueFixture
{
    public int matchday;       // 0-based
    public string home;        // username
    public string away;        // username
    public bool played;
    public int homeGoals;
    public int awayGoals;
}

[Serializable]
public class LeagueState
{
    public int season = 1;
    public int nextMatchday;
    public bool prizesPaid;
    public List<string> participants = new List<string>();
    public List<LeagueFixture> fixtures = new List<LeagueFixture>();
}

public class TableRow
{
    public string username;
    public string club;
    public int played, won, drawn, lost, goalsFor, goalsAgainst, points;
    public int GoalDifference { get { return goalsFor - goalsAgainst; } }
}

public static class LeagueEngine
{
    // Prize money (same money as the shops).
    public const long WinBonus = 10_000_000;
    public const long DrawBonus = 3_000_000;
    // Injuries, suspensions and bans from the matchday just played (LeagueUI shows them).
    public static List<string> LastNews = new List<string>();

    public static readonly long[] PlacePrizes = { 200_000_000, 120_000_000, 80_000_000 };   // 1st, 2nd, 3rd

    // Round robin with the circle method: with N clubs there are N-1 matchdays (N odd: N matchdays, one rests).
    public static LeagueState Create(List<UserProfile> profiles, int season)
    {
        LeagueState state = new LeagueState { season = season };
        foreach (UserProfile p in profiles) state.participants.Add(p.username);

        List<string> teams = new List<string>(state.participants);
        if (teams.Count % 2 == 1) teams.Add(null);   // null = rest day
        int n = teams.Count;
        for (int round = 0; round < n - 1; round++)
        {
            for (int i = 0; i < n / 2; i++)
            {
                string a = teams[i], b = teams[n - 1 - i];
                if (a == null || b == null) continue;
                // swap home/away now and then so nobody is always at home
                bool swap = (round + i) % 2 == 1;
                state.fixtures.Add(new LeagueFixture { matchday = round, home = swap ? b : a, away = swap ? a : b });
            }
            // keep teams[0] fixed, rotate the rest
            string last = teams[n - 1];
            teams.RemoveAt(n - 1);
            teams.Insert(1, last);
        }
        return state;
    }

    public static int TotalMatchdays(LeagueState s)
    {
        int max = -1;
        foreach (LeagueFixture f in s.fixtures) if (f.matchday > max) max = f.matchday;
        return max + 1;
    }

    public static bool IsFinished(LeagueState s) { return s.nextMatchday >= TotalMatchdays(s); }

    // Plays every match of the next matchday and returns them.
    public static List<LeagueFixture> PlayMatchday(LeagueState s, List<UserProfile> profiles, Random rng)
    {
        List<LeagueFixture> played = new List<LeagueFixture>();
        LastNews = new List<string>();
        if (IsFinished(s)) return played;

        foreach (LeagueFixture f in s.fixtures)
        {
            if (f.matchday != s.nextMatchday || f.played) continue;
            UserProfile home = Find(profiles, f.home), away = Find(profiles, f.away);
            if (home == null || away == null) { f.played = true; continue; }

            MatchResult r = MatchEngine.Simulate(TeamBuilder.FromProfile(home), TeamBuilder.FromProfile(away), rng.Next());
            f.homeGoals = r.goals[0];
            f.awayGoals = r.goals[1];
            f.played = true;
            played.Add(f);
            home.money += FacilitiesEngine.MatchdayIncome(home);   // gate receipts for the home club
            if (r.goals[0] > r.goals[1]) home.leagueWins++; else if (r.goals[1] > r.goals[0]) away.leagueWins++;
            LastNews.AddRange(Availability.ApplyMatch(home, r, 0));
            LastNews.AddRange(Availability.ApplyMatch(away, r, 1));
        }
        s.nextMatchday++;
        return played;
    }

    public static List<TableRow> Table(LeagueState s, List<UserProfile> profiles)
    {
        Dictionary<string, TableRow> rows = new Dictionary<string, TableRow>();
        foreach (string u in s.participants)
        {
            UserProfile p = Find(profiles, u);
            rows[u] = new TableRow { username = u, club = p != null ? p.clubName : u };
        }
        foreach (LeagueFixture f in s.fixtures)
        {
            if (!f.played || !rows.ContainsKey(f.home) || !rows.ContainsKey(f.away)) continue;
            TableRow h = rows[f.home], a = rows[f.away];
            h.played++; a.played++;
            h.goalsFor += f.homeGoals; h.goalsAgainst += f.awayGoals;
            a.goalsFor += f.awayGoals; a.goalsAgainst += f.homeGoals;
            if (f.homeGoals > f.awayGoals) { h.won++; h.points += 3; a.lost++; }
            else if (f.homeGoals < f.awayGoals) { a.won++; a.points += 3; h.lost++; }
            else { h.drawn++; a.drawn++; h.points++; a.points++; }
        }
        List<TableRow> table = new List<TableRow>(rows.Values);
        table.Sort((x, y) =>
        {
            if (x.points != y.points) return y.points.CompareTo(x.points);
            if (x.GoalDifference != y.GoalDifference) return y.GoalDifference.CompareTo(x.GoalDifference);
            if (x.goalsFor != y.goalsFor) return y.goalsFor.CompareTo(x.goalsFor);
            return string.Compare(x.club, y.club, StringComparison.OrdinalIgnoreCase);
        });
        return table;
    }

    // Pays win/draw bonuses and the top 3 place prizes. Only once per season. Returns lines to show.
    public static List<string> PayPrizes(LeagueState s, List<UserProfile> profiles)
    {
        List<string> lines = new List<string>();
        if (s.prizesPaid || !IsFinished(s)) return lines;
        s.prizesPaid = true;

        List<TableRow> table = Table(s, profiles);
        for (int i = 0; i < table.Count; i++)
        {
            TableRow row = table[i];
            UserProfile p = Find(profiles, row.username);
            if (p == null) continue;
            long prize = row.won * WinBonus + row.drawn * DrawBonus;
            if (i == 0) p.leagueTitles++;
            if (i < PlacePrizes.Length) prize += PlacePrizes[i];
            p.money += prize;
            if (i < 3) lines.Add((i + 1) + ". " + row.club + " (" + row.points + " pts) wins " + prize.ToString("N0"));
        }
        return lines;
    }

    // Starts the next season for everybody: contracts, sponsors and scouts roll over, and a new fixture list is made.
    public static LeagueState StartNextSeason(LeagueState old, List<UserProfile> profiles)
    {
        foreach (UserProfile p in profiles) SeasonManager.NewSeason(p);
        return Create(profiles, old.season + 1);
    }

    private static UserProfile Find(List<UserProfile> profiles, string username)
    {
        foreach (UserProfile p in profiles) if (p.username == username) return p;
        return null;
    }
}
