using FplLiveRank.Domain.Enums;

namespace FplLiveRank.Domain.Entities;

public class LiveManagerScore
{
    public long Id { get; set; }
    public int ManagerId { get; set; }
    public int EventId { get; set; }
    public int RawLivePoints { get; set; }
    public int TransferCost { get; set; }
    public int LivePointsAfterHits { get; set; }
    public int PreviousTotal { get; set; }
    public int LiveSeasonTotal { get; set; }
    public string? ProjectedAutoSubsJson { get; set; }
    public ChipType ActiveChip { get; set; }
    public DateTimeOffset CalculatedAtUtc { get; set; }
}
