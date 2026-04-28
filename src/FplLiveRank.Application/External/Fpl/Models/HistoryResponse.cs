using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class HistoryResponse
{
    [JsonPropertyName("current")] public List<HistoryCurrentItem> Current { get; set; } = new();
    [JsonPropertyName("chips")] public List<HistoryChip> Chips { get; set; } = new();
}

public sealed class HistoryCurrentItem
{
    [JsonPropertyName("event")] public int Event { get; set; }
    [JsonPropertyName("points")] public int Points { get; set; }
    [JsonPropertyName("total_points")] public int TotalPoints { get; set; }
    [JsonPropertyName("rank")] public int? Rank { get; set; }
    [JsonPropertyName("overall_rank")] public int? OverallRank { get; set; }
    [JsonPropertyName("event_transfers")] public int EventTransfers { get; set; }
    [JsonPropertyName("event_transfers_cost")] public int EventTransfersCost { get; set; }
}

public sealed class HistoryChip
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("event")] public int Event { get; set; }
}
