using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace FplLiveRank.Api.Hubs;

public sealed class SignalRFplLiveBroadcaster : IFplLiveBroadcaster
{
    private readonly IHubContext<FplLiveHub> _hub;

    public SignalRFplLiveBroadcaster(IHubContext<FplLiveHub> hub)
    {
        _hub = hub;
    }

    public Task ManagerLiveScoreUpdated(ManagerLiveDto dto, CancellationToken ct = default)
        => _hub.Clients.Group(FplLiveHub.ManagerGroup(dto.ManagerId))
            .SendAsync("ManagerLiveScoreUpdated", dto, ct);

    public Task LeagueLiveTableUpdated(LeagueLiveRankDto dto, CancellationToken ct = default)
        => _hub.Clients.Group(FplLiveHub.LeagueGroup(dto.LeagueId))
            .SendAsync("LeagueLiveTableUpdated", dto, ct);

    public Task EventLiveRefreshed(int eventId, DateTimeOffset refreshedAtUtc, CancellationToken ct = default)
        => _hub.Clients.Group(FplLiveHub.EventGroup(eventId))
            .SendAsync("EventLiveRefreshed", new { eventId, refreshedAtUtc }, ct);

    public Task RefreshProgressUpdated(string scope, string status, string? detail = null, CancellationToken ct = default)
        => _hub.Clients.Group(scope)
            .SendAsync("RefreshProgressUpdated", new { scope, status, detail }, ct);
}
