using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.Application.Services;

public sealed class NullFplLiveBroadcaster : IFplLiveBroadcaster
{
    public Task ManagerLiveScoreUpdated(ManagerLiveDto dto, CancellationToken ct = default) => Task.CompletedTask;
    public Task LeagueLiveTableUpdated(LeagueLiveRankDto dto, CancellationToken ct = default) => Task.CompletedTask;
    public Task EventLiveRefreshed(int eventId, DateTimeOffset refreshedAtUtc, CancellationToken ct = default) => Task.CompletedTask;
    public Task RefreshProgressUpdated(string scope, string status, string? detail = null, CancellationToken ct = default) => Task.CompletedTask;
}
