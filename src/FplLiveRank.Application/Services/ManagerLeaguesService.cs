using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.Application.Services;

public sealed class ManagerLeaguesService : IManagerLeaguesService
{
    private readonly IFplApiClient _fpl;
    private readonly ICacheService _cache;

    public ManagerLeaguesService(IFplApiClient fpl, ICacheService cache)
    {
        _fpl = fpl;
        _cache = cache;
    }

    public async Task<ManagerLeaguesDto> GetAsync(int managerId, CancellationToken ct = default)
    {
        if (managerId <= 0)
        {
            throw new Errors.ValidationException(
                new Dictionary<string, string[]> { [nameof(managerId)] = new[] { "Manager ID must be positive." } });
        }

        var entry = await _cache.GetOrSetAsync(
            CacheKeys.ManagerEntry(managerId),
            CacheTtl.ManagerEntry,
            inner => _fpl.GetManagerEntryAsync(managerId, inner),
            ct).ConfigureAwait(false);

        var playerName = string.Join(
            ' ',
            new[] { entry.PlayerFirstName, entry.PlayerLastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new ManagerLeaguesDto(
            ManagerId: managerId,
            PlayerName: playerName,
            TeamName: entry.Name,
            ClassicLeagues: entry.Leagues.Classic
                .Where(l => l.Id > 0)
                .Select(ToDto)
                .ToList(),
            SyncedAtUtc: DateTimeOffset.UtcNow);
    }

    private static ManagerLeagueDto ToDto(ManagerEntryClassicLeague league)
        => new(
            Id: league.Id,
            Name: league.Name,
            ShortName: league.ShortName,
            LeagueType: league.LeagueType,
            Scoring: league.Scoring,
            Rank: league.Rank,
            MaxEntries: league.MaxEntries,
            EntryCanLeave: league.EntryCanLeave,
            EntryCanAdmin: league.EntryCanAdmin,
            EntryCanInvite: league.EntryCanInvite,
            IsSystemLeague: string.Equals(league.LeagueType, "s", StringComparison.OrdinalIgnoreCase));
}
