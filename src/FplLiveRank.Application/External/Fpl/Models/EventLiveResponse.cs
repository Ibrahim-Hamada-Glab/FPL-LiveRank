using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class EventLiveResponse
{
    [JsonPropertyName("elements")] public List<EventLiveElement> Elements { get; set; } = new();
}

public sealed class EventLiveElement
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("stats")] public EventLiveStats Stats { get; set; } = new();
}

public sealed class EventLiveStats
{
    [JsonPropertyName("minutes")] public int Minutes { get; set; }
    [JsonPropertyName("goals_scored")] public int GoalsScored { get; set; }
    [JsonPropertyName("assists")] public int Assists { get; set; }
    [JsonPropertyName("clean_sheets")] public int CleanSheets { get; set; }
    [JsonPropertyName("goals_conceded")] public int GoalsConceded { get; set; }
    [JsonPropertyName("own_goals")] public int OwnGoals { get; set; }
    [JsonPropertyName("penalties_saved")] public int PenaltiesSaved { get; set; }
    [JsonPropertyName("penalties_missed")] public int PenaltiesMissed { get; set; }
    [JsonPropertyName("yellow_cards")] public int YellowCards { get; set; }
    [JsonPropertyName("red_cards")] public int RedCards { get; set; }
    [JsonPropertyName("saves")] public int Saves { get; set; }
    [JsonPropertyName("bonus")] public int Bonus { get; set; }
    [JsonPropertyName("bps")] public int Bps { get; set; }
    [JsonPropertyName("influence")] public string Influence { get; set; } = "0";
    [JsonPropertyName("creativity")] public string Creativity { get; set; } = "0";
    [JsonPropertyName("threat")] public string Threat { get; set; } = "0";
    [JsonPropertyName("ict_index")] public string IctIndex { get; set; } = "0";
    [JsonPropertyName("total_points")] public int TotalPoints { get; set; }
    [JsonPropertyName("in_dreamteam")] public bool InDreamteam { get; set; }
}
