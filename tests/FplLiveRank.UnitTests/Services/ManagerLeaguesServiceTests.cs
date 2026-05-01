using FluentAssertions;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using Moq;

namespace FplLiveRank.UnitTests.Services;

public sealed class ManagerLeaguesServiceTests
{
    [Fact]
    public async Task GetAsync_returns_classic_leagues_and_uses_cache_key()
    {
        var fpl = new Mock<IFplApiClient>();
        fpl.Setup(x => x.GetManagerEntryAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagerEntryResponse
            {
                PlayerFirstName = "Test",
                PlayerLastName = "Manager",
                Name = "Test FC",
                Leagues = new ManagerEntryLeagues
                {
                    Classic =
                    {
                        new ManagerEntryClassicLeague
                        {
                            Id = 1,
                            Name = "Overall",
                            ShortName = "overall",
                            LeagueType = "s",
                            Scoring = "c",
                            Rank = 1000
                        },
                        new ManagerEntryClassicLeague
                        {
                            Id = 99,
                            Name = "Friends League",
                            LeagueType = "x",
                            Scoring = "c",
                            Rank = 4,
                            EntryCanLeave = true,
                            EntryCanInvite = true
                        }
                    }
                }
            });
        var cache = new RecordingCacheService();
        var service = new ManagerLeaguesService(fpl.Object, cache);

        var result = await service.GetAsync(123);

        result.ManagerId.Should().Be(123);
        result.PlayerName.Should().Be("Test Manager");
        result.TeamName.Should().Be("Test FC");
        result.ClassicLeagues.Should().HaveCount(2);
        result.ClassicLeagues[0].IsSystemLeague.Should().BeTrue();
        result.ClassicLeagues[1].Name.Should().Be("Friends League");
        result.ClassicLeagues[1].EntryCanInvite.Should().BeTrue();
        cache.RequestedKeys.Should().Contain(CacheKeys.ManagerEntry(123));
    }

    [Fact]
    public async Task GetAsync_throws_validation_exception_for_non_positive_manager_id()
    {
        var service = new ManagerLeaguesService(Mock.Of<IFplApiClient>(), new RecordingCacheService());

        var act = async () => await service.GetAsync(0);

        var ex = await act.Should().ThrowAsync<Application.Errors.ValidationException>();
        ex.Which.Errors.Should().ContainKey("managerId");
    }
}
