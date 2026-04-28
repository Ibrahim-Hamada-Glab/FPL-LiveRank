using FluentAssertions;
using FplLiveRank.Infrastructure.Cache;

namespace FplLiveRank.UnitTests.Cache;

public sealed class NullCacheServiceTests
{
    [Fact]
    public async Task GetAsync_always_returns_null()
    {
        var cache = new NullCacheService();

        var result = await cache.GetAsync<object>("key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_invokes_factory_every_time()
    {
        var cache = new NullCacheService();
        var calls = 0;

        var first = await cache.GetOrSetAsync("key", TimeSpan.FromMinutes(1), _ =>
        {
            calls++;
            return Task.FromResult(new CacheValue(calls));
        });
        var second = await cache.GetOrSetAsync("key", TimeSpan.FromMinutes(1), _ =>
        {
            calls++;
            return Task.FromResult(new CacheValue(calls));
        });

        calls.Should().Be(2);
        first.Value.Should().Be(1);
        second.Value.Should().Be(2);
    }

    [Fact]
    public async Task SetAsync_and_RemoveAsync_are_noops()
    {
        var cache = new NullCacheService();

        await cache.SetAsync("key", new CacheValue(1), TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("key");
    }

    private sealed record CacheValue(int Value);
}
