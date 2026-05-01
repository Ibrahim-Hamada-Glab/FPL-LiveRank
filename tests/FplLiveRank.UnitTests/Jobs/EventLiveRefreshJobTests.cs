using FluentAssertions;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Jobs;
using FplLiveRank.Application.Services;
using FplLiveRank.UnitTests.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FplLiveRank.UnitTests.Jobs;

public sealed class EventLiveRefreshJobTests
{
    [Fact]
    public async Task RunAsync_warms_event_caches_and_broadcasts_refresh()
    {
        var cache = new InMemoryCacheService();
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentEventDto(7, "Gameweek 7", null, true, false, false, false));

        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetEventStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventStatusResponse());
        fpl.Setup(x => x.GetEventLiveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventLiveResponse());
        fpl.Setup(x => x.GetFixturesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FplFixture>());

        var broadcaster = new RecordingBroadcaster();
        var job = new EventLiveRefreshJob(fpl.Object, bootstrap.Object, cache, broadcaster, NullLogger<EventLiveRefreshJob>.Instance);

        await job.RunAsync();

        cache.SnapshotWrites.Should().Contain(new[]
        {
            CacheKeys.EventStatus,
            CacheKeys.EventLive(7),
            CacheKeys.EventFixtures(7)
        });
        broadcaster.EventRefreshes.Should().ContainSingle()
            .Which.EventId.Should().Be(7);
    }

    [Fact]
    public async Task RunAsync_swallows_exceptions_so_hangfire_doesnt_alarm()
    {
        var bootstrap = new Mock<IFplBootstrapService>();
        bootstrap.Setup(x => x.GetCurrentEventAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FPL down"));

        var job = new EventLiveRefreshJob(
            Mock.Of<IFplApiClient>(),
            bootstrap.Object,
            new InMemoryCacheService(),
            new RecordingBroadcaster(),
            NullLogger<EventLiveRefreshJob>.Instance);

        var act = async () => await job.RunAsync();
        await act.Should().NotThrowAsync();
    }
}
