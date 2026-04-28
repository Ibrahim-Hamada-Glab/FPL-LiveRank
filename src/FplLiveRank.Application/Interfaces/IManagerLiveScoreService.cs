using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

public interface IManagerLiveScoreService
{
    Task<ManagerLiveDto> GetAsync(int managerId, int? eventId, CancellationToken ct = default);
}
