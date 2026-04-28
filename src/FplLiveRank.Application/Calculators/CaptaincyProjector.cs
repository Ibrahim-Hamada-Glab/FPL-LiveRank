using FplLiveRank.Domain.Enums;

namespace FplLiveRank.Application.Calculators;

public sealed record CaptaincyProjectionResult(
    IReadOnlyList<LivePickInput> AdjustedPicks,
    CaptaincyStatus Status,
    int? PromotedElementId);

/// <summary>
/// Decides whether to keep the FPL-supplied captain multiplier or promote the vice-captain.
///
/// Rule (per FPL): if the captain plays 0 minutes AND every fixture his team has in the gameweek
/// is finished, the vice-captain receives the captain's multiplier. While any of the captain's
/// team's fixtures are still upcoming, the official multiplier stands but the result is flagged Projected.
///
/// Pure function — takes everything it needs as inputs; no IO.
/// </summary>
public static class CaptaincyProjector
{
    public static CaptaincyProjectionResult Project(
        IReadOnlyList<LivePickInput> picks,
        IReadOnlyDictionary<int, LivePlayerStat> liveStats,
        IReadOnlyDictionary<int, int> playerTeams,
        IReadOnlyDictionary<int, bool> teamFixturesFinished)
    {
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(liveStats);
        ArgumentNullException.ThrowIfNull(playerTeams);
        ArgumentNullException.ThrowIfNull(teamFixturesFinished);

        var captain = picks.FirstOrDefault(p => p.IsCaptain);
        if (captain is null)
        {
            return new CaptaincyProjectionResult(picks, CaptaincyStatus.NoCaptainPoints, null);
        }

        var captainMinutes = liveStats.TryGetValue(captain.ElementId, out var capStat) ? capStat.Minutes : 0;
        if (captainMinutes > 0)
        {
            return new CaptaincyProjectionResult(picks, CaptaincyStatus.CaptainPlayed, captain.ElementId);
        }

        var captainTeamFinished = playerTeams.TryGetValue(captain.ElementId, out var capTeam)
            && teamFixturesFinished.TryGetValue(capTeam, out var capFinished)
            && capFinished;

        if (!captainTeamFinished)
        {
            return new CaptaincyProjectionResult(picks, CaptaincyStatus.Projected, captain.ElementId);
        }

        var vice = picks.FirstOrDefault(p => p.IsViceCaptain);
        if (vice is null)
        {
            return new CaptaincyProjectionResult(picks, CaptaincyStatus.NoCaptainPoints, null);
        }

        var viceMinutes = liveStats.TryGetValue(vice.ElementId, out var viceStat) ? viceStat.Minutes : 0;
        if (viceMinutes == 0)
        {
            var viceTeamFinished = playerTeams.TryGetValue(vice.ElementId, out var viceTeam)
                && teamFixturesFinished.TryGetValue(viceTeam, out var vFinished)
                && vFinished;

            return viceTeamFinished
                ? new CaptaincyProjectionResult(picks, CaptaincyStatus.NoCaptainPoints, null)
                : new CaptaincyProjectionResult(picks, CaptaincyStatus.Projected, captain.ElementId);
        }

        var captainMultiplier = captain.Multiplier;
        var adjusted = new List<LivePickInput>(picks.Count);
        foreach (var p in picks)
        {
            if (p.ElementId == captain.ElementId)
            {
                adjusted.Add(p with { Multiplier = 1 });
            }
            else if (p.ElementId == vice.ElementId)
            {
                adjusted.Add(p with { Multiplier = captainMultiplier });
            }
            else
            {
                adjusted.Add(p);
            }
        }

        return new CaptaincyProjectionResult(adjusted, CaptaincyStatus.VicePromoted, vice.ElementId);
    }
}
