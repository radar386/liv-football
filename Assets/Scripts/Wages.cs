using System;

// Players are paid every season, by rating (Unity scale 55-115): about 6M at 80, 36M at 100, 88M at 110.
// The money is taken when the season ends. A club that cannot cover the bill pays what it has;
// nothing is carried over. Same curve as backend/online_backend/wages.py.
public static class Wages
{
    public static long WageOf(int rating)
    {
        long w = (long)(Math.Round(1_000_000 * Math.Exp((rating - 60) * 0.09) / 10000.0) * 10000);
        return Math.Max(10000, w);
    }

    public static long Bill(UserProfile p)
    {
        long total = 0;
        foreach (ClubSquadPlayer x in p.squad) total += WageOf(x.rating);
        return total;
    }

    // Returns how much was actually paid.
    public static long Pay(UserProfile p)
    {
        long paid = Math.Min(Math.Max(0, p.money), Bill(p));
        p.money -= paid;
        return paid;
    }
}
