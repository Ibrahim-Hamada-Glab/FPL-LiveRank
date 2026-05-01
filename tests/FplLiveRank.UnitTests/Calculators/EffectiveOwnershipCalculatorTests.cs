using FluentAssertions;
using FplLiveRank.Application.Calculators;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Domain.Enums;

namespace FplLiveRank.UnitTests.Calculators;

public sealed class EffectiveOwnershipCalculatorTests
{
    [Fact]
    public void Calculate_returns_eo_and_user_rank_impact_per_point()
    {
        var calculator = new EffectiveOwnershipCalculator();
        var managerScores = new List<ManagerLiveDto>
        {
            BuildManager(managerId: 1, new Dictionary<int, (string Name, int Multiplier)> { [10] = ("Saka", 2), [20] = ("Haaland", 1) }),
            BuildManager(managerId: 2, new Dictionary<int, (string Name, int Multiplier)> { [10] = ("Saka", 1), [20] = ("Haaland", 1) }),
            BuildManager(managerId: 3, new Dictionary<int, (string Name, int Multiplier)> { [10] = ("Saka", 0), [20] = ("Haaland", 2) }),
        };

        var result = calculator.Calculate(managerScores, selectedManagerId: 1);

        var saka = result.Single(x => x.ElementId == 10);
        saka.OwnershipPercent.Should().Be(66.67m);
        saka.CaptaincyPercent.Should().Be(33.33m);
        saka.EffectiveOwnershipPercent.Should().Be(100m);
        saka.UserMultiplier.Should().Be(2);
        saka.RankImpactPerPoint.Should().Be(1m);

        var haaland = result.Single(x => x.ElementId == 20);
        haaland.EffectiveOwnershipPercent.Should().Be(133.33m);
        haaland.UserMultiplier.Should().Be(1);
        haaland.RankImpactPerPoint.Should().Be(-0.3333m);
    }

    private static ManagerLiveDto BuildManager(int managerId, IReadOnlyDictionary<int, (string Name, int Multiplier)> players)
    {
        var picks = players.Select((player, index) => new ManagerLivePickDto(
            ElementId: player.Key,
            WebName: player.Value.Name,
            TeamId: 1,
            ElementType: (int)ElementType.Midfielder,
            Position: index + 1,
            Multiplier: player.Value.Multiplier,
            IsCaptain: player.Value.Multiplier > 1,
            IsViceCaptain: false,
            LiveTotalPoints: 0,
            Minutes: 0,
            Bonus: 0,
            ContributedPoints: 0)).ToList();

        return new ManagerLiveDto(
            ManagerId: managerId,
            EventId: 34,
            PlayerName: $"Manager {managerId}",
            TeamName: $"Team {managerId}",
            RawLivePoints: 0,
            TransferCost: 0,
            LivePointsAfterHits: 0,
            PreviousTotal: 0,
            LiveSeasonTotal: 0,
            ActiveChip: ChipType.None,
            CaptainElementId: null,
            ViceCaptainElementId: null,
            CaptaincyStatus: CaptaincyStatus.Projected,
            EffectiveCaptainElementId: null,
            AutoSubs: Array.Empty<SubstitutionDto>(),
            BlockedStarterElementIds: Array.Empty<int>(),
            AutoSubProjectionFinal: true,
            Picks: picks,
            CalculatedAtUtc: DateTimeOffset.UtcNow);
    }
}
