namespace FplLiveRank.Domain.Enums;

public enum ChipType
{
    None = 0,
    Wildcard = 1,
    FreeHit = 2,
    BenchBoost = 3,
    TripleCaptain = 4
}

public static class ChipTypeParser
{
    public static ChipType Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "wildcard" => ChipType.Wildcard,
        "freehit" => ChipType.FreeHit,
        "bboost" => ChipType.BenchBoost,
        "3xc" => ChipType.TripleCaptain,
        _ => ChipType.None
    };
}
