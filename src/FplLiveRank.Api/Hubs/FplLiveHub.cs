using Microsoft.AspNetCore.SignalR;

namespace FplLiveRank.Api.Hubs;

// Group naming is mirrored in SignalRFplLiveBroadcaster — keep them in sync.
public sealed class FplLiveHub : Hub
{
    public const string Path = "/hubs/fpl-live";

    public static string ManagerGroup(int managerId) => $"manager-{managerId}";
    public static string LeagueGroup(int leagueId) => $"league-{leagueId}";
    public static string EventGroup(int eventId) => $"event-{eventId}";

    public Task JoinManager(int managerId)
        => Groups.AddToGroupAsync(Context.ConnectionId, ManagerGroup(managerId));

    public Task LeaveManager(int managerId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, ManagerGroup(managerId));

    public Task JoinLeague(int leagueId)
        => Groups.AddToGroupAsync(Context.ConnectionId, LeagueGroup(leagueId));

    public Task LeaveLeague(int leagueId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, LeagueGroup(leagueId));

    public Task JoinEvent(int eventId)
        => Groups.AddToGroupAsync(Context.ConnectionId, EventGroup(eventId));

    public Task LeaveEvent(int eventId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, EventGroup(eventId));
}
