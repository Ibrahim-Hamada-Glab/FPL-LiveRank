using FplLiveRank.Application.DTOs;
using FplLiveRank.Domain.Entities;

namespace FplLiveRank.Application.Interfaces;

public interface IFplBootstrapService
{
    Task<CurrentEventDto> GetCurrentEventAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, FplPlayer>> GetPlayersAsync(CancellationToken ct = default);
    Task SyncAsync(CancellationToken ct = default);
}
