using FluentAssertions;
using FplLiveRank.Application.Calculators;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FplLiveRank.UnitTests.Services;

public sealed class LeagueEffectiveOwnershipServiceTests
{
    [Fact]
    public async Task GetAsync_returns_effective_ownership_for_league_members()
    {
        var cache = new InMemoryCacheService();
        var league = new LeagueLiveRankDto(
            LeagueId: 101,
            LeagueName: "Mini League",
            EventId: 34,
            ManagerCount: 2,
            Standings:
            [
                new LeagueLiveRankEntryDto(1, "A", "Alice", 1, 1, 0, 1000, 1010, 10, 0, ChipType.None, null, null, [], true, false),
                new LeagueLiveRankEntryDto(2, "B", "Bob", 2, 2, 0, 995, 1002, 7, 0, ChipType.None, null, null, [], true, false)
            ],
            CalculatedAtUtc: DateTimeOffset.UtcNow);

        var leagues = new Mock<ILeagueLiveRankService>();
        leagues.Setup(x => x.GetAsync(101, 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(league);

        var managerScores = new Mock<IManagerLiveScoreService>();
        managerScores.Setup(x => x.GetAsync(1, 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildManager(1, ("Saka", 10, 2), ("Haaland", 20, 1)));
        managerScores.Setup(x => x.GetAsync(2, 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildManager(2, ("Saka", 10, 1), ("Haaland", 20, 2)));

        var service = new LeagueEffectiveOwnershipService(
            leagues.Object,
            managerScores.Object,
            new EffectiveOwnershipCalculator(),
            cache,
            NullLogger<LeagueEffectiveOwnershipService>.Instance);

        var result = await service.GetAsync(101, 34, managerId: 1);

        result.LeagueId.Should().Be(101);
        result.Players.Should().HaveCount(2);
        result.Players.Single(x => x.ElementId == 10).UserMultiplier.Should().Be(2);
        cache.SnapshotWrites.Should().Contain(CacheKeys.LeagueEffectiveOwnershipSnapshot(101, 34));
    }

    private static ManagerLiveDto BuildManager(
        int managerId,
        (string Name, int ElementId, int Multiplier) first,
        (string Name, int ElementId, int Multiplier) second)
    {
        var picks = new List<ManagerLivePickDto>
        {
            new(first.ElementId, first.Name, 1, (int)ElementType.Midfielder, 1, first.Multiplier, first.Multiplier > 1, false, 0, 0, 0, 0),
            new(second.ElementId, second.Name, 1, (int)ElementType.Forward, 2, second.Multiplier, second.Multiplier > 1, false, 0, 0, 0, 0)
        };

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
