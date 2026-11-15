using System;
using System.Collections.Generic;

// Player growth (plain C#): at the start of every season your coach trains the squad and players get better for good.
//  - A better training ground adds up to +3 possible points on top (see FacilitiesEngine).
//  - The better the coach, the bigger the possible gain: elite coach (+3 tier) 0 to 3, very good 0 to 2, good 0 to 1, no coach 0.
//  - Scouted academy players are young: they grow faster (up to 2 extra points) until they reach their potential.
//  - Stars slow down: 105+ players gain at most 1 point, 115+ players do not grow.
public class TrainingGain
{
    public string playerName;
    public int from;
    public int to;
    public int Gain { get { return to - from; } }
}

public static class TrainingEngine
{
    public const int YouthBonus = 2;           // extra possible points for scouted academy players
    public const int SlowDownFrom = 105;       // from this rating a player gains at most 1
    public const int NoGrowthFrom = 115;       // from this rating a player stops growing

    // Applies one season of training to the whole squad. Returns everybody who improved, best first.
    public static List<TrainingGain> Apply(UserProfile p, Random rng)
    {
        List<TrainingGain> gains = new List<TrainingGain>();
        int tier = 0;
        if (!string.IsNullOrEmpty(p.coachId))
        {
            CoachOffer coach = CoachCatalog.Find(p.coachId);
            if (coach != null) tier = TeamBuilder.CoachBoost(coach.rating);
        }

        foreach (ClubSquadPlayer c in p.squad)
        {
            int rating = c.hasRating ? c.rating : 70;
            if (rating >= NoGrowthFrom) continue;

            PlayerPotential pot = null;
            foreach (PlayerPotential x in p.potentials) if (x.playerId == c.id) { pot = x; break; }

            int training = tier + FacilitiesEngine.TrainingBonus(p);    // coach + training ground
            int maxGain = training + (pot != null ? YouthBonus : 0);
            if (training == 0 && pot == null) continue;                       // no coach and not a youngster: no training
            if (rating >= SlowDownFrom) maxGain = Math.Min(maxGain, 1);
            if (maxGain <= 0) continue;

            int gain = rng.Next(0, maxGain + 1);
            if (pot != null) gain = Math.Min(gain, Math.Max(0, pot.potential - rating));   // never above potential
            if (gain <= 0) continue;

            c.rating = rating + gain;
            c.hasRating = true;
            gains.Add(new TrainingGain { playerName = c.playerName, from = rating, to = rating + gain });
        }
        gains.Sort((a, b) => b.Gain.CompareTo(a.Gain));
        return gains;
    }
}
