using System.Collections.Concurrent;
using System.Text.Json;
using FplLiveRank.Application.Interfaces;

namespace FplLiveRank.UnitTests;

// Stand-in for Redis in unit tests. Mirrors RedisCacheService semantics: stores values
// as JSON strings (so we exercise serialization) and supports atomic NX-style locks.
internal sealed class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly ConcurrentDictionary<string, string> _locks = new();

    public List<string> SnapshotWrites { get; } = new();
    public int LockAcquireAttempts { get; private set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_store.TryGetValue(key, out var json))
        {
            return Task.FromResult<T?>(JsonSerializer.Deserialize<T>(json));
        }
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        SnapshotWrites.Add(key);
        _store[key] = JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) where T : class
    {
        var existing = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (existing is not null) return existing;
        var fresh = await factory(ct).ConfigureAwait(false);
        await SetAsync(key, fresh, ttl, ct).ConfigureAwait(false);
        return fresh;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable?> AcquireLockAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        LockAcquireAttempts++;
        var token = Guid.NewGuid().ToString("N");
        if (_locks.TryAdd(key, token))
        {
            IAsyncDisposable handle = new LockHandle(_locks, key, token);
            return Task.FromResult<IAsyncDisposable?>(handle);
        }
        return Task.FromResult<IAsyncDisposable?>(null);
    }

    public void Seed<T>(string key, T value) => _store[key] = JsonSerializer.Serialize(value);

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, string> _locks;
        private readonly string _key;
        private readonly string _token;

        public LockHandle(ConcurrentDictionary<string, string> locks, string key, string token)
        {
            _locks = locks;
            _key = key;
            _token = token;
        }

        public ValueTask DisposeAsync()
        {
            // Compare-and-delete: only release if we still own the token.
            if (_locks.TryGetValue(_key, out var current) && current == _token)
            {
                _locks.TryRemove(_key, out _);
            }
            return ValueTask.CompletedTask;
        }
    }
}
