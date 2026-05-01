using FluentAssertions;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FplLiveRank.UnitTests.Services;

public sealed class SnapshotCachingTests
{
    [Fact]
    public async Task LeagueLiveRank_GetAsync_serves_cached_snapshot_without_recomputing()
    {
        var cache = new InMemoryCacheService();
        var snapshot = BuildLeagueDto(99, 7);
        cache.Seed(CacheKeys.LeagueLiveSnapshot(99, 7), snapshot);

        var fpl = new Mock<IFplApiClient>(MockBehavior.Strict);
        var managers = new Mock<IManagerLiveScoreService>(MockBehavior.Strict);
        var bootstrap = new Mock<IFplBootstrapService>(MockBehavior.Strict);

        var service = new LeagueLiveRankService(
            fpl.Object,
            bootstrap.Object,
            managers.Object,
            cache,
            new NullFplLiveBroadcaster(),
            NullLogger<LeagueLiveRankService>.Instance);

        var result = await service.GetAsync(99, eventId: 7);

        result.LeagueId.Should().Be(99);
        // Strict mocks would have thrown if any FPL/manager work happened.
        fpl.VerifyAll();
        managers.VerifyAll();
        bootstrap.VerifyAll();
    }

    [Fact]
    public async Task LeagueLiveRank_RefreshAsync_writes_snapshot_and_broadcasts()
    {
        var cache = new InMemoryCacheService();
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentEventDto(7, "Gameweek 7", null, true, false, false, false));

        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetLeagueStandingsAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueStandingsResponse
            {
                League = new LeagueInfo { Id = 99, Name = "Test League" },
                Standings = new LeagueStandings
                {
                    Page = 1,
                    Results = { new LeagueStandingResult { Entry = 1, EntryName = "Alpha", PlayerName = "A", Rank = 1, RankSort = 1, Total = 1000 } }
                }
            });

        var managers = new Mock<IManagerLiveScoreService>();
        managers.Setup(x => x.GetAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildManagerDto(1, 1050));

        var broadcaster = new RecordingBroadcaster();

        var service = new LeagueLiveRankService(
            fpl.Object, bootstrap.Object, managers.Object, cache, broadcaster, NullLogger<LeagueLiveRankService>.Instance);

        var dto = await service.RefreshAsync(99, eventId: null);

        dto.LeagueId.Should().Be(99);
        broadcaster.LeagueUpdates.Should().ContainSingle().Which.LeagueId.Should().Be(99);
        broadcaster.RefreshProgress.Select(p => p.Status).Should().Equal("started", "completed");
        cache.SnapshotWrites.Should().Contain(CacheKeys.LeagueLiveSnapshot(99, 7));
    }

    [Fact]
    public async Task LeagueLiveRank_RefreshAsync_preserves_prior_snapshot_for_rank_delta_explanations()
    {
        var cache = new InMemoryCacheService();
        var previous = new LeagueLiveRankDto(
            LeagueId: 99,
            LeagueName: "Test League",
            EventId: 7,
            ManagerCount: 1,
            Standings:
            [
                new LeagueLiveRankEntryDto(
                    ManagerId: 1,
                    EntryName: "Alpha",
                    PlayerName: "A",
                    OfficialRank: 1,
                    LiveRank: 2,
                    RankChange: -1,
                    OfficialTotal: 1000,
                    LiveTotal: 1020,
                    LiveGwPoints: 20,
                    TransferCost: 0,
                    ActiveChip: ChipType.None,
                    CaptainElementId: null,
                    CaptainName: null,
                    AutoSubs: [],
                    AutoSubProjectionFinal: true,
                    IsTiedOnLiveTotal: false)
            ],
            CalculatedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1));
        cache.Seed(CacheKeys.LeagueLiveSnapshot(99, 7), previous);

        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentEventDto(7, "Gameweek 7", null, true, false, false, false));

        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetLeagueStandingsAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueStandingsResponse
            {
                League = new LeagueInfo { Id = 99, Name = "Test League" },
                Standings = new LeagueStandings
                {
                    Page = 1,
                    Results = { new LeagueStandingResult { Entry = 1, EntryName = "Alpha", PlayerName = "A", Rank = 1, RankSort = 1, Total = 1000 } }
                }
            });

        var managers = new Mock<IManagerLiveScoreService>();
        managers.Setup(x => x.GetAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildManagerDto(1, 1050));

        var service = new LeagueLiveRankService(
            fpl.Object, bootstrap.Object, managers.Object, cache, new RecordingBroadcaster(), NullLogger<LeagueLiveRankService>.Instance);

        await service.RefreshAsync(99, eventId: null);

        cache.SnapshotWrites.Should().Contain(CacheKeys.LeagueLivePreviousSnapshot(99, 7));
    }

    [Fact]
    public async Task LeagueLiveRank_RefreshAsync_when_lock_held_returns_existing_snapshot_without_broadcasting()
    {
        var cache = new InMemoryCacheService();
        var existing = BuildLeagueDto(99, 7);
        cache.Seed(CacheKeys.LeagueLiveSnapshot(99, 7), existing);

        // Pre-acquire the lock so RefreshAsync can't get it.
        var preAcquired = await cache.AcquireLockAsync(CacheKeys.LeagueRefreshLock(99, 7), TimeSpan.FromSeconds(60));
        preAcquired.Should().NotBeNull();

        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentEventDto(7, "Gameweek 7", null, true, false, false, false));

        var fpl = new Mock<IFplApiClient>(MockBehavior.Strict);
        var managers = new Mock<IManagerLiveScoreService>(MockBehavior.Strict);
        var broadcaster = new RecordingBroadcaster();

        var service = new LeagueLiveRankService(
            fpl.Object, bootstrap.Object, managers.Object, cache, broadcaster, NullLogger<LeagueLiveRankService>.Instance);

        var dto = await service.RefreshAsync(99, eventId: null);

        dto.LeagueId.Should().Be(99);
        broadcaster.LeagueUpdates.Should().BeEmpty();
        broadcaster.RefreshProgress.Should().ContainSingle()
            .Which.Status.Should().Be("skipped");
    }

    private static LeagueLiveRankDto BuildLeagueDto(int leagueId, int eventId) => new(
        LeagueId: leagueId,
        LeagueName: "Test League",
        EventId: eventId,
        ManagerCount: 0,
        Standings: Array.Empty<LeagueLiveRankEntryDto>(),
        CalculatedAtUtc: DateTimeOffset.UtcNow);

    private static ManagerLiveDto BuildManagerDto(int managerId, int liveSeasonTotal) => new(
        ManagerId: managerId,
        EventId: 7,
        PlayerName: string.Empty,
        TeamName: string.Empty,
        RawLivePoints: 50,
        TransferCost: 0,
        LivePointsAfterHits: 50,
        PreviousTotal: liveSeasonTotal - 50,
        LiveSeasonTotal: liveSeasonTotal,
        ActiveChip: ChipType.None,
        CaptainElementId: 10,
        ViceCaptainElementId: null,
        CaptaincyStatus: CaptaincyStatus.CaptainPlayed,
        EffectiveCaptainElementId: 10,
        AutoSubs: Array.Empty<SubstitutionDto>(),
        BlockedStarterElementIds: Array.Empty<int>(),
        AutoSubProjectionFinal: true,
        Picks: Array.Empty<ManagerLivePickDto>(),
        CalculatedAtUtc: DateTimeOffset.UtcNow);
}

internal sealed class RecordingBroadcaster : IFplLiveBroadcaster
{
    public List<ManagerLiveDto> ManagerUpdates { get; } = new();
    public List<LeagueLiveRankDto> LeagueUpdates { get; } = new();
    public List<(int EventId, DateTimeOffset At)> EventRefreshes { get; } = new();
    public List<(string Scope, string Status, string? Detail)> RefreshProgress { get; } = new();

    public Task ManagerLiveScoreUpdated(ManagerLiveDto dto, CancellationToken ct = default)
    {
        ManagerUpdates.Add(dto);
        return Task.CompletedTask;
    }

    public Task LeagueLiveTableUpdated(LeagueLiveRankDto dto, CancellationToken ct = default)
    {
        LeagueUpdates.Add(dto);
        return Task.CompletedTask;
    }

    public Task EventLiveRefreshed(int eventId, DateTimeOffset refreshedAtUtc, CancellationToken ct = default)
    {
        EventRefreshes.Add((eventId, refreshedAtUtc));
        return Task.CompletedTask;
    }

    public Task RefreshProgressUpdated(string scope, string status, string? detail = null, CancellationToken ct = default)
    {
        RefreshProgress.Add((scope, status, detail));
        return Task.CompletedTask;
    }
}
