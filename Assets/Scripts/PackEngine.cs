using System;
using System.Collections.Generic;

// Pack opening in the style of FC Mobile (plain C#): several pack types, each with its own price,
// odds and guaranteed minimum rating. Open one (x1) or ten (x10) at a time.
// Only the game's own money is used: there is no real-money purchase here.
public class PackPull
{
    public Footballer player;
    public string tier;        // Bronze, Silver, Gold or Elite
    public bool duplicate;     // you already owned him
    public long refund;        // cash you got back for a duplicate
    public int seasons;        // contract length of a new player (1-4)
}

public class PackType
{
    public string name;
    public long price;          // price of ONE pack
    public int minRating;       // every player in this pack is at least this good
    public int[] chances;       // percent chance of Bronze, Silver, Gold, Elite (add up to 100)
    public int guaranteeTier;   // an x10 always contains at least one player of this tier or better (0 Bronze .. 3 Elite)
    public string description;
}

public static class PackEngine
{
    public const double DuplicateRefundShare = 0.30;   // a duplicate pays back 30% of his market value

    public static readonly string[] TierNames = { "Bronze", "Silver", "Gold", "Elite" };

    // Ratings per tier: Bronze below 80, Silver 80-94, Gold 95-104, Elite 105 and up.
    public static readonly int[] TierMinRating = { 0, 80, 95, 105 };

    public static readonly PackType[] Types =
    {
        new PackType { name = "Standard Pack", price = 2_000_000,  minRating = 0,  chances = new[] { 80, 18, 2, 0 },  guaranteeTier = 1,
            description = "Cheap pack. A x10 always has a Silver or better." },
        new PackType { name = "Premium Pack",  price = 10_000_000, minRating = 80, chances = new[] { 0, 75, 22, 3 },  guaranteeTier = 2,
            description = "Every player is 80+. A x10 always has a Gold or better." },
        new PackType { name = "Elite Pack",    price = 40_000_000, minRating = 95, chances = new[] { 0, 0, 85, 15 }, guaranteeTier = 3,
            description = "Every player is 95+. A x10 always has an Elite." },
    };

    public static string TierOf(int rating)
    {
        for (int t = 3; t >= 1; t--) if (rating >= TierMinRating[t]) return TierNames[t];
        return TierNames[0];
    }

    public static int TierIndex(int rating)
    {
        for (int t = 3; t >= 1; t--) if (rating >= TierMinRating[t]) return t;
        return 0;
    }

    // ---- packs you own (for example given by an admin): opening them is free ----

    public static int TypeIndex(PackType type) { return Array.IndexOf(Types, type); }

    public static int Owned(UserProfile p, int typeIndex)
    {
        return typeIndex >= 0 && typeIndex < p.ownedPacks.Count ? p.ownedPacks[typeIndex] : 0;
    }

    public static void AddOwned(UserProfile p, int typeIndex, int count)
    {
        while (p.ownedPacks.Count < Types.Length) p.ownedPacks.Add(0);
        p.ownedPacks[typeIndex] = Math.Max(0, p.ownedPacks[typeIndex] + count);
    }

    // Opens `count` packs you already own. Returns null if you do not own that many.
    public static List<PackPull> OpenOwned(UserProfile p, PackType type, int count, Random rng)
    {
        int index = TypeIndex(type);
        if (count < 1 || Owned(p, index) < count) return null;
        p.ownedPacks[index] -= count;
        return OpenCore(p, type, count, rng);
    }

    public static long Cost(PackType type, int count) { return type.price * count; }

    public static bool CanAfford(UserProfile p, PackType type, int count) { return p.money >= Cost(type, count); }

    // Opens `count` packs of one type (1 or 10). Returns null if you cannot afford it.
    public static List<PackPull> Open(UserProfile p, PackType type, int count, Random rng)
    {
        if (count < 1 || !CanAfford(p, type, count)) return null;
        p.money -= Cost(type, count);
        return OpenCore(p, type, count, rng);
    }

    // The actual draw (money or owned packs have already been taken).
    private static List<PackPull> OpenCore(UserProfile p, PackType type, int count, Random rng)
    {
        List<Footballer> all = PlayerCatalog.Create();
        List<Footballer> picks = new List<Footballer>();
        for (int i = 0; i < count; i++) picks.Add(RandomPlayer(all, type, RollTier(type, rng), rng));

        // x10 guarantee: if nobody reached the guaranteed tier, the last pull is re-drawn from that tier.
        if (count >= 10)
        {
            bool ok = false;
            foreach (Footballer f in picks) if (TierIndex(f.rating) >= type.guaranteeTier) { ok = true; break; }
            if (!ok) picks[picks.Count - 1] = RandomPlayer(all, type, type.guaranteeTier, rng);
        }

        List<PackPull> pulls = new List<PackPull>();
        foreach (Footballer f in picks)
        {
            PackPull pull = new PackPull { player = f, tier = TierOf(f.rating) };
            if (TeamBuilder.FindSquadPlayer(p, f.id) != null)
            {
                pull.duplicate = true;
                pull.refund = (long)(f.marketValue * DuplicateRefundShare);
                p.money += pull.refund;
            }
            else
            {
                p.squad.Add(new ClubSquadPlayer { id = f.id, playerName = f.playerName, position = f.position, rating = f.rating, hasRating = true });
                pull.seasons = AcademyScout.AddBoughtContract(p, f.id, rng);
            }
            pulls.Add(pull);
        }
        return pulls;
    }

    private static int RollTier(PackType type, Random rng)
    {
        int roll = rng.Next(100), sum = 0;
        for (int t = 0; t < 4; t++)
        {
            sum += type.chances[t];
            if (roll < sum) return t;
        }
        return 3;
    }

    private static Footballer RandomPlayer(List<Footballer> all, PackType type, int tier, Random rng)
    {
        List<Footballer> pool = new List<Footballer>();
        foreach (Footballer f in all)
            if (TierIndex(f.rating) == tier && f.rating >= type.minRating) pool.Add(f);
        if (pool.Count == 0)   // nobody in that tier: take anyone at or above the pack's minimum
            foreach (Footballer f in all) if (f.rating >= type.minRating) pool.Add(f);
        if (pool.Count == 0) pool = all;
        return pool[rng.Next(pool.Count)];
    }
}
