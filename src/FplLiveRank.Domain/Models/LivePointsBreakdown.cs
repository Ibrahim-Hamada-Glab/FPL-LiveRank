namespace FplLiveRank.Domain.Models;

public sealed record PlayerLineLivePoints(
    int ElementId,
    int Position,
    int Multiplier,
    bool IsCaptain,
    bool IsViceCaptain,
    int LiveTotalPoints,
    int Minutes,
    int Bonus,
    int ContributedPoints);

public sealed record LiveManagerPointsBreakdown(
    int RawLivePoints,
    int TransferCost,
    int LivePointsAfterHits,
    IReadOnlyList<PlayerLineLivePoints> Lines);
