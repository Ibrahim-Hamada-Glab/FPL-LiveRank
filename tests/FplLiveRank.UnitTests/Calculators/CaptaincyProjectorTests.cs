using FluentAssertions;
using FplLiveRank.Application.Calculators;
using FplLiveRank.Domain.Enums;

namespace FplLiveRank.UnitTests.Calculators;

public sealed class CaptaincyProjectorTests
{
    // element ids
    private const int Cap = 100;
    private const int Vice = 200;
    private const int Other = 300;
    private const int CapTeam = 1;
    private const int ViceTeam = 2;

    private static IReadOnlyList<LivePickInput> StandardPicks(int captainMultiplier = 2) => new[]
    {
        new LivePickInput(Cap, 1, captainMultiplier, IsCaptain: true, IsViceCaptain: false),
        new LivePickInput(Vice, 2, 1, IsCaptain: false, IsViceCaptain: true),
        new LivePickInput(Other, 3, 1, IsCaptain: false, IsViceCaptain: false),
    };

    private static IReadOnlyDictionary<int, int> Teams() => new Dictionary<int, int>
    {
        [Cap] = CapTeam, [Vice] = ViceTeam, [Other] = 3,
    };

    private static IReadOnlyDictionary<int, LivePlayerStat> Stats(int capMins, int viceMins) => new Dictionary<int, LivePlayerStat>
    {
        [Cap] = new(Cap, capMins > 0 ? 6 : 0, capMins, 0),
        [Vice] = new(Vice, viceMins > 0 ? 5 : 0, viceMins, 0),
        [Other] = new(Other, 4, 90, 0),
    };

    [Fact]
    public void Captain_played_keeps_official_multiplier()
    {
        var result = CaptaincyProjector.Project(
            StandardPicks(),
            Stats(capMins: 60, viceMins: 90),
            Teams(),
            new Dictionary<int, bool> { [CapTeam] = false, [ViceTeam] = true });

        result.Status.Should().Be(CaptaincyStatus.CaptainPlayed);
        result.AdjustedPicks.Should().BeEquivalentTo(StandardPicks());
        result.PromotedElementId.Should().Be(Cap);
    }

    [Fact]
    public void Captain_blanked_with_team_finished_promotes_vice()
    {
        var result = CaptaincyProjector.Project(
            StandardPicks(),
            Stats(capMins: 0, viceMins: 75),
            Teams(),
            new Dictionary<int, bool> { [CapTeam] = true, [ViceTeam] = true });

        result.Status.Should().Be(CaptaincyStatus.VicePromoted);
        result.PromotedElementId.Should().Be(Vice);
        result.AdjustedPicks.First(p => p.ElementId == Cap).Multiplier.Should().Be(1);
        result.AdjustedPicks.First(p => p.ElementId == Vice).Multiplier.Should().Be(2);
    }

    [Fact]
    public void Captain_blanked_with_team_unfinished_stays_projected()
    {
        var result = CaptaincyProjector.Project(
            StandardPicks(),
            Stats(capMins: 0, viceMins: 90),
            Teams(),
            new Dictionary<int, bool> { [CapTeam] = false, [ViceTeam] = true });

        result.Status.Should().Be(CaptaincyStatus.Projected);
        result.AdjustedPicks.Should().BeEquivalentTo(StandardPicks());
    }

    [Fact]
    public void Triple_captain_promotion_carries_multiplier_three()
    {
        var result = CaptaincyProjector.Project(
            StandardPicks(captainMultiplier: 3),
            Stats(capMins: 0, viceMins: 90),
            Teams(),
            new Dictionary<int, bool> { [CapTeam] = true, [ViceTeam] = true });

        result.Status.Should().Be(CaptaincyStatus.VicePromoted);
        result.AdjustedPicks.First(p => p.ElementId == Vice).Multiplier.Should().Be(3);
        result.AdjustedPicks.First(p => p.ElementId == Cap).Multiplier.Should().Be(1);
    }

    [Fact]
    public void Both_blanked_with_all_finished_yields_no_captain_points()
    {
        var result = CaptaincyProjector.Project(
            StandardPicks(),
            Stats(capMins: 0, viceMins: 0),
            Teams(),
            new Dictionary<int, bool> { [CapTeam] = true, [ViceTeam] = true });

        result.Status.Should().Be(CaptaincyStatus.NoCaptainPoints);
        result.PromotedElementId.Should().BeNull();
        result.AdjustedPicks.Should().BeEquivalentTo(StandardPicks());
    }

    [Fact]
    public void Captain_blanked_vice_yet_to_play_stays_projected()
    {
        var result = CaptaincyProjector.Project(
            StandardPicks(),
            Stats(capMins: 0, viceMins: 0),
            Teams(),
            new Dictionary<int, bool> { [CapTeam] = true, [ViceTeam] = false });

        result.Status.Should().Be(CaptaincyStatus.Projected);
        result.AdjustedPicks.Should().BeEquivalentTo(StandardPicks());
    }
}
