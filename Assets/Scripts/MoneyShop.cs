using System;
using System.Collections.Generic;

// Buy in-game money with real money, e.g. $0.99 for 500,000.
// This file holds the bundles and the crediting rules (plain C#). The actual payment is done by an
// IPaymentProvider, so you can plug in Stripe / PayPal / Microsoft Store / Google Play later.
public class MoneyBundle
{
    public string id;
    public string label;
    public decimal priceUsd;
    public long money;
}

public class PaymentResult
{
    public bool success;
    public string transactionId;   // unique id from the payment provider
    public string error;
}

public interface IPaymentProvider
{
    // Starts a payment for the bundle; calls done when the provider confirms or fails.
    void Purchase(MoneyBundle bundle, UserProfile buyer, Action<PaymentResult> done);
}

public static class MoneyShop
{
    public static readonly MoneyBundle[] Bundles =
    {
        new MoneyBundle { id = "money_500k", label = "500,000",     priceUsd = 0.49m,  money = 500_000 },
        new MoneyBundle { id = "money_3m",   label = "3,000,000",   priceUsd = 2.49m,  money = 3_000_000 },
        new MoneyBundle { id = "money_7m",   label = "7,000,000",   priceUsd = 4.99m,  money = 7_000_000 },
        new MoneyBundle { id = "money_20m",  label = "20,000,000",  priceUsd = 12.49m, money = 20_000_000 },
        new MoneyBundle { id = "money_50m",  label = "50,000,000",  priceUsd = 24.99m, money = 50_000_000 },
    };

    public static MoneyBundle Find(string id)
    {
        foreach (MoneyBundle b in Bundles) if (b.id == id) return b;
        return null;
    }

    // Credits the money once per transaction id. Returns false if it was already credited.
    public static bool Credit(UserProfile p, MoneyBundle bundle, string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId)) return false;
        if (p.processedPurchases.Contains(transactionId)) return false;
        p.processedPurchases.Add(transactionId);
        p.money += bundle.money;
        return true;
    }
}

// Test provider: pretends the payment worked. Only use while developing; never ship it.
public class FakePaymentProvider : IPaymentProvider
{
    public void Purchase(MoneyBundle bundle, UserProfile buyer, Action<PaymentResult> done)
    {
        done(new PaymentResult { success = true, transactionId = "test-" + Guid.NewGuid() });
    }
}
