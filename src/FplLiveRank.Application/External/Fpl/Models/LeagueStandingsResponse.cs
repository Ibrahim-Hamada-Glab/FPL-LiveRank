using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class LeagueStandingsResponse
{
    [JsonPropertyName("last_updated_data")] public DateTimeOffset? LastUpdatedData { get; set; }
    [JsonPropertyName("league")] public LeagueInfo League { get; set; } = new();
    [JsonPropertyName("standings")] public LeagueStandings Standings { get; set; } = new();
}

public sealed class LeagueInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class LeagueStandings
{
    [JsonPropertyName("has_next")] public bool HasNext { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("results")] public List<LeagueStandingResult> Results { get; set; } = new();
}

public sealed class LeagueStandingResult
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("entry")] public int Entry { get; set; }
    [JsonPropertyName("entry_name")] public string EntryName { get; set; } = string.Empty;
    [JsonPropertyName("player_name")] public string PlayerName { get; set; } = string.Empty;
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("last_rank")] public int LastRank { get; set; }
    [JsonPropertyName("rank_sort")] public int RankSort { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("event_total")] public int EventTotal { get; set; }
}
