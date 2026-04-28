using FluentAssertions;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FplLiveRank.UnitTests.Services;

public sealed class FplBootstrapServiceTests
{
    [Fact]
    public async Task GetCurrentEvent_returns_current_event_when_available()
    {
        var service = CreateService(new BootstrapResponse
        {
            Events =
            {
                new BootstrapEvent { Id = 1, Name = "Gameweek 1", IsNext = true },
                new BootstrapEvent { Id = 2, Name = "Gameweek 2", IsCurrent = true, DataChecked = true }
            }
        });

        var result = await service.GetCurrentEventAsync();

        result.Id.Should().Be(2);
        result.Name.Should().Be("Gameweek 2");
        result.IsCurrent.Should().BeTrue();
        result.IsDataChecked.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentEvent_falls_back_to_next_event()
    {
        var service = CreateService(new BootstrapResponse
        {
            Events =
            {
                new BootstrapEvent { Id = 3, Name = "Gameweek 3", IsNext = true }
            }
        });

        var result = await service.GetCurrentEventAsync();

        result.Id.Should().Be(3);
        result.IsNext.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentEvent_throws_when_no_current_or_next_event_exists()
    {
        var service = CreateService(new BootstrapResponse
        {
            Events =
            {
                new BootstrapEvent { Id = 1, Name = "Gameweek 1", Finished = true }
            }
        });

        var act = async () => await service.GetCurrentEventAsync();

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("No current or next gameweek found.");
    }

    [Fact]
    public async Task GetPlayers_maps_bootstrap_elements_to_domain_players()
    {
        var service = CreateService(new BootstrapResponse
        {
            Elements =
            {
                new BootstrapElement
                {
                    Id = 100,
                    WebName = "Saka",
                    FirstName = "Bukayo",
                    SecondName = "Saka",
                    Team = 1,
                    ElementType = (int)ElementType.Midfielder,
                    Status = "a",
                    NowCost = 95,
                    SelectedByPercent = "42.1"
                },
                new BootstrapElement
                {
                    Id = 101,
                    WebName = "Mystery",
                    ElementType = (int)ElementType.Forward,
                    SelectedByPercent = "not-a-decimal"
                }
            }
        });

        var result = await service.GetPlayersAsync();

        result[100].WebName.Should().Be("Saka");
        result[100].TeamId.Should().Be(1);
        result[100].ElementType.Should().Be(ElementType.Midfielder);
        result[100].SelectedByPercent.Should().Be(42.1m);
        result[101].SelectedByPercent.Should().Be(0m);
    }

    [Fact]
    public async Task Sync_removes_bootstrap_cache_and_refreshes_it()
    {
        var api = new Mock<IFplApiClient>();
        api.Setup(x => x.GetBootstrapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootstrapResponse());
        var cache = new RecordingCacheService();
        var service = new FplBootstrapService(api.Object, cache, NullLogger<FplBootstrapService>.Instance);

        await service.SyncAsync();

        cache.RemovedKeys.Should().ContainSingle(CacheKeys.Bootstrap);
        cache.RequestedKeys.Should().Contain(CacheKeys.Bootstrap);
        api.Verify(x => x.GetBootstrapAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FplBootstrapService CreateService(BootstrapResponse bootstrap)
    {
        var api = new Mock<IFplApiClient>();
        api.Setup(x => x.GetBootstrapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bootstrap);

        return new FplBootstrapService(
            api.Object,
            new RecordingCacheService(),
            NullLogger<FplBootstrapService>.Instance);
    }
}
