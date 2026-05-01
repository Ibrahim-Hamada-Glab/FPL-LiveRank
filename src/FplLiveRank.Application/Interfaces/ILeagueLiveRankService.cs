using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

public interface ILeagueLiveRankService
{
    Task<LeagueLiveRankDto> GetAsync(int leagueId, int? eventId, CancellationToken ct = default);

    // Force-recomputes the league snapshot under a distributed lock and broadcasts
    // the new table to SignalR subscribers. Used by the manual refresh endpoint
    // and the background refresh job.
    Task<LeagueLiveRankDto> RefreshAsync(int leagueId, int? eventId, CancellationToken ct = default);
}
