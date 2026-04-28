namespace FplLiveRank.Infrastructure.External.Fpl;

public sealed class FplApiOptions
{
    public const string SectionName = "FplApi";

    public string BaseUrl { get; set; } = "https://fantasy.premierleague.com/api/";
    public string UserAgent { get; set; } = "FplLiveRank/1.0";
    public int TimeoutSeconds { get; set; } = 15;
    public int RetryCount { get; set; } = 3;
}
