using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Application.Services;

public sealed class LeagueEffectiveOwnershipService : ILeagueEffectiveOwnershipService
{
    private const int MaxConcurrentManagerLoads = 8;

    private readonly ILeagueLiveRankService _leagueLiveRanks;
    private readonly IManagerLiveScoreService _managerLiveScores;
    private readonly IEffectiveOwnershipCalculator _calculator;
    private readonly ICacheService _cache;
    private readonly ILogger<LeagueEffectiveOwnershipService> _logger;

    public LeagueEffectiveOwnershipService(
        ILeagueLiveRankService leagueLiveRanks,
        IManagerLiveScoreService managerLiveScores,
        IEffectiveOwnershipCalculator calculator,
        ICacheService cache,
        ILogger<LeagueEffectiveOwnershipService> logger)
    {
        _leagueLiveRanks = leagueLiveRanks;
        _managerLiveScores = managerLiveScores;
        _calculator = calculator;
        _cache = cache;
        _logger = logger;
    }

    public async Task<LeagueEffectiveOwnershipDto> GetAsync(
        int leagueId,
        int? eventId,
        int? managerId,
        CancellationToken ct = default)
    {
        if (leagueId <= 0)
        {
            throw new ValidationException(
                new Dictionary<string, string[]> { [nameof(leagueId)] = new[] { "League ID must be positive." } });
        }

        if (managerId.HasValue && managerId.Value <= 0)
        {
            throw new ValidationException(
                new Dictionary<string, string[]> { [nameof(managerId)] = new[] { "Manager ID must be positive when provided." } });
        }

        var league = await _leagueLiveRanks.GetAsync(leagueId, eventId, ct).ConfigureAwait(false);
        if (managerId.HasValue && league.Standings.All(x => x.ManagerId != managerId.Value))
        {
            throw new ValidationException(
                new Dictionary<string, string[]> { [nameof(managerId)] = new[] { $"Manager {managerId.Value} is not in league {leagueId}." } });
        }

        var cacheKey = CacheKeys.LeagueEffectiveOwnershipSnapshot(leagueId, league.EventId);
        var cached = await _cache.GetAsync<LeagueEffectiveOwnershipDto>(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null && cached.SelectedManagerId == managerId)
        {
            return cached;
        }

        var managerScores = await LoadManagerScoresAsync(league.Standings.Select(x => x.ManagerId).ToList(), league.EventId, ct).ConfigureAwait(false);
        var players = _calculator.Calculate(managerScores, managerId);
        var result = new LeagueEffectiveOwnershipDto(
            LeagueId: league.LeagueId,
            LeagueName: league.LeagueName,
            EventId: league.EventId,
            ManagerCount: league.ManagerCount,
            SelectedManagerId: managerId,
            Players: players,
            CalculatedAtUtc: DateTimeOffset.UtcNow);

        await _cache.SetAsync(cacheKey, result, CacheTtl.LeagueEffectiveOwnershipSnapshot, ct).ConfigureAwait(false);
        return result;
    }

    private async Task<List<ManagerLiveDto>> LoadManagerScoresAsync(
        IReadOnlyList<int> managerIds,
        int eventId,
        CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentManagerLoads);
        var tasks = managerIds.Select(async managerId =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await _managerLiveScores.GetAsync(managerId, eventId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping manager {ManagerId} while calculating EO for event {EventId}", managerId, eventId);
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks).ConfigureAwait(false))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }
}
