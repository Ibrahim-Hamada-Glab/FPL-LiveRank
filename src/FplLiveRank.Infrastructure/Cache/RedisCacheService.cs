using System.Text.Json;
using FplLiveRank.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FplLiveRank.Infrastructure.Cache;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly RedisCacheOptions _options;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisCacheOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(Prefix(key)).ConfigureAwait(false);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(Prefix(key), payload, ttl).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) where T : class
    {
        var cached = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (cached is not null) return cached;

        var fresh = await factory(ct).ConfigureAwait(false);
        await SetAsync(key, fresh, ttl, ct).ConfigureAwait(false);
        return fresh;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(Prefix(key)).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Cache DEL failed for key {Key}", key);
        }
    }

    private string Prefix(string key) => _options.KeyPrefix + key;
}
