namespace FplLiveRank.Domain.Entities;

public class FplEvent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? DeadlineTime { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsNext { get; set; }
    public bool IsFinished { get; set; }
    public bool IsDataChecked { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
