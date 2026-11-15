using UnityEngine;

public static class Negotiation
{
    // Tune these two numbers to make the game easier or harder.
    public const float MinOfferRatio = 0.5f;  // offers below 50% of the asking price always fail
    public const float MaxChance = 0.9f;      // chance of "yes" when offering the full asking price

    // Chance of a yes: 0% at half the price, rising in a straight line to 90% at the full price.
    // Examples: 60% of price -> 18%, 75% of price -> 45%, 90% of price -> 72%.
    public static float GetChance(long offer, long price)
    {
        if (price <= 0) return 0f;
        float ratio = (float)offer / price;
        if (ratio < MinOfferRatio) return 0f;
        float t = (ratio - MinOfferRatio) / (1f - MinOfferRatio);
        return Mathf.Clamp01(t) * MaxChance;
    }

    // The calculator trick, with weighted odds: the game "rolls" and the result
    // is either 1 (yes, deal accepted) or 2 (no, refused). A bigger offer makes 1 more likely.
    public static bool Roll(long offer, long price)
    {
        float chance = GetChance(offer, price);
        return Random.value < chance;
    }
}
