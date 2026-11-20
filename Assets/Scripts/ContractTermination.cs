using System;
using System.Collections.Generic;

// Ending a contract early (DRAFT). The termination fee is random: 0 to 100 million, in whole millions.
// Roll the fee when the player taps Terminate, show it, and only call a Terminate method after he confirms.
public static class ContractTermination
{
    public const long MaxFee = 100_000_000;
    public const int MinSquad = 11;

    public static long RollFee(Random rng) { return (long)rng.Next(0, 101) * 1_000_000L; }

    // Releases a squad player. You must keep at least 11. Returns false and sets problem if it cannot be done.
    public static bool TerminatePlayer(UserProfile p, string playerId, long fee, out string problem)
    {
        problem = null;
        ClubSquadPlayer c = TeamBuilder.FindSquadPlayer(p, playerId);
        if (c == null) { problem = "That player is not in your squad."; return false; }
        if (p.squad.Count <= MinSquad) { problem = "You must keep at least " + MinSquad + " players."; return false; }
        if (p.money < fee) { problem = "The fee is " + fee.ToString("N0") + " and you only have " + p.money.ToString("N0") + "."; return false; }
        p.money -= fee;
        p.squad.Remove(c);
        if (p.lineupIds != null) p.lineupIds.Remove(playerId);
        if (p.academyContracts != null) p.academyContracts.RemoveAll(x => x.playerId == playerId);
        if (p.potentials != null) p.potentials.RemoveAll(x => x.playerId == playerId);
        if (p.statuses != null) p.statuses.RemoveAll(x => x.playerId == playerId);
        if (p.lineupIds != null && p.lineupIds.Count != 11) p.lineupIds.Clear();   // back to the automatic lineup
        return true;
    }

    // Dismisses the hired coach (a real one; your own coaches have no contract).
    public static bool TerminateCoach(UserProfile p, long fee, out string problem)
    {
        problem = null;
        if (string.IsNullOrEmpty(p.coachId) || OwnCoachService.IsOwn(p.coachId)) { problem = "You have no coach under contract."; return false; }
        if (p.money < fee) { problem = "The fee is " + fee.ToString("N0") + " and you only have " + p.money.ToString("N0") + "."; return false; }
        p.money -= fee;
        p.coachId = "";
        return true;
    }

    // Ends the sponsor deal early (no refund: sponsors only pay at the end of a season).
    public static bool TerminateSponsor(UserProfile p, long fee, out string problem)
    {
        problem = null;
        if (string.IsNullOrEmpty(p.sponsorId) || p.sponsorSeasonsLeft <= 0) { problem = "You have no sponsor deal."; return false; }
        if (p.money < fee) { problem = "The fee is " + fee.ToString("N0") + " and you only have " + p.money.ToString("N0") + "."; return false; }
        p.money -= fee;
        p.sponsorId = "";
        p.sponsorSeasonsLeft = 0;
        return true;
    }
}
