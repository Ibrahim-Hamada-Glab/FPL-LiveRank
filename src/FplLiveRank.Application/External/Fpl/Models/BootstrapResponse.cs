using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class BootstrapResponse
{
    [JsonPropertyName("events")] public List<BootstrapEvent> Events { get; set; } = new();
    [JsonPropertyName("teams")] public List<BootstrapTeam> Teams { get; set; } = new();
    [JsonPropertyName("elements")] public List<BootstrapElement> Elements { get; set; } = new();
    [JsonPropertyName("total_players")] public int TotalPlayers { get; set; }
}

public sealed class BootstrapEvent
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("deadline_time")] public DateTimeOffset? DeadlineTime { get; set; }
    [JsonPropertyName("is_current")] public bool IsCurrent { get; set; }
    [JsonPropertyName("is_next")] public bool IsNext { get; set; }
    [JsonPropertyName("finished")] public bool Finished { get; set; }
    [JsonPropertyName("data_checked")] public bool DataChecked { get; set; }
}

public sealed class BootstrapTeam
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("short_name")] public string ShortName { get; set; } = string.Empty;
}

public sealed class BootstrapElement
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("web_name")] public string WebName { get; set; } = string.Empty;
    [JsonPropertyName("first_name")] public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("second_name")] public string SecondName { get; set; } = string.Empty;
    [JsonPropertyName("team")] public int Team { get; set; }
    [JsonPropertyName("element_type")] public int ElementType { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("now_cost")] public int NowCost { get; set; }
    [JsonPropertyName("selected_by_percent")] public string SelectedByPercent { get; set; } = "0";
}
