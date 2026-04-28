using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class EventStatusResponse
{
    [JsonPropertyName("status")] public List<EventStatusItem> Status { get; set; } = new();
    [JsonPropertyName("leagues")] public string? Leagues { get; set; }
}

public sealed class EventStatusItem
{
    [JsonPropertyName("bonus_added")] public bool BonusAdded { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("event")] public int Event { get; set; }
    [JsonPropertyName("points")] public string? Points { get; set; }
}
