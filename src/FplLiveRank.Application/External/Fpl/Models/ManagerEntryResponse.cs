using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class ManagerEntryResponse
{
    [JsonPropertyName("player_first_name")] public string PlayerFirstName { get; set; } = string.Empty;
    [JsonPropertyName("player_last_name")] public string PlayerLastName { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("leagues")] public ManagerEntryLeagues Leagues { get; set; } = new();
}

public sealed class ManagerEntryLeagues
{
    [JsonPropertyName("classic")] public List<ManagerEntryClassicLeague> Classic { get; set; } = new();
}

public sealed class ManagerEntryClassicLeague
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("short_name")] public string? ShortName { get; set; }
    [JsonPropertyName("league_type")] public string LeagueType { get; set; } = string.Empty;
    [JsonPropertyName("scoring")] public string Scoring { get; set; } = string.Empty;
    [JsonPropertyName("rank")] public int? Rank { get; set; }
    [JsonPropertyName("max_entries")] public int? MaxEntries { get; set; }
    [JsonPropertyName("entry_can_leave")] public bool EntryCanLeave { get; set; }
    [JsonPropertyName("entry_can_admin")] public bool EntryCanAdmin { get; set; }
    [JsonPropertyName("entry_can_invite")] public bool EntryCanInvite { get; set; }
}
