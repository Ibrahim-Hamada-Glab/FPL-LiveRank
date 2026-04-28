using FluentAssertions;
using FplLiveRank.Application.Calculators;

namespace FplLiveRank.UnitTests.Calculators;

public sealed class LivePointsCalculatorTests
{
    private static LivePickInput Pick(int element, int position, int multiplier, bool captain = false, bool vice = false)
        => new(element, position, multiplier, captain, vice);

    private static IReadOnlyDictionary<int, LivePlayerStat> Stats(params (int Element, int Pts, int Min, int Bonus)[] entries)
        => entries.ToDictionary(e => e.Element, e => new LivePlayerStat(e.Element, e.Pts, e.Min, e.Bonus));

    [Fact]
    public void Sums_pick_points_times_multiplier()
    {
        var picks = new[] { Pick(1, 1, 1), Pick(2, 2, 1), Pick(3, 3, 1) };
        var stats = Stats((1, 5, 90, 0), (2, 8, 90, 1), (3, 2, 60, 0));

        var result = LivePointsCalculator.Calculate(picks, stats, transferCost: 0);

        result.RawLivePoints.Should().Be(15);
        result.LivePointsAfterHits.Should().Be(15);
        result.Lines.Should().HaveCount(3);
        result.Lines.Single(l => l.ElementId == 2).ContributedPoints.Should().Be(8);
    }

    [Fact]
    public void Doubles_captain_via_multiplier_two()
    {
        var picks = new[] { Pick(10, 1, 2, captain: true), Pick(20, 2, 1) };
        var stats = Stats((10, 10, 90, 0), (20, 4, 90, 0));

        var result = LivePointsCalculator.Calculate(picks, stats, transferCost: 0);

        result.RawLivePoints.Should().Be(24);
        result.Lines.Single(l => l.IsCaptain).ContributedPoints.Should().Be(20);
    }

    [Fact]
    public void Triples_captain_via_multiplier_three()
    {
        var picks = new[] { Pick(10, 1, 3, captain: true) };
        var stats = Stats((10, 7, 90, 0));

        var result = LivePointsCalculator.Calculate(picks, stats, transferCost: 0);

        result.RawLivePoints.Should().Be(21);
    }

    [Fact]
    public void Bench_picks_with_multiplier_zero_contribute_nothing()
    {
        var picks = new[] { Pick(1, 1, 1), Pick(99, 12, 0) };
        var stats = Stats((1, 5, 90, 0), (99, 9, 90, 1));

        var result = LivePointsCalculator.Calculate(picks, stats, transferCost: 0);

        result.RawLivePoints.Should().Be(5);
        result.Lines.Single(l => l.ElementId == 99).ContributedPoints.Should().Be(0);
        result.Lines.Single(l => l.ElementId == 99).LiveTotalPoints.Should().Be(9);
    }

    [Fact]
    public void Deducts_transfer_cost_after_summation()
    {
        var picks = new[] { Pick(1, 1, 1), Pick(2, 2, 2, captain: true) };
        var stats = Stats((1, 5, 90, 0), (2, 6, 90, 0));

        var result = LivePointsCalculator.Calculate(picks, stats, transferCost: 8);

        result.RawLivePoints.Should().Be(17);
        result.LivePointsAfterHits.Should().Be(9);
        result.TransferCost.Should().Be(8);
    }

    [Fact]
    public void Missing_live_stat_treated_as_zero()
    {
        var picks = new[] { Pick(1, 1, 1), Pick(2, 2, 1) };
        var stats = Stats((1, 5, 90, 0));

        var result = LivePointsCalculator.Calculate(picks, stats, transferCost: 0);

        result.RawLivePoints.Should().Be(5);
        result.Lines.Single(l => l.ElementId == 2).LiveTotalPoints.Should().Be(0);
        result.Lines.Single(l => l.ElementId == 2).ContributedPoints.Should().Be(0);
    }

    [Fact]
    public void Negative_transfer_cost_throws()
    {
        var act = () => LivePointsCalculator.Calculate(
            Array.Empty<LivePickInput>(),
            new Dictionary<int, LivePlayerStat>(),
            transferCost: -4);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
