using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Application.Services;

public sealed class LeagueLiveRankService : ILeagueLiveRankService
{
    private const int MaxConcurrentManagerCalculations = 8;

    private readonly IFplApiClient _fpl;
    private readonly IFplBootstrapService _bootstrap;
    private readonly IManagerLiveScoreService _managerLiveScores;
    private readonly ICacheService _cache;
    private readonly IFplLiveBroadcaster _broadcaster;
    private readonly ILogger<LeagueLiveRankService> _logger;

    public LeagueLiveRankService(
        IFplApiClient fpl,
        IFplBootstrapService bootstrap,
        IManagerLiveScoreService managerLiveScores,
        ICacheService cache,
        IFplLiveBroadcaster broadcaster,
        ILogger<LeagueLiveRankService> logger)
    {
        _fpl = fpl;
        _bootstrap = bootstrap;
        _managerLiveScores = managerLiveScores;
        _cache = cache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<LeagueLiveRankDto> GetAsync(int leagueId, int? eventId, CancellationToken ct = default)
    {
        ValidateLeagueId(leagueId);
        var resolvedEventId = eventId ?? (await _bootstrap.GetCurrentEventAsync(ct).ConfigureAwait(false)).Id;

        return await SnapshotCache.GetOrComputeAsync(
            _cache,
            CacheKeys.LeagueLiveSnapshot(leagueId, resolvedEventId),
            CacheKeys.LeagueRefreshLock(leagueId, resolvedEventId),
            CacheTtl.LeagueLiveSnapshot,
            CacheTtl.RefreshLock,
            inner => ComputeAsync(leagueId, resolvedEventId, inner),
            _logger,
            ct).ConfigureAwait(false);
    }

    public async Task<LeagueLiveRankDto> RefreshAsync(int leagueId, int? eventId, CancellationToken ct = default)
    {
        ValidateLeagueId(leagueId);
        var resolvedEventId = eventId ?? (await _bootstrap.GetCurrentEventAsync(ct).ConfigureAwait(false)).Id;

        var lockKey = CacheKeys.LeagueRefreshLock(leagueId, resolvedEventId);
        var snapshotKey = CacheKeys.LeagueLiveSnapshot(leagueId, resolvedEventId);
        var previousSnapshotKey = CacheKeys.LeagueLivePreviousSnapshot(leagueId, resolvedEventId);

        await using var handle = await _cache.AcquireLockAsync(lockKey, CacheTtl.RefreshLock, ct).ConfigureAwait(false);
        if (handle is null)
        {
            _logger.LogInformation("League {LeagueId} refresh skipped: another refresh is already in progress", leagueId);
            await _broadcaster.RefreshProgressUpdated($"league-{leagueId}", "skipped", "another refresh in progress", ct).ConfigureAwait(false);
            // Return whatever snapshot the in-flight refresh produces (poll briefly), or the existing one.
            var existing = await _cache.GetAsync<LeagueLiveRankDto>(snapshotKey, ct).ConfigureAwait(false);
            if (existing is not null) return existing;
            // Fall through to a forced compute if no snapshot exists at all.
            var fallback = await ComputeAsync(leagueId, resolvedEventId, ct).ConfigureAwait(false);
            await _cache.SetAsync(snapshotKey, fallback, CacheTtl.LeagueLiveSnapshot, ct).ConfigureAwait(false);
            return fallback;
        }

        await _broadcaster.RefreshProgressUpdated($"league-{leagueId}", "started", null, ct).ConfigureAwait(false);
        try
        {
            var previousSnapshot = await _cache.GetAsync<LeagueLiveRankDto>(snapshotKey, ct).ConfigureAwait(false);
            var fresh = await ComputeAsync(leagueId, resolvedEventId, ct).ConfigureAwait(false);
            if (previousSnapshot is not null)
            {
                await _cache.SetAsync(
                    previousSnapshotKey,
                    previousSnapshot,
                    CacheTtl.LeagueLivePreviousSnapshot,
                    ct).ConfigureAwait(false);
            }
            await _cache.SetAsync(snapshotKey, fresh, CacheTtl.LeagueLiveSnapshot, ct).ConfigureAwait(false);
            await _broadcaster.LeagueLiveTableUpdated(fresh, ct).ConfigureAwait(false);
            await _broadcaster.RefreshProgressUpdated($"league-{leagueId}", "completed", null, ct).ConfigureAwait(false);
            return fresh;
        }
        catch (Exception ex)
        {
            await _broadcaster.RefreshProgressUpdated($"league-{leagueId}", "failed", ex.Message, ct).ConfigureAwait(false);
            throw;
        }
    }

    private static void ValidateLeagueId(int leagueId)
    {
        if (leagueId <= 0)
        {
            throw new Errors.ValidationException(
                new Dictionary<string, string[]> { [nameof(leagueId)] = new[] { "League ID must be positive." } });
        }
    }

    private async Task<LeagueLiveRankDto> ComputeAsync(int leagueId, int resolvedEventId, CancellationToken ct)
    {
        var previousSnapshot = await _cache.GetAsync<LeagueLiveRankDto>(
            CacheKeys.LeagueLivePreviousSnapshot(leagueId, resolvedEventId),
            ct).ConfigureAwait(false);
        var previousRankByManager = previousSnapshot?.Standings
            .ToDictionary(x => x.ManagerId, x => x.LiveRank)
            ?? new Dictionary<int, int>();

        var pages = await GetAllStandingsPagesAsync(leagueId, ct).ConfigureAwait(false);
        var league = pages.FirstOrDefault()?.League ?? new LeagueInfo { Id = leagueId };
        var members = pages
            .SelectMany(p => p.Standings.Results)
            .Where(r => r.Entry > 0)
            .DistinctBy(r => r.Entry)
            .ToList();

        if (members.Count == 0)
        {
            throw new NotFoundException(
                $"No standings were found for league {leagueId}.",
                "The league may be empty, private, or unavailable from the public FPL endpoint.");
        }

        var candidates = await CalculateManagersAsync(members, resolvedEventId, ct).ConfigureAwait(false);
        var tiedLiveTotals = candidates
            .GroupBy(c => c.Score.LiveSeasonTotal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var ranked = candidates
            .OrderByDescending(c => c.Score.LiveSeasonTotal)
            .ThenBy(c => c.Score.TransferCost)
            .ThenBy(c => c.Member.Rank)
            .ThenBy(c => c.Member.RankSort)
            .ThenBy(c => c.Member.EntryName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = ranked
            .Select((candidate, index) => ToDto(
                candidate,
                index + 1,
                tiedLiveTotals.Contains(candidate.Score.LiveSeasonTotal),
                previousRankByManager.TryGetValue(candidate.Member.Entry, out var previousRank)
                    ? previousRank
                    : null))
            .ToList();

        return new LeagueLiveRankDto(
            LeagueId: league.Id == 0 ? leagueId : league.Id,
            LeagueName: league.Name,
            EventId: resolvedEventId,
            ManagerCount: entries.Count,
            Standings: entries,
            CalculatedAtUtc: DateTimeOffset.UtcNow);
    }

    private async Task<List<LeagueStandingsResponse>> GetAllStandingsPagesAsync(int leagueId, CancellationToken ct)
    {
        var pages = new List<LeagueStandingsResponse>();
        var page = 1;

        while (true)
        {
            var response = await GetStandingsPageAsync(leagueId, page, ct).ConfigureAwait(false);
            pages.Add(response);

            if (!response.Standings.HasNext)
            {
                return pages;
            }

            page++;
        }
    }

    private Task<LeagueStandingsResponse> GetStandingsPageAsync(int leagueId, int page, CancellationToken ct)
        => _cache.GetOrSetAsync(
            CacheKeys.LeagueStandingsPage(leagueId, page),
            CacheTtl.LeagueStandings,
            inner => _fpl.GetLeagueStandingsAsync(leagueId, page, inner),
            ct);

    private async Task<List<RankCandidate>> CalculateManagersAsync(
        IReadOnlyList<LeagueStandingResult> members,
        int eventId,
        CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentManagerCalculations);

        var tasks = members.Select(async member =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var score = await _managerLiveScores.GetAsync(member.Entry, eventId, ct).ConfigureAwait(false);
                return new RankCandidate(member, score);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
    }

    private static LeagueLiveRankEntryDto ToDto(
        RankCandidate candidate,
        int liveRank,
        bool isTied,
        int? previousLiveRank)
    {
        var member = candidate.Member;
        var score = candidate.Score;
        var captainElementId = score.EffectiveCaptainElementId ?? score.CaptainElementId;
        var captainName = captainElementId.HasValue
            ? score.Picks.FirstOrDefault(p => p.ElementId == captainElementId.Value)?.WebName
            : null;

        var rankDelta = previousLiveRank.HasValue ? previousLiveRank.Value - liveRank : 0;

        return new LeagueLiveRankEntryDto(
            ManagerId: member.Entry,
            EntryName: member.EntryName,
            PlayerName: member.PlayerName,
            OfficialRank: member.Rank,
            LiveRank: liveRank,
            RankChange: member.Rank - liveRank,
            OfficialTotal: member.Total,
            LiveTotal: score.LiveSeasonTotal,
            LiveGwPoints: score.LivePointsAfterHits,
            TransferCost: score.TransferCost,
            ActiveChip: score.ActiveChip,
            CaptainElementId: captainElementId,
            CaptainName: captainName,
            AutoSubs: score.AutoSubs,
            AutoSubProjectionFinal: score.AutoSubProjectionFinal,
            IsTiedOnLiveTotal: isTied,
            PreviousLiveRank: previousLiveRank,
            RankDeltaSincePreviousSnapshot: rankDelta,
            RankChangeExplanation: BuildRankChangeExplanation(previousLiveRank, rankDelta));
    }

    private static string BuildRankChangeExplanation(int? previousLiveRank, int rankDelta)
    {
        if (!previousLiveRank.HasValue)
        {
            return "No prior refresh snapshot yet.";
        }

        if (rankDelta > 0)
        {
            return $"Up {rankDelta} since the previous refresh.";
        }

        if (rankDelta < 0)
        {
            return $"Down {Math.Abs(rankDelta)} since the previous refresh.";
        }

        return "No rank movement since the previous refresh.";
    }

    private sealed record RankCandidate(LeagueStandingResult Member, ManagerLiveDto Score);
}
