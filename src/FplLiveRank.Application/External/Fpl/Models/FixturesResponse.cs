using System.Text.Json.Serialization;

namespace FplLiveRank.Application.External.Fpl.Models;

public sealed class FplFixture
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("event")] public int? Event { get; set; }
    [JsonPropertyName("team_h")] public int TeamH { get; set; }
    [JsonPropertyName("team_a")] public int TeamA { get; set; }
    [JsonPropertyName("kickoff_time")] public DateTimeOffset? KickoffTime { get; set; }
    [JsonPropertyName("started")] public bool? Started { get; set; }
    [JsonPropertyName("finished")] public bool Finished { get; set; }
    [JsonPropertyName("finished_provisional")] public bool FinishedProvisional { get; set; }
    [JsonPropertyName("minutes")] public int Minutes { get; set; }
}
