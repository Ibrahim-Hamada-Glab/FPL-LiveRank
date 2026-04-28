using FplLiveRank.Domain.Enums;

namespace FplLiveRank.Domain.Entities;

public class FplPlayer
{
    public int Id { get; set; }
    public string WebName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string SecondName { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public ElementType ElementType { get; set; }
    public string Status { get; set; } = string.Empty;
    public int NowCost { get; set; }
    public decimal SelectedByPercent { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
