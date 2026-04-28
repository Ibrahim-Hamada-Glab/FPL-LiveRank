namespace FplLiveRank.Infrastructure.Cache;

public sealed class RedisCacheOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public string KeyPrefix { get; set; } = "fpl:";
    public bool Enabled { get; set; } = true;
}
