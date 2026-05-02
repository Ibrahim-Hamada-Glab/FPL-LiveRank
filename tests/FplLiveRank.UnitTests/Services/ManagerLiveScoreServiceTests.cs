using FluentAssertions;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using FplLiveRank.Domain.Entities;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FplLiveRank.UnitTests.Services;

public sealed class ManagerLiveScoreServiceTests
{
    [Fact]
    public async Task GetAsync_aggregates_live_score_for_explicit_event()
    {
        var fpl = CreateFplClient();
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetPlayersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FplPlayer>
            {
                [10] = new() { Id = 10, WebName = "Captain", TeamId = 1, ElementType = ElementType.Midfielder },
                [20] = new() { Id = 20, WebName = "Starter", TeamId = 2, ElementType = ElementType.Forward },
                [30] = new() { Id = 30, WebName = "Bench", TeamId = 3, ElementType = ElementType.Goalkeeper }
            });
        var service = CreateService(fpl.Object, bootstrap.Object);

        var result = await service.GetAsync(123, eventId: 5);

        result.ManagerId.Should().Be(123);
        result.EventId.Should().Be(5);
        result.PlayerName.Should().Be("Test Manager");
        result.TeamName.Should().Be("Test Team");
        result.RawLivePoints.Should().Be(25);
        result.TransferCost.Should().Be(4);
        result.LivePointsAfterHits.Should().Be(21);
        result.PreviousTotal.Should().Be(90);
        result.LiveSeasonTotal.Should().Be(111);
        result.ActiveChip.Should().Be(ChipType.TripleCaptain);
        result.CaptainElementId.Should().Be(10);
        result.ViceCaptainElementId.Should().Be(20);
        result.CaptaincyStatus.Should().Be(CaptaincyStatus.CaptainPlayed);
        result.EffectiveCaptainElementId.Should().Be(10);
        result.Picks.Should().HaveCount(3);
        result.Picks.Single(x => x.ElementId == 10).ContributedPoints.Should().Be(20);
        result.Picks.Single(x => x.ElementId == 30).ContributedPoints.Should().Be(0);
        bootstrap.Verify(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_resolves_current_event_when_event_is_not_provided()
    {
        var fpl = CreateFplClient();
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentEventDto(5, "Gameweek 5", null, true, false, false, false));
        bootstrap.Setup(x => x.GetPlayersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FplPlayer>());
        var service = CreateService(fpl.Object, bootstrap.Object);

        var result = await service.GetAsync(123, eventId: null);

        result.EventId.Should().Be(5);
        bootstrap.Verify(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()), Times.Once);
        fpl.Verify(x => x.GetPicksAsync(123, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_throws_validation_exception_for_non_positive_manager_id()
    {
        var service = CreateService(CreateFplClient().Object, Mock.Of<IFplBootstrapService>());

        var act = async () => await service.GetAsync(0, eventId: 1);

        var ex = await act.Should().ThrowAsync<Application.Errors.ValidationException>();
        ex.Which.Errors.Should().ContainKey("managerId");
    }

    [Fact]
    public async Task GetAsync_throws_not_found_when_manager_has_no_picks()
    {
        var fpl = CreateFplClient(picks: new PicksResponse());
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetPlayersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FplPlayer>());
        var service = CreateService(fpl.Object, bootstrap.Object);

        var act = async () => await service.GetAsync(123, eventId: 5);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("No picks available for manager 123 in gameweek 5.");
    }

    [Fact]
    public async Task GetAsync_uses_cache_keys_for_fpl_calls()
    {
        var fpl = CreateFplClient();
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetPlayersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FplPlayer>());
        var cache = new RecordingCacheService();
        var service = CreateService(fpl.Object, bootstrap.Object, cache);

        await service.GetAsync(123, eventId: 5);

        cache.RequestedKeys.Should().Contain(new[]
        {
            CacheKeys.ManagerPicks(123, 5),
            CacheKeys.EventLive(5),
            CacheKeys.EventFixtures(5),
            CacheKeys.ManagerHistory(123),
            CacheKeys.ManagerEntry(123)
        });
    }

    [Fact]
    public async Task GetAsync_returns_cached_snapshot_without_calling_fpl()
    {
        var expected = new ManagerLiveDto(
            ManagerId: 123,
            EventId: 5,
            PlayerName: "Cached Player",
            TeamName: "Cached Team",
            RawLivePoints: 30,
            TransferCost: 0,
            LivePointsAfterHits: 30,
            PreviousTotal: 100,
            LiveSeasonTotal: 130,
            ActiveChip: ChipType.None,
            CaptainElementId: null,
            ViceCaptainElementId: null,
            CaptaincyStatus: CaptaincyStatus.CaptainPlayed,
            EffectiveCaptainElementId: null,
            AutoSubs: Array.Empty<SubstitutionDto>(),
            BlockedStarterElementIds: Array.Empty<int>(),
            AutoSubProjectionFinal: true,
            Picks: Array.Empty<ManagerLivePickDto>(),
            CalculatedAtUtc: DateTimeOffset.UtcNow);
        var cache = new SnapshotHitCacheService(CacheKeys.ManagerLiveSnapshot(123, 5), expected);
        var fpl = CreateFplClient();
        var service = CreateService(fpl.Object, Mock.Of<IFplBootstrapService>(), cache);

        var result = await service.GetAsync(123, eventId: 5);

        result.Should().BeSameAs(expected);
        cache.RequestedKeys.Should().ContainSingle(CacheKeys.ManagerLiveSnapshot(123, 5));
        fpl.Verify(x => x.GetPicksAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fpl.Verify(x => x.GetEventLiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fpl.Verify(x => x.GetFixturesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fpl.Verify(x => x.GetHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ManagerLiveScoreService CreateService(
        IFplApiClient fpl,
        IFplBootstrapService bootstrap,
        ICacheService? cache = null)
        => new(
            fpl,
            bootstrap,
            cache ?? new RecordingCacheService(),
            NullLogger<ManagerLiveScoreService>.Instance);

    private static Mock<IFplApiClient> CreateFplClient(PicksResponse? picks = null)
    {
        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetPicksAsync(123, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(picks ?? new PicksResponse
            {
                ActiveChip = "3xc",
                EntryHistory = new PicksEntryHistory { EventTransfersCost = 4 },
                Picks =
                {
                    new PicksItem { Element = 10, Position = 1, Multiplier = 2, IsCaptain = true },
                    new PicksItem { Element = 20, Position = 2, Multiplier = 1, IsViceCaptain = true },
                    new PicksItem { Element = 30, Position = 12, Multiplier = 0 }
                }
            });
        fpl.Setup(x => x.GetEventLiveAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventLiveResponse
            {
                Elements =
                {
                    new EventLiveElement { Id = 10, Stats = new EventLiveStats { TotalPoints = 10, Minutes = 90, Bonus = 3 } },
                    new EventLiveElement { Id = 20, Stats = new EventLiveStats { TotalPoints = 5, Minutes = 45, Bonus = 0 } },
                    new EventLiveElement { Id = 30, Stats = new EventLiveStats { TotalPoints = 8, Minutes = 90, Bonus = 1 } }
                }
            });
        fpl.Setup(x => x.GetFixturesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FplFixture>
            {
                new() { Id = 1, Event = 5, TeamH = 1, TeamA = 2, Finished = true },
                new() { Id = 2, Event = 5, TeamH = 3, TeamA = 4, FinishedProvisional = true }
            });
        fpl.Setup(x => x.GetHistoryAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HistoryResponse
            {
                Current =
                {
                    new HistoryCurrentItem { Event = 3, TotalPoints = 70 },
                    new HistoryCurrentItem { Event = 4, TotalPoints = 90 },
                    new HistoryCurrentItem { Event = 5, TotalPoints = 110 }
                }
            });
        fpl.Setup(x => x.GetManagerEntryAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerEntryResponse
            {
                PlayerFirstName = "Test",
                PlayerLastName = "Manager",
                Name = "Test Team"
            });

        return fpl;
    }

    private sealed class SnapshotHitCacheService : ICacheService
    {
        private readonly string _key;
        private readonly object _value;

        public SnapshotHitCacheService(string key, object value)
        {
            _key = key;
            _value = value;
        }

        public List<string> RequestedKeys { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            RequestedKeys.Add(key);
            return Task.FromResult(key == _key ? (T?)_value : null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
            => throw new InvalidOperationException("Cached snapshot should not be recomputed.");

        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken ct = default)
            where T : class
            => throw new InvalidOperationException("Cached snapshot should not call downstream cache factories.");

        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IAsyncDisposable?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
            => throw new InvalidOperationException("Cached snapshot should not acquire a refresh lock.");
    }
}
