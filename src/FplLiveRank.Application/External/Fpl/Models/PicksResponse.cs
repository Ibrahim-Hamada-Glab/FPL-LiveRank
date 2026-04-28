using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class PicksResponse
{
    [JsonPropertyName("active_chip")] public string? ActiveChip { get; set; }
    [JsonPropertyName("entry_history")] public PicksEntryHistory? EntryHistory { get; set; }
    [JsonPropertyName("picks")] public List<PicksItem> Picks { get; set; } = new();
}

public sealed class PicksEntryHistory
{
    [JsonPropertyName("event")] public int Event { get; set; }
    [JsonPropertyName("points")] public int Points { get; set; }
    [JsonPropertyName("total_points")] public int TotalPoints { get; set; }
    [JsonPropertyName("rank")] public int? Rank { get; set; }
    [JsonPropertyName("overall_rank")] public int? OverallRank { get; set; }
    [JsonPropertyName("bank")] public int Bank { get; set; }
    [JsonPropertyName("value")] public int Value { get; set; }
    [JsonPropertyName("event_transfers")] public int EventTransfers { get; set; }
    [JsonPropertyName("event_transfers_cost")] public int EventTransfersCost { get; set; }
    [JsonPropertyName("points_on_bench")] public int PointsOnBench { get; set; }
}

public sealed class PicksItem
{
    [JsonPropertyName("element")] public int Element { get; set; }
    [JsonPropertyName("position")] public int Position { get; set; }
    [JsonPropertyName("multiplier")] public int Multiplier { get; set; }
    [JsonPropertyName("is_captain")] public bool IsCaptain { get; set; }
    [JsonPropertyName("is_vice_captain")] public bool IsViceCaptain { get; set; }
}
