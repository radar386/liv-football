using System;
using System.Collections.Generic;

// Checks own coaches, kits and the live-final script. Run with run_finals_kits.sh.
public static class FinalsKitsTest
{
    static int fails;
    static void Check(bool ok, string what) { if (!ok) { fails++; Console.WriteLine("FAIL: " + what); } }

    public static int Main()
    {
        // Own coaches: free, capped, no bonus.
        UserProfile p = new UserProfile { username = "t", clubName = "Test FC" };
        string problem;
        Check(OwnCoachService.Create(p, "  ", "4-4-2", out problem) == null && problem != null, "blank name refused");
        OwnCoach c = OwnCoachService.Create(p, "Coach Joe", "4-4-2", out problem);
        Check(c != null && OwnCoachService.IsOwn(c.id), "own coach created");
        for (int i = 0; i < 4; i++) OwnCoachService.Create(p, "C" + i, "4-4-2", out problem);
        Check(OwnCoachService.Create(p, "Sixth", "4-4-2", out problem) == null, "sixth coach refused");
        OwnCoachService.Hire(p, c.id);
        CoachOffer o = OwnCoachService.AsOffer(p);
        Check(o != null && TeamBuilder.CoachBoost(o.rating) == 0 && o.price == 0, "own coach gives boost 0");
        OwnCoachService.Delete(p, c.id);
        Check(p.coachId == "" && OwnCoachService.Find(p, c.id) == null, "delete clears hire");

        // Kits: known colours, stable fallback, bad input cleaned.
        Check(KitCatalog.ForName("Liverpool", "x").body == "#C8102E", "liverpool red");
        Check(KitCatalog.ForName("Nobody FC", "a").body == KitCatalog.ForName("Nobody FC", "a").body, "fallback stable");
        ClubKit bad = KitCatalog.Sanitize(new ClubKit { body = "javascript:1", pattern = "<b>" });
        Check(KitCatalog.IsHex(bad.body) && bad.pattern == "solid", "bad kit sanitised");

        // Live finals: draws get a shootout with a winner, wins do not.
        int shoot = 0, ok5 = 0;
        for (int i = 0; i < 40; i++)
        {
            MatchResult r = MatchEngine.Simulate(TeamBuilder.FromClub(TeamBuilder.CatalogClubs()[0]), TeamBuilder.FromClub(TeamBuilder.CatalogClubs()[1]));
            LiveFinalScript s = FinalsEngine.Build(r, i);
            if (r.goals[0] == r.goals[1])
            {
                shoot++;
                Check(s.hasShootout && s.kicks.Count == 10 && s.penalties[s.winner] > s.penalties[1 - s.winner], "shootout has a clear winner");
                if (s.kicks.Count == 10) ok5++;
            }
            else Check(!s.hasShootout && s.winner == (r.goals[0] > r.goals[1] ? 0 : 1), "no shootout when decided");
            int goals = 0; foreach (MatchEvent e in s.goals) goals++;
            Check(goals == r.goals[0] + r.goals[1], "goal events match the score");
        }
        // Cup: five clubs, one winner, a bye, a final, prizes paid once.
        List<UserProfile> ps = new List<UserProfile>();
        List<string> clubs = TeamBuilder.CatalogClubs();
        for (int i = 0; i < 5; i++) ps.Add(new UserProfile { username = "u" + i, clubName = clubs[i], money = 0 });
        CupState cup = CupEngine.Create(ps, 1, new Random(3));
        Check(cup.alive.Count == 5, "five clubs drawn");
        CupEngine.PlayAll(cup, ps, new Random(4));
        Check(CupEngine.IsFinished(cup) && cup.ties.Count == 4, "five-club cup has 4 ties");
        Check(CupEngine.FinalTie(cup) != null && CupEngine.FinalTie(cup).winner == cup.winner, "final decides the winner");
        CupEngine.PlaySuperCup(cup, "u0", "u1", ps, new Random(5));
        Check(cup.superCup != null && cup.superCup.home != cup.superCup.away, "super cup played between two clubs");
        List<string> paid = CupEngine.PayPrizes(cup, ps);
        Check(paid.Count >= 2 && CupEngine.PayPrizes(cup, ps).Count == 0, "prizes paid once");
        Check(CupEngine.Replay(CupEngine.FinalTie(cup), ps).goals[0] == CupEngine.FinalTie(cup).homeGoals, "replay matches stored score");
        Console.WriteLine("Finals/kits/own-coach tests: " + (fails == 0 ? "all passed" : fails + " failed") + " (" + shoot + " shootouts seen)");
        return fails;
    }
}
