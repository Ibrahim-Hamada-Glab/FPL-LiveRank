using System.Text.Json;
using FplLiveRank.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FplLiveRank.Infrastructure.Cache;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Atomic compare-and-delete: only release the lock if we still own the token.
    private const string ReleaseLockScript = @"
if redis.call('get', KEYS[1]) == ARGV[1] then
    return redis.call('del', KEYS[1])
else
    return 0
end";

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

    public async Task<IAsyncDisposable?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var lockKey = Prefix("lock:" + key);
        var token = Guid.NewGuid().ToString("N");
        try
        {
            var db = _redis.GetDatabase();
            var acquired = await db.StringSetAsync(lockKey, token, ttl, When.NotExists).ConfigureAwait(false);
            return acquired ? new RedisLockHandle(_redis, lockKey, token, _logger) : null;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Cache LOCK failed for key {Key}", key);
            // If Redis is unhealthy, fall through without a lock; callers treat null as "could not acquire".
            return null;
        }
    }

    private string Prefix(string key) => _options.KeyPrefix + key;

    private sealed class RedisLockHandle : IAsyncDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly RedisKey _lockKey;
        private readonly RedisValue _token;
        private readonly ILogger _logger;
        private int _disposed;

        public RedisLockHandle(IConnectionMultiplexer redis, RedisKey lockKey, RedisValue token, ILogger logger)
        {
            _redis = redis;
            _lockKey = lockKey;
            _token = token;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                var db = _redis.GetDatabase();
                await db.ScriptEvaluateAsync(ReleaseLockScript, new[] { _lockKey }, new[] { _token }).ConfigureAwait(false);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Cache UNLOCK failed for key {Key}", (string)_lockKey!);
            }
        }
    }
}
