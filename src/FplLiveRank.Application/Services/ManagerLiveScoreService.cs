using FplLiveRank.Application.Calculators;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Application.Services;

public sealed class ManagerLiveScoreService : IManagerLiveScoreService
{
    private readonly IFplApiClient _fpl;
    private readonly IFplBootstrapService _bootstrap;
    private readonly ICacheService _cache;
    private readonly ILogger<ManagerLiveScoreService> _logger;

    public ManagerLiveScoreService(
        IFplApiClient fpl,
        IFplBootstrapService bootstrap,
        ICacheService cache,
        ILogger<ManagerLiveScoreService> logger)
    {
        _fpl = fpl;
        _bootstrap = bootstrap;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ManagerLiveDto> GetAsync(int managerId, int? eventId, CancellationToken ct = default)
    {
        if (managerId <= 0) throw new Errors.ValidationException(
            new Dictionary<string, string[]> { [nameof(managerId)] = new[] { "Manager ID must be positive." } });

        var resolvedEventId = eventId ?? (await _bootstrap.GetCurrentEventAsync(ct).ConfigureAwait(false)).Id;

        var picksTask = GetPicksAsync(managerId, resolvedEventId, ct);
        var liveTask = GetEventLiveAsync(resolvedEventId, ct);
        var fixturesTask = GetFixturesAsync(resolvedEventId, ct);
        var historyTask = GetHistoryAsync(managerId, ct);
        var playersTask = _bootstrap.GetPlayersAsync(ct);

        await Task.WhenAll(picksTask, liveTask, fixturesTask, historyTask, playersTask).ConfigureAwait(false);

        var picks = picksTask.Result;
        var live = liveTask.Result;
        var fixtures = fixturesTask.Result;
        var history = historyTask.Result;
        var players = playersTask.Result;

        if (picks.Picks.Count == 0)
        {
            throw new NotFoundException(
                $"No picks available for manager {managerId} in gameweek {resolvedEventId}.",
                "Either the manager has not yet picked a team or the gameweek deadline has not passed.");
        }

        var liveStats = live.Elements.ToDictionary(
            e => e.Id,
            e => new LivePlayerStat(e.Id, e.Stats.TotalPoints, e.Stats.Minutes, e.Stats.Bonus));

        var pickInputs = picks.Picks
            .Select(p => new LivePickInput(p.Element, p.Position, p.Multiplier, p.IsCaptain, p.IsViceCaptain))
            .ToList();

        var playerTeams = players.ToDictionary(kv => kv.Key, kv => kv.Value.TeamId);
        var teamFixturesFinished = BuildTeamFinishedMap(fixtures);

        var captaincy = CaptaincyProjector.Project(pickInputs, liveStats, playerTeams, teamFixturesFinished);

        var activeChip = ChipTypeParser.Parse(picks.ActiveChip);
        var autoSubsEnabled = activeChip != ChipType.BenchBoost;

        AutoSubProjectionResult autoSub;
        if (autoSubsEnabled)
        {
            var autoSubInputs = picks.Picks.Select(p =>
            {
                players.TryGetValue(p.Element, out var pl);
                var elementType = (int?)pl?.ElementType ?? 0;
                var teamId = pl?.TeamId ?? 0;
                var adjusted = captaincy.AdjustedPicks.First(a => a.ElementId == p.Element);
                return new AutoSubPick(p.Element, p.Position, adjusted.Multiplier, elementType, teamId, p.IsCaptain, p.IsViceCaptain);
            }).ToList();

            autoSub = AutoSubProjector.Project(autoSubInputs, liveStats, teamFixturesFinished);
        }
        else
        {
            autoSub = new AutoSubProjectionResult(captaincy.AdjustedPicks, Array.Empty<Substitution>(), Array.Empty<int>(), IsFinal: true);
        }

        var transferCost = picks.EntryHistory?.EventTransfersCost ?? 0;
        var breakdown = LivePointsCalculator.Calculate(autoSub.AdjustedPicks, liveStats, transferCost);

        var previousTotal = history.Current
            .Where(c => c.Event < resolvedEventId)
            .OrderByDescending(c => c.Event)
            .Select(c => c.TotalPoints)
            .FirstOrDefault();

        var captain = picks.Picks.FirstOrDefault(p => p.IsCaptain);
        var viceCaptain = picks.Picks.FirstOrDefault(p => p.IsViceCaptain);

        var pickDtos = breakdown.Lines
            .Select(line =>
            {
                players.TryGetValue(line.ElementId, out var player);
                return new ManagerLivePickDto(
                    ElementId: line.ElementId,
                    WebName: player?.WebName ?? $"#{line.ElementId}",
                    TeamId: player?.TeamId ?? 0,
                    ElementType: (int?)player?.ElementType ?? 0,
                    Position: line.Position,
                    Multiplier: line.Multiplier,
                    IsCaptain: line.IsCaptain,
                    IsViceCaptain: line.IsViceCaptain,
                    LiveTotalPoints: line.LiveTotalPoints,
                    Minutes: line.Minutes,
                    Bonus: line.Bonus,
                    ContributedPoints: line.ContributedPoints);
            })
            .ToList();

        return new ManagerLiveDto(
            ManagerId: managerId,
            EventId: resolvedEventId,
            PlayerName: string.Empty,
            TeamName: string.Empty,
            RawLivePoints: breakdown.RawLivePoints,
            TransferCost: breakdown.TransferCost,
            LivePointsAfterHits: breakdown.LivePointsAfterHits,
            PreviousTotal: previousTotal,
            LiveSeasonTotal: previousTotal + breakdown.LivePointsAfterHits,
            ActiveChip: activeChip,
            CaptainElementId: captain?.Element,
            ViceCaptainElementId: viceCaptain?.Element,
            CaptaincyStatus: captaincy.Status,
            EffectiveCaptainElementId: captaincy.PromotedElementId,
            AutoSubs: autoSub.Substitutions.Select(s => new SubstitutionDto(s.OutElementId, s.InElementId)).ToList(),
            BlockedStarterElementIds: autoSub.BlockedStarterIds.ToList(),
            AutoSubProjectionFinal: autoSub.IsFinal,
            Picks: pickDtos,
            CalculatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static Dictionary<int, bool> BuildTeamFinishedMap(IReadOnlyList<FplFixture> fixtures)
    {
        var map = new Dictionary<int, bool>();
        foreach (var f in fixtures)
        {
            // Treat finished_provisional as authoritative since FPL flips finished only after bonus is awarded.
            var done = f.Finished || f.FinishedProvisional;
            map[f.TeamH] = map.TryGetValue(f.TeamH, out var h) ? h && done : done;
            map[f.TeamA] = map.TryGetValue(f.TeamA, out var a) ? a && done : done;
        }
        return map;
    }

    private Task<PicksResponse> GetPicksAsync(int managerId, int eventId, CancellationToken ct)
        => _cache.GetOrSetAsync(CacheKeys.ManagerPicks(managerId, eventId), CacheTtl.ManagerPicks,
            inner => _fpl.GetPicksAsync(managerId, eventId, inner), ct);

    private Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken ct)
        => _cache.GetOrSetAsync(CacheKeys.EventLive(eventId), CacheTtl.EventLive,
            inner => _fpl.GetEventLiveAsync(eventId, inner), ct);

    private Task<List<FplFixture>> GetFixturesAsync(int eventId, CancellationToken ct)
        => _cache.GetOrSetAsync(CacheKeys.EventFixtures(eventId), CacheTtl.EventFixtures,
            async inner => (await _fpl.GetFixturesAsync(eventId, inner).ConfigureAwait(false)).ToList(), ct);

    private Task<HistoryResponse> GetHistoryAsync(int managerId, CancellationToken ct)
        => _cache.GetOrSetAsync(CacheKeys.ManagerHistory(managerId), CacheTtl.ManagerHistory,
            inner => _fpl.GetHistoryAsync(managerId, inner), ct);
}
