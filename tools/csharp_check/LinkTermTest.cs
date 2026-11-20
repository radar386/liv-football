using System;
using System.Collections.Generic;
public static class LinkTermTest
{
    static int fails;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "ok   " : "FAIL ") + what); if (!ok) fails++; }
    public static int Main()
    {
        string[] pos = { "GK", "LB", "CB", "CB", "RB", "CM", "CM", "CM", "LW", "ST", "RW" };
        SimTeam t = new SimTeam { name = "T", formation = "4-3-3" };
        for (int i = 0; i < 11; i++) t.players.Add(new SimPlayer { id = "p" + i, name = "Nobody" + i, position = pos[i], rating = 80 });
        int pct; var links = LinkUp.Compute(t, out pct);
        Check(links.Count > 10, "links built: " + links.Count);
        Check(pct == 67, "same tier, no club = 2/3 -> 67% (got " + pct + ")");
        LinkUp.Apply(t);
        Check(t.linkUp == pct && t.players[0].rating == 82, "bonus +2 applied (got " + t.players[0].rating + ")");
        Check(CoachCatalog.ScalePrice(25000000) == 100000000, "cheapest coach 100M");
        Check(CoachCatalog.ScalePrice(500000000) == 4000000000, "dearest coach 4B (got " + CoachCatalog.ScalePrice(500000000) + ")");
        Random r = new Random(1); bool inRange = true;
        for (int i = 0; i < 500; i++) { long f = ContractTermination.RollFee(r); if (f < 0 || f > 100000000 || f % 1000000 != 0) inRange = false; }
        Check(inRange, "termination fee 0..100M in whole millions");
        Console.WriteLine(fails == 0 ? "ALL OK" : fails + " FAILED");
        return fails;
    }
}
