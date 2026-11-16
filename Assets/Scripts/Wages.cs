using System;

// Players are paid every season: 12% of his market value, taken when the season ends.
// If the club cannot cover the bill it pays what it has and the rest is not carried over.
public static class Wages
{
    public const double Share = 0.12;

    public static long WageOf(int rating)
    {
        long w = (long)(Math.Round(PlayerCatalog.ValueOf(rating) * Share / 10000.0) * 10000);
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
