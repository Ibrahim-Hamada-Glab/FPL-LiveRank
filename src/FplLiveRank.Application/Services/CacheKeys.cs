namespace FplLiveRank.Application.Services;

public static class CacheKeys
{
    public const string Bootstrap = "bootstrap";
    public static string EventLive(int eventId) => $"event:{eventId}:live";
    public static string EventFixtures(int eventId) => $"event:{eventId}:fixtures";
    public const string EventStatus = "event:status";
    public static string ManagerPicks(int managerId, int eventId) => $"manager:{managerId}:event:{eventId}:picks";
    public static string ManagerHistory(int managerId) => $"manager:{managerId}:history";
    public static string LeagueStandingsPage(int leagueId, int page) => $"league:{leagueId}:standings:page:{page}";
}

public static class CacheTtl
{
    public static readonly TimeSpan Bootstrap = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan EventLive = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan EventFixtures = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan EventStatus = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan ManagerPicks = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ManagerHistory = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan LeagueStandings = TimeSpan.FromMinutes(2);
}
