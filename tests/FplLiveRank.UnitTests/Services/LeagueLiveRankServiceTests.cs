using FluentAssertions;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FplLiveRank.UnitTests.Services;

public sealed class LeagueLiveRankServiceTests
{
    [Fact]
    public async Task GetAsync_paginates_scores_managers_and_ranks_live_totals()
    {
        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetLeagueStandingsAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueStandingsResponse
            {
                League = new LeagueInfo { Id = 99, Name = "Test League" },
                Standings = new LeagueStandings
                {
                    HasNext = true,
                    Page = 1,
                    Results =
                    {
                        new LeagueStandingResult { Entry = 1, EntryName = "Alpha", PlayerName = "A", Rank = 1, RankSort = 1, Total = 1000 },
                        new LeagueStandingResult { Entry = 2, EntryName = "Beta", PlayerName = "B", Rank = 2, RankSort = 2, Total = 990 }
                    }
                }
            });
        fpl.Setup(x => x.GetLeagueStandingsAsync(99, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueStandingsResponse
            {
                League = new LeagueInfo { Id = 99, Name = "Test League" },
                Standings = new LeagueStandings
                {
                    Page = 2,
                    Results =
                    {
                        new LeagueStandingResult { Entry = 3, EntryName = "Gamma", PlayerName = "G", Rank = 3, RankSort = 3, Total = 985 }
                    }
                }
            });

        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentEventDto(7, "Gameweek 7", null, true, false, false, false));

        var managerScores = new Mock<IManagerLiveScoreService>();
        managerScores.Setup(x => x.GetAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateScore(1, liveSeasonTotal: 1050, liveGwPoints: 50, transferCost: 0, captainName: "Captain A"));
        managerScores.Setup(x => x.GetAsync(2, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateScore(2, liveSeasonTotal: 1070, liveGwPoints: 80, transferCost: 4, captainName: "Captain B"));
        managerScores.Setup(x => x.GetAsync(3, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateScore(3, liveSeasonTotal: 1070, liveGwPoints: 85, transferCost: 0, captainName: "Captain C"));

        var cache = new RecordingCacheService();
        var service = new LeagueLiveRankService(fpl.Object, bootstrap.Object, managerScores.Object, cache,
            new NullFplLiveBroadcaster(), NullLogger<LeagueLiveRankService>.Instance);

        var result = await service.GetAsync(99, eventId: null);

        result.LeagueId.Should().Be(99);
        result.LeagueName.Should().Be("Test League");
        result.EventId.Should().Be(7);
        result.ManagerCount.Should().Be(3);
        result.Standings.Select(x => x.ManagerId).Should().Equal(3, 2, 1);
        result.Standings[0].LiveRank.Should().Be(1);
        result.Standings[0].RankChange.Should().Be(2);
        result.Standings[0].IsTiedOnLiveTotal.Should().BeTrue();
        result.Standings[0].CaptainName.Should().Be("Captain C");
        result.Standings[1].LiveRank.Should().Be(2);
        result.Standings[1].RankChange.Should().Be(0);
        result.Standings[2].LiveRank.Should().Be(3);
        result.Standings[2].RankChange.Should().Be(-2);
        cache.RequestedKeys.Should().Contain(new[]
        {
            CacheKeys.LeagueStandingsPage(99, 1),
            CacheKeys.LeagueStandingsPage(99, 2)
        });
    }

    [Fact]
    public async Task GetAsync_throws_validation_exception_for_non_positive_league_id()
    {
        var service = new LeagueLiveRankService(
            Mock.Of<IFplApiClient>(),
            Mock.Of<IFplBootstrapService>(),
            Mock.Of<IManagerLiveScoreService>(),
            new RecordingCacheService(),
            new NullFplLiveBroadcaster(),
            NullLogger<LeagueLiveRankService>.Instance);

        var act = async () => await service.GetAsync(0, eventId: 7);

        var ex = await act.Should().ThrowAsync<Application.Errors.ValidationException>();
        ex.Which.Errors.Should().ContainKey("leagueId");
    }

    [Fact]
    public async Task GetAsync_throws_not_found_when_league_has_no_members()
    {
        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetLeagueStandingsAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueStandingsResponse
            {
                League = new LeagueInfo { Id = 99, Name = "Empty League" },
                Standings = new LeagueStandings { Page = 1 }
            });
        var service = new LeagueLiveRankService(
            fpl.Object,
            Mock.Of<IFplBootstrapService>(),
            Mock.Of<IManagerLiveScoreService>(),
            new RecordingCacheService(),
            new NullFplLiveBroadcaster(),
            NullLogger<LeagueLiveRankService>.Instance);

        var act = async () => await service.GetAsync(99, eventId: 7);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("No standings were found for league 99.");
    }

    private static ManagerLiveDto CreateScore(
        int managerId,
        int liveSeasonTotal,
        int liveGwPoints,
        int transferCost,
        string captainName)
    {
        var captainElementId = managerId * 100;
        return new ManagerLiveDto(
            ManagerId: managerId,
            EventId: 7,
            PlayerName: string.Empty,
            TeamName: string.Empty,
            RawLivePoints: liveGwPoints + transferCost,
            TransferCost: transferCost,
            LivePointsAfterHits: liveGwPoints,
            PreviousTotal: liveSeasonTotal - liveGwPoints,
            LiveSeasonTotal: liveSeasonTotal,
            ActiveChip: ChipType.None,
            CaptainElementId: captainElementId,
            ViceCaptainElementId: null,
            CaptaincyStatus: CaptaincyStatus.CaptainPlayed,
            EffectiveCaptainElementId: captainElementId,
            AutoSubs: Array.Empty<SubstitutionDto>(),
            BlockedStarterElementIds: Array.Empty<int>(),
            AutoSubProjectionFinal: true,
            Picks: new List<ManagerLivePickDto>
            {
                new(captainElementId, captainName, 1, (int)ElementType.Midfielder, 1, 2, true, false, 10, 90, 0, 20)
            },
            CalculatedAtUtc: DateTimeOffset.UtcNow);
    }
}
