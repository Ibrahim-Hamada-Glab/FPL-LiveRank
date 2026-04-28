namespace FplLiveRank.Domain.Entities;

public class ManagerGameweekHistory
{
    public long Id { get; set; }
    public int ManagerId { get; set; }
    public int EventId { get; set; }
    public int Points { get; set; }
    public int TotalPoints { get; set; }
    public int Rank { get; set; }
    public int OverallRank { get; set; }
    public int Bank { get; set; }
    public int Value { get; set; }
    public int EventTransfers { get; set; }
    public int EventTransfersCost { get; set; }
}
