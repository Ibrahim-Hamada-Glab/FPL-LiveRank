using FplLiveRank.Application.External.Fpl.Models;

namespace FplLiveRank.Application.Interfaces;

public interface IFplApiClient
{
    Task<BootstrapResponse> GetBootstrapAsync(CancellationToken ct = default);
    Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken ct = default);
    Task<IReadOnlyList<FplFixture>> GetFixturesAsync(int? eventId, CancellationToken ct = default);
    Task<ManagerEntryResponse> GetManagerEntryAsync(int managerId, CancellationToken ct = default);
    Task<PicksResponse> GetPicksAsync(int managerId, int eventId, CancellationToken ct = default);
    Task<HistoryResponse> GetHistoryAsync(int managerId, CancellationToken ct = default);
    Task<LeagueStandingsResponse> GetLeagueStandingsAsync(int leagueId, int page, CancellationToken ct = default);
    Task<EventStatusResponse> GetEventStatusAsync(CancellationToken ct = default);
}
