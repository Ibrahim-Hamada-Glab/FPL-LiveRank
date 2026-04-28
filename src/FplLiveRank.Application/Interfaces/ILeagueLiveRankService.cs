using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

public interface ILeagueLiveRankService
{
    Task<LeagueLiveRankDto> GetAsync(int leagueId, int? eventId, CancellationToken ct = default);
}
