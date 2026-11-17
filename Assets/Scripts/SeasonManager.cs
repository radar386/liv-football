// What happens to one club when a new season starts (no Unity code).
// DRAFT written from how the other scripts call it; compare with your own version.
public static class SeasonManager
{
    public static void NewSeason(UserProfile p)
    {
        // Sponsor pays once a season while the deal lasts.
        if (p.sponsorSeasonsLeft > 0 && !string.IsNullOrEmpty(p.sponsorId))
        {
            SponsorOffer sponsor = SponsorCatalog.Find(p.sponsorId);
            if (sponsor != null) p.money += sponsor.income;
            p.sponsorSeasonsLeft--;
            if (p.sponsorSeasonsLeft <= 0) p.sponsorId = "";
        }

        // Injuries heal and bans and yellow cards reset over the break.
        p.statuses.Clear();

        // Season counter, scouts, contracts and wages.
        AcademyScout.NewSeason(p);
    }
}
