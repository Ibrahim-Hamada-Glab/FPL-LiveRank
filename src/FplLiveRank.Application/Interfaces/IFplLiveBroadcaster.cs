using FplLiveRank.Application.DTOs;

namespace FplLiveRank.Application.Interfaces;

public interface IFplLiveBroadcaster
{
    Task ManagerLiveScoreUpdated(ManagerLiveDto dto, CancellationToken ct = default);
    Task LeagueLiveTableUpdated(LeagueLiveRankDto dto, CancellationToken ct = default);
    Task EventLiveRefreshed(int eventId, DateTimeOffset refreshedAtUtc, CancellationToken ct = default);
    Task RefreshProgressUpdated(string scope, string status, string? detail = null, CancellationToken ct = default);
}
