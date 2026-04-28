namespace FplLiveRank.Domain.Entities;

public class ManagerGameweekPick
{
    public long Id { get; set; }
    public int ManagerId { get; set; }
    public int EventId { get; set; }
    public int ElementId { get; set; }
    public int Position { get; set; }
    public int Multiplier { get; set; }
    public bool IsCaptain { get; set; }
    public bool IsViceCaptain { get; set; }
}
