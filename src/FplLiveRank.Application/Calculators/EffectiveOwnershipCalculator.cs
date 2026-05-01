using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.Application.Calculators;

public sealed class EffectiveOwnershipCalculator : IEffectiveOwnershipCalculator
{
    public IReadOnlyList<LeagueEffectiveOwnershipEntryDto> Calculate(
        IReadOnlyList<ManagerLiveDto> managerScores,
        int? selectedManagerId)
    {
        if (managerScores.Count == 0)
        {
            return Array.Empty<LeagueEffectiveOwnershipEntryDto>();
        }

        var managerCount = managerScores.Count;
        var selectedMultipliers = BuildSelectedManagerMultiplierMap(managerScores, selectedManagerId);

        var groupedPlayers = managerScores
            .SelectMany(score => score.Picks)
            .GroupBy(pick => pick.ElementId);

        return groupedPlayers
            .Select(group =>
            {
                var picks = group.ToList();
                var ownedCount = picks.Count(p => p.Multiplier > 0);
                var captainedCount = picks.Count(p => p.Multiplier > 1);
                var multiplierSum = picks.Sum(p => p.Multiplier);
                var sample = picks[0];

                var ownershipPercent = ToPercent(ownedCount, managerCount);
                var captaincyPercent = ToPercent(captainedCount, managerCount);
                var effectiveOwnershipPercent = ToPercent(multiplierSum, managerCount);

                selectedMultipliers.TryGetValue(group.Key, out var userMultiplier);
                var rankImpactPerPoint = userMultiplier - (effectiveOwnershipPercent / 100m);

                return new LeagueEffectiveOwnershipEntryDto(
                    ElementId: group.Key,
                    WebName: sample.WebName,
                    TeamId: sample.TeamId,
                    ElementType: sample.ElementType,
                    OwnershipPercent: ownershipPercent,
                    CaptaincyPercent: captaincyPercent,
                    EffectiveOwnershipPercent: effectiveOwnershipPercent,
                    UserMultiplier: userMultiplier,
                    RankImpactPerPoint: rankImpactPerPoint,
                    ImpactExplanation: DescribeImpact(rankImpactPerPoint, userMultiplier, effectiveOwnershipPercent));
            })
            .OrderByDescending(row => row.EffectiveOwnershipPercent)
            .ThenByDescending(row => row.OwnershipPercent)
            .ThenBy(row => row.WebName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<int, int> BuildSelectedManagerMultiplierMap(
        IReadOnlyList<ManagerLiveDto> managerScores,
        int? selectedManagerId)
    {
        if (!selectedManagerId.HasValue)
        {
            return new Dictionary<int, int>();
        }

        var selected = managerScores.FirstOrDefault(score => score.ManagerId == selectedManagerId.Value);
        return selected is null
            ? new Dictionary<int, int>()
            : selected.Picks.ToDictionary(p => p.ElementId, p => p.Multiplier);
    }

    private static decimal ToPercent(int value, int totalManagers)
        => totalManagers == 0 ? 0m : decimal.Round(value * 100m / totalManagers, 2);

    private static string DescribeImpact(decimal rankImpactPerPoint, int userMultiplier, decimal effectiveOwnershipPercent)
    {
        if (rankImpactPerPoint > 0m)
        {
            return $"Differential upside: your multiplier ({userMultiplier}) is above league EO ({effectiveOwnershipPercent:0.##}%).";
        }

        if (rankImpactPerPoint < 0m)
        {
            return $"Shield risk: league EO ({effectiveOwnershipPercent:0.##}%) is above your multiplier ({userMultiplier}).";
        }

        return "Neutral impact: your exposure matches league effective ownership.";
    }
}
