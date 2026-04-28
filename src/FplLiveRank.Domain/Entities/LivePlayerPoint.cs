namespace FplLiveRank.Domain.Entities;

public class LivePlayerPoint
{
    public long Id { get; set; }
    public int EventId { get; set; }
    public int ElementId { get; set; }
    public int TotalPoints { get; set; }
    public int Minutes { get; set; }
    public int GoalsScored { get; set; }
    public int Assists { get; set; }
    public int CleanSheets { get; set; }
    public int GoalsConceded { get; set; }
    public int OwnGoals { get; set; }
    public int PenaltiesSaved { get; set; }
    public int PenaltiesMissed { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Saves { get; set; }
    public int Bonus { get; set; }
    public int Bps { get; set; }
    public decimal Influence { get; set; }
    public decimal Creativity { get; set; }
    public decimal Threat { get; set; }
    public decimal IctIndex { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
