using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.Infrastructure.Cache;

public sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
        => Task.CompletedTask;

    public Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) where T : class
        => factory(ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IAsyncDisposable?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
        => Task.FromResult<IAsyncDisposable?>(NoOpLock.Instance);

    private sealed class NoOpLock : IAsyncDisposable
    {
        public static readonly NoOpLock Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
