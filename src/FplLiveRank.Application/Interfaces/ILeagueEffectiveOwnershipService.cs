using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

/// <summary>
/// Produces effective ownership views for a mini-league.
/// </summary>
public interface ILeagueEffectiveOwnershipService
{
    /// <summary>
    /// Returns effective ownership values for managers in a league for the given event.
    /// </summary>
    Task<LeagueEffectiveOwnershipDto> GetAsync(
        int leagueId,
        int? eventId,
        int? managerId,
        CancellationToken ct = default);
}
