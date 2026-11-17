using System.Collections.Generic;

// Sponsors a club can sign. DRAFT written from how the other scripts use it; compare with your own.
// Income is paid once per season (SeasonManager.NewSeason) while the deal lasts.
[System.Serializable]
public class SponsorOffer
{
    public string id;
    public string sponsorName;
    public long income;   // paid every season
}

public static class SponsorCatalog
{
    public const int ContractSeasons = 3;   // a sponsor deal lasts 3 seasons

    public static List<SponsorOffer> Create()
    {
        string[] names = { "Nike", "Adidas", "Puma", "New Balance", "Emirates", "Visa", "Qatar Airways", "Samsung", "Pepsi", "Coca-Cola" };
        long[] income = { 60_000_000, 55_000_000, 40_000_000, 35_000_000, 90_000_000, 70_000_000, 85_000_000, 50_000_000, 30_000_000, 30_000_000 };
        List<SponsorOffer> list = new List<SponsorOffer>();
        for (int i = 0; i < names.Length; i++)
            list.Add(new SponsorOffer { id = "SP" + (i + 1).ToString("00"), sponsorName = names[i], income = income[i] });
        return list;
    }

    public static SponsorOffer Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (SponsorOffer s in Create()) if (s.id == id) return s;
        return null;
    }
}
