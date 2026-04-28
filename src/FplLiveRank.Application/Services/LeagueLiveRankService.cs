using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.Application.Services;

public sealed class LeagueLiveRankService : ILeagueLiveRankService
{
    private const int MaxConcurrentManagerCalculations = 8;

    private readonly IFplApiClient _fpl;
    private readonly IFplBootstrapService _bootstrap;
    private readonly IManagerLiveScoreService _managerLiveScores;
    private readonly ICacheService _cache;

    public LeagueLiveRankService(
        IFplApiClient fpl,
        IFplBootstrapService bootstrap,
        IManagerLiveScoreService managerLiveScores,
        ICacheService cache)
    {
        _fpl = fpl;
        _bootstrap = bootstrap;
        _managerLiveScores = managerLiveScores;
        _cache = cache;
    }

    public async Task<LeagueLiveRankDto> GetAsync(int leagueId, int? eventId, CancellationToken ct = default)
    {
        if (leagueId <= 0)
        {
            throw new Errors.ValidationException(
                new Dictionary<string, string[]> { [nameof(leagueId)] = new[] { "League ID must be positive." } });
        }

        var resolvedEventId = eventId ?? (await _bootstrap.GetCurrentEventAsync(ct).ConfigureAwait(false)).Id;
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
            .Select((candidate, index) => ToDto(candidate, index + 1, tiedLiveTotals.Contains(candidate.Score.LiveSeasonTotal)))
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

    private static LeagueLiveRankEntryDto ToDto(RankCandidate candidate, int liveRank, bool isTied)
    {
        var member = candidate.Member;
        var score = candidate.Score;
        var captainElementId = score.EffectiveCaptainElementId ?? score.CaptainElementId;
        var captainName = captainElementId.HasValue
            ? score.Picks.FirstOrDefault(p => p.ElementId == captainElementId.Value)?.WebName
            : null;

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
            IsTiedOnLiveTotal: isTied);
    }

    private sealed record RankCandidate(LeagueStandingResult Member, ManagerLiveDto Score);
}
