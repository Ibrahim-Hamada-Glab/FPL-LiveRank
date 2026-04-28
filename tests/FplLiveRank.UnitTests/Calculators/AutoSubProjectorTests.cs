using FluentAssertions;
using FplLiveRank.Application.Calculators;

namespace FplLiveRank.UnitTests.Calculators;

public sealed class AutoSubProjectorTests
{
    private const int Gk = 1, Def = 2, Mid = 3, Fwd = 4;

    private static AutoSubPick P(int id, int pos, int type, int team)
        => new(id, pos, pos <= 11 ? 1 : 0, type, team, IsCaptain: false, IsViceCaptain: false);

    /// <summary>3-4-3 starting XI + bench GK + 3 outfield bench (DEF, MID, FWD). All teams = 99 by default.</summary>
    private static List<AutoSubPick> StandardSquad() => new()
    {
        P(1,  1, Gk,  99),
        P(2,  2, Def, 99), P(3,  3, Def, 99), P(4,  4, Def, 99),
        P(5,  5, Mid, 99), P(6,  6, Mid, 99), P(7,  7, Mid, 99), P(8,  8, Mid, 99),
        P(9,  9, Fwd, 99), P(10, 10, Fwd, 99), P(11, 11, Fwd, 99),
        P(12, 12, Gk,  99),
        P(13, 13, Def, 99), P(14, 14, Mid, 99), P(15, 15, Fwd, 99),
    };

    private static Dictionary<int, LivePlayerStat> Stats(params (int id, int mins)[] mins)
    {
        var dict = new Dictionary<int, LivePlayerStat>();
        for (var id = 1; id <= 15; id++) dict[id] = new LivePlayerStat(id, 0, 90, 0); // default played
        foreach (var (id, m) in mins) dict[id] = new LivePlayerStat(id, m > 0 ? 2 : 0, m, 0);
        return dict;
    }

    private static Dictionary<int, bool> AllTeamsFinished(bool finished = true)
        => new() { [99] = finished };

    [Fact]
    public void No_blanks_means_no_subs()
    {
        var result = AutoSubProjector.Project(StandardSquad(), Stats(), AllTeamsFinished());
        result.Substitutions.Should().BeEmpty();
        result.IsFinal.Should().BeTrue();
        result.AdjustedPicks.Should().AllSatisfy(p => p.Multiplier.Should().Be(p.Position <= 11 ? 1 : 0));
    }

    [Fact]
    public void Blanked_outfield_starter_subbed_with_first_eligible_bench()
    {
        // Mid (id 5) blanked; bench order is 13(DEF),14(MID),15(FWD). 13 first eligible by formation.
        var result = AutoSubProjector.Project(StandardSquad(), Stats((5, 0)), AllTeamsFinished());

        result.Substitutions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new Substitution(5, 13));
        result.IsFinal.Should().BeTrue();
        result.AdjustedPicks.First(p => p.ElementId == 5).Multiplier.Should().Be(0);
        result.AdjustedPicks.First(p => p.ElementId == 13).Multiplier.Should().Be(1);
    }

    [Fact]
    public void Blanked_gk_only_swaps_with_bench_gk_when_he_played()
    {
        var result = AutoSubProjector.Project(StandardSquad(), Stats((1, 0)), AllTeamsFinished());

        result.Substitutions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new Substitution(1, 12));
        result.IsFinal.Should().BeTrue();
    }

    [Fact]
    public void Bench_gk_blanked_means_starter_gk_left_unsubbed()
    {
        var result = AutoSubProjector.Project(StandardSquad(), Stats((1, 0), (12, 0)), AllTeamsFinished());

        result.Substitutions.Should().BeEmpty();
        result.BlockedStarterIds.Should().ContainSingle().Which.Should().Be(1);
        result.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void Sub_blocked_when_formation_would_break()
    {
        // 3-4-3 formation, all 3 DEF starters (ids 2,3,4) blank. Bench order: 13 DEF, 14 MID, 15 FWD.
        // out=2 → 13(DEF): keeps 3 DEF, valid. After this only 14 (MID) and 15 (FWD) on the bench.
        // out=3 → 14 (MID) lands on 2 DEF/5 MID/3 FWD — DEF<3, invalid. 15 (FWD) lands on 2 DEF/4 MID/4 FWD — DEF<3, invalid. Blocked.
        // out=4 → same pool, still invalid. Blocked.
        var result = AutoSubProjector.Project(StandardSquad(), Stats((2, 0), (3, 0), (4, 0)), AllTeamsFinished());

        result.Substitutions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new Substitution(2, 13));
        result.BlockedStarterIds.Should().BeEquivalentTo(new[] { 3, 4 });
        result.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void Starter_blanked_with_team_unfinished_marks_projection_pending()
    {
        var teams = new Dictionary<int, bool> { [99] = false };
        var result = AutoSubProjector.Project(StandardSquad(), Stats((5, 0)), teams);

        result.Substitutions.Should().BeEmpty();
        result.BlockedStarterIds.Should().BeEmpty();
        result.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void Each_bench_player_used_at_most_once()
    {
        // Two blanked midfielders. Bench order: 13(DEF), 14(MID), 15(FWD).
        // out=5(MID) → 13(DEF) → 4 DEF + 3 MID + 3 FWD valid. 13 used.
        // out=6(MID) → next bench 14(MID) → 4 DEF + 3 MID + 3 FWD valid. 14 used.
        var result = AutoSubProjector.Project(StandardSquad(), Stats((5, 0), (6, 0)), AllTeamsFinished());

        result.Substitutions.Select(s => s.InElementId).Should().BeEquivalentTo(new[] { 13, 14 });
        result.IsFinal.Should().BeTrue();
    }
}
