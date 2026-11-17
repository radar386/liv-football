using System;
using System.Collections.Generic;
public class ClubSquadPlayer { public string id, playerName, position; public int rating; public bool hasRating; }
public class UserProfile { public long money = 500000000000; public List<ClubSquadPlayer> squad = new List<ClubSquadPlayer>(); public List<int> ownedPacks = new List<int>(); }
public static class TeamBuilder { public static ClubSquadPlayer FindSquadPlayer(UserProfile p, string id) { return p.squad.Find(x => x.id == id); } }
public static class AcademyScout { public static int AddBoughtContract(UserProfile p, string id, Random r) { return 1 + r.Next(4); } }
public static class T
{
    public static int Main()
    {
        int fails = 0;
        var all = PlayerCatalog.Create();
        Console.WriteLine("players " + all.Count + " clubs-of-first " + PlayerCatalog.ClubOf(all[0].id));
        if (all.Count != 1034) fails++;
        var coaches = CoachCatalog.Create(); var ids = new HashSet<string>();
        foreach (var c in coaches) if (!ids.Add(c.id)) { Console.WriteLine("dup coach " + c.id); fails++; }
        Console.WriteLine("coaches " + coaches.Count);
        var rng = new Random(7);
        foreach (var t in PackEngine.Types)
        {
            var p = new UserProfile();
            int minEf = 999, hit = 0;
            for (int n = 0; n < 40; n++)
            {
                var pulls = PackEngine.Open(p, t, 10, rng);
                if (pulls == null) { Console.WriteLine("cannot open " + t.name); fails++; break; }
                bool g = false;
                foreach (var pl in pulls)
                {
                    if (pl.player.rating < t.minRating) { Console.WriteLine("below min " + t.name); fails++; }
                    if (t.eFootball) { int ef = RatingScale.ToEFootball(pl.player.rating); minEf = Math.Min(minEf, ef); if (ef >= t.efGuarantee) g = true; }
                    else if (PackEngine.TierIndex(pl.player.rating) >= t.guaranteeTier) g = true;
                }
                if (g) hit++;
            }
            Console.WriteLine(t.name + ": x10 guarantee held " + hit + "/40" + (t.eFootball ? ", lowest eF " + minEf : ""));
            if (hit != 40) fails++;
        }
        Console.WriteLine("wage 80=" + Wages.WageOf(80) + " 100=" + Wages.WageOf(100) + " 110=" + Wages.WageOf(110));
        Console.WriteLine("scale 115->" + RatingScale.ToWeb(115) + " web, eF " + RatingScale.ToEFootball(115));
        return fails;
    }
}
