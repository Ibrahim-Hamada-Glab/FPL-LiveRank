using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

public interface IManagerLeaguesService
{
    Task<ManagerLeaguesDto> GetAsync(int managerId, CancellationToken ct = default);
}
