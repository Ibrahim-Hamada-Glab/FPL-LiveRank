using FplLiveRank.Domain.Models;

namespace FplLiveRank.Application.Calculators;

public sealed record LivePickInput(
    int ElementId,
    int Position,
    int Multiplier,
    bool IsCaptain,
    bool IsViceCaptain);

public sealed record LivePlayerStat(
    int ElementId,
    int TotalPoints,
    int Minutes,
    int Bonus);

/// <summary>
/// Pure calculation: Σ(player.live_total_points × pick.multiplier) − transferCost.
/// Trusts the multiplier supplied by FPL (which already encodes captain × 2/3 and bench=0).
/// Captaincy/auto-sub projection layers add corrections before this is invoked.
/// </summary>
public static class LivePointsCalculator
{
    public static LiveManagerPointsBreakdown Calculate(
        IReadOnlyList<LivePickInput> picks,
        IReadOnlyDictionary<int, LivePlayerStat> liveStats,
        int transferCost)
    {
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(liveStats);
        if (transferCost < 0) throw new ArgumentOutOfRangeException(nameof(transferCost));

        var lines = new List<PlayerLineLivePoints>(picks.Count);
        var raw = 0;

        foreach (var pick in picks)
        {
            liveStats.TryGetValue(pick.ElementId, out var stat);
            var totalPoints = stat?.TotalPoints ?? 0;
            var minutes = stat?.Minutes ?? 0;
            var bonus = stat?.Bonus ?? 0;
            var contributed = totalPoints * pick.Multiplier;
            raw += contributed;

            lines.Add(new PlayerLineLivePoints(
                ElementId: pick.ElementId,
                Position: pick.Position,
                Multiplier: pick.Multiplier,
                IsCaptain: pick.IsCaptain,
                IsViceCaptain: pick.IsViceCaptain,
                LiveTotalPoints: totalPoints,
                Minutes: minutes,
                Bonus: bonus,
                ContributedPoints: contributed));
        }

        return new LiveManagerPointsBreakdown(
            RawLivePoints: raw,
            TransferCost: transferCost,
            LivePointsAfterHits: raw - transferCost,
            Lines: lines);
    }
}
