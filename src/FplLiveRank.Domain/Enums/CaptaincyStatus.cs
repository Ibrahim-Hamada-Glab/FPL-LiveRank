namespace FplLiveRank.Domain.Enums;

public enum CaptaincyStatus
{
    /// <summary>Captain played; official multiplier stands.</summary>
    CaptainPlayed,

    /// <summary>Captain has 0 minutes but his team has unfinished fixtures — multiplier may still resolve to captain.</summary>
    Projected,

    /// <summary>Captain blanked (all team fixtures finished); vice-captain promoted to receive the multiplier.</summary>
    VicePromoted,

    /// <summary>Both captain and vice blanked after all relevant fixtures finished — no double points awarded.</summary>
    NoCaptainPoints,
}
