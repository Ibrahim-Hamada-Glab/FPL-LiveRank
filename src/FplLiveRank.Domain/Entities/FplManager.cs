namespace FplLiveRank.Domain.Entities;

public class FplManager
{
    public int Id { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public DateTimeOffset LastSyncedAtUtc { get; set; }
}
