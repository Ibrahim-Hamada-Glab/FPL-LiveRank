namespace FplLiveRank.Domain.Models;

public sealed record CaptaincyResult(
    int? EffectiveCaptainElementId,
    int Multiplier,
    bool IsProjected,
    string Reason);
