namespace FplLiveRank.Application.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
