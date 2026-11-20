using System;
using System.Collections.Generic;

// Link-up play (DRAFT): team chemistry like eFootball. Neighbouring players on the pitch are linked.
// Same club = strong link (3 points), same card tier = medium (2), anything else = weak (1).
// Chemistry is 0-100 %; the bonus is up to +2.5 rating on every player of the team (rounded), like a small coach boost.
// The web game draws the links as lines on the pitch; this is the same maths for the Unity match engine.
public static class LinkUp
{
    public const double MaxBonus = 2.5;
    private static Dictionary<string, string> clubByName;

    public class Link { public int a; public int b; public int points; }

    public static int Tier(int rating) { return rating < 65 ? 0 : rating < 75 ? 1 : rating < 85 ? 2 : 3; }

    private static string ClubOfName(string name)
    {
        if (clubByName == null)
        {
            clubByName = new Dictionary<string, string>();
            foreach (Footballer f in PlayerCatalog.Create()) clubByName[f.playerName] = PlayerCatalog.ClubOf(f.id);
        }
        string c;
        return name != null && clubByName.TryGetValue(name, out c) ? c : "";
    }

    private static int GroupOrder(string pos)
    {
        if (pos == "GK") return 0;
        if (pos == "CB" || pos == "LB" || pos == "RB") return 1;
        if (pos == "LW" || pos == "RW" || pos == "ST") return 3;
        return 2;
    }

    // The links of an 11-player team, plus the chemistry percentage.
    public static List<Link> Compute(SimTeam team, out int percent)
    {
        percent = 0;
        List<Link> links = new List<Link>();
        if (team == null || team.players.Count != 11) return links;
        int d, m, f;
        SimTeam.ParseFormation(team.formation, out d, out m, out f);
        if (1 + d + m + f != 11) return links;

        // order: goalkeeper, defenders, midfielders, forwards (stable)
        List<SimPlayer> ordered = new List<SimPlayer>(team.players);
        for (int i = 1; i < ordered.Count; i++)
        {
            SimPlayer x = ordered[i]; int j = i - 1;
            while (j >= 0 && GroupOrder(ordered[j].position) > GroupOrder(x.position)) { ordered[j + 1] = ordered[j]; j--; }
            ordered[j + 1] = x;
        }
        int[] rows = { 1, d, m, f };
        List<int>[] rowIdx = new List<int>[4];
        double[] xs = new double[11];
        int at = 0;
        for (int r = 0; r < 4; r++)
        {
            rowIdx[r] = new List<int>();
            for (int k = 0; k < rows[r]; k++) { rowIdx[r].Add(at); xs[at] = (k + 0.5) / rows[r]; at++; }
        }
        HashSet<string> seen = new HashSet<string>();
        Action<int, int> add = (a, b) =>
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            if (!seen.Add(lo + "-" + hi)) return;
            string ca = ClubOfName(ordered[lo].name), cb = ClubOfName(ordered[hi].name);
            int pts = ca.Length > 0 && ca == cb ? 3 : Tier(ordered[lo].rating) == Tier(ordered[hi].rating) ? 2 : 1;
            links.Add(new Link { a = lo, b = hi, points = pts });
        };
        for (int r = 0; r < 4; r++)
        {
            List<int> row = rowIdx[r];
            for (int k = 0; k + 1 < row.Count; k++) add(row[k], row[k + 1]);
            if (r == 0) continue;
            foreach (int q in row)
            {
                List<int> prev = new List<int>(rowIdx[r - 1]);
                prev.Sort((u, v) => Math.Abs(xs[u] - xs[q]).CompareTo(Math.Abs(xs[v] - xs[q])));
                add(q, prev[0]);
                if (prev.Count > 1 && Math.Abs(xs[prev[1]] - xs[q]) <= Math.Abs(xs[prev[0]] - xs[q]) + 0.08) add(q, prev[1]);
            }
        }
        int total = 0; foreach (Link l in links) total += l.points;
        percent = links.Count == 0 ? 0 : (int)Math.Round(total * 100.0 / (3 * links.Count));
        return links;
    }

    public static int Percent(SimTeam team) { int p; Compute(team, out p); return p; }

    public static int BonusFor(int percent) { return (int)Math.Round(percent / 100.0 * MaxBonus); }

    // Adds the link-up bonus to every player's rating and remembers the chemistry on the team.
    public static void Apply(SimTeam team)
    {
        int pct = Percent(team);
        team.linkUp = pct;
        int bonus = BonusFor(pct);
        if (bonus > 0) foreach (SimPlayer s in team.players) s.rating += bonus;
    }
}
