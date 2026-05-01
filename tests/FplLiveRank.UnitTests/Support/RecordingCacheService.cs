using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.UnitTests;

internal sealed class RecordingCacheService : ICacheService
{
    public List<string> RequestedKeys { get; } = new();
    public List<string> RemovedKeys { get; } = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        RequestedKeys.Add(key);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        RequestedKeys.Add(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default)
        where T : class
    {
        RequestedKeys.Add(key);
        return await factory(ct).ConfigureAwait(false);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        RemovedKeys.Add(key);
        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
        => Task.FromResult<IAsyncDisposable?>(NoOpLock.Instance);

    private sealed class NoOpLock : IAsyncDisposable
    {
        public static readonly NoOpLock Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
