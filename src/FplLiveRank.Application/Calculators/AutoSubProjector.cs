namespace FplLiveRank.Application.Calculators;

/// <summary>Position 1-15 + element_type (1=GK,2=DEF,3=MID,4=FWD) + team id, used by the auto-sub projector.</summary>
public sealed record AutoSubPick(
    int ElementId,
    int Position,
    int Multiplier,
    int ElementType,
    int TeamId,
    bool IsCaptain,
    bool IsViceCaptain);

public sealed record Substitution(int OutElementId, int InElementId);

public sealed record AutoSubProjectionResult(
    IReadOnlyList<LivePickInput> AdjustedPicks,
    IReadOnlyList<Substitution> Substitutions,
    IReadOnlyList<int> BlockedStarterIds,
    bool IsFinal);

/// <summary>
/// Replaces starters who blanked (0 mins, all team fixtures finished) with bench players who played,
/// subject to FPL formation constraints (1 GK, ≥3 DEF, ≥2 MID, ≥1 FWD).
///
/// Pure function — no IO, deterministic given inputs.
/// </summary>
public static class AutoSubProjector
{
    private const int Gk = 1, Def = 2, Mid = 3, Fwd = 4;

    public static AutoSubProjectionResult Project(
        IReadOnlyList<AutoSubPick> picks,
        IReadOnlyDictionary<int, LivePlayerStat> liveStats,
        IReadOnlyDictionary<int, bool> teamFixturesFinished)
    {
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(liveStats);
        ArgumentNullException.ThrowIfNull(teamFixturesFinished);

        var starters = picks.Where(p => p.Position is >= 1 and <= 11)
            .OrderBy(p => p.Position).ToList();
        var bench = picks.Where(p => p.Position is >= 12 and <= 15)
            .OrderBy(p => p.Position).ToList();

        var subs = new List<Substitution>();
        var blocked = new List<int>();

        // Mutable working set of starter ids we can still sub out, plus bench ids we still have available.
        var startingXi = starters.ToDictionary(p => p.ElementId);
        var benchAvailable = bench.ToDictionary(p => p.ElementId);
        var allFinal = true;

        foreach (var starter in starters)
        {
            if (Minutes(liveStats, starter.ElementId) > 0) continue;

            var teamDone = teamFixturesFinished.TryGetValue(starter.TeamId, out var done) && done;
            if (!teamDone)
            {
                allFinal = false;
                continue;
            }

            // Find a candidate bench player — for GK only the bench GK; for outfield the bench order 13→15.
            var candidates = starter.ElementType == Gk
                ? benchAvailable.Values.Where(b => b.ElementType == Gk).OrderBy(b => b.Position).ToList()
                : benchAvailable.Values.Where(b => b.ElementType != Gk).OrderBy(b => b.Position).ToList();

            var subbed = false;
            foreach (var cand in candidates)
            {
                if (Minutes(liveStats, cand.ElementId) == 0) continue;
                if (!teamFixturesFinished.TryGetValue(cand.TeamId, out var candTeamDone) || !candTeamDone)
                {
                    // Bench candidate's team has fixtures pending — projection may still resolve later.
                    allFinal = false;
                    continue;
                }

                if (!ResultingFormationValid(startingXi.Values, starter.ElementId, cand.ElementType))
                    continue;

                startingXi.Remove(starter.ElementId);
                startingXi[cand.ElementId] = cand;
                benchAvailable.Remove(cand.ElementId);
                subs.Add(new Substitution(starter.ElementId, cand.ElementId));
                subbed = true;
                break;
            }

            if (!subbed) blocked.Add(starter.ElementId);
        }

        var adjusted = new List<LivePickInput>(picks.Count);
        foreach (var p in picks)
        {
            var subOut = subs.FirstOrDefault(s => s.OutElementId == p.ElementId);
            var subIn = subs.FirstOrDefault(s => s.InElementId == p.ElementId);
            int multiplier = p.Multiplier;
            if (subOut is not null) multiplier = 0;
            else if (subIn is not null) multiplier = 1;

            adjusted.Add(new LivePickInput(
                ElementId: p.ElementId,
                Position: p.Position,
                Multiplier: multiplier,
                IsCaptain: p.IsCaptain,
                IsViceCaptain: p.IsViceCaptain));
        }

        return new AutoSubProjectionResult(adjusted, subs, blocked, allFinal && blocked.Count == 0);
    }

    private static int Minutes(IReadOnlyDictionary<int, LivePlayerStat> stats, int id)
        => stats.TryGetValue(id, out var s) ? s.Minutes : 0;

    private static bool ResultingFormationValid(IEnumerable<AutoSubPick> currentXi, int outId, int incomingType)
    {
        int gk = 0, def = 0, mid = 0, fwd = 0;
        foreach (var p in currentXi)
        {
            if (p.ElementId == outId) continue;
            switch (p.ElementType) { case Gk: gk++; break; case Def: def++; break; case Mid: mid++; break; case Fwd: fwd++; break; }
        }
        switch (incomingType) { case Gk: gk++; break; case Def: def++; break; case Mid: mid++; break; case Fwd: fwd++; break; }
        return gk == 1 && def >= 3 && mid >= 2 && fwd >= 1 && (gk + def + mid + fwd) == 11;
    }
}
