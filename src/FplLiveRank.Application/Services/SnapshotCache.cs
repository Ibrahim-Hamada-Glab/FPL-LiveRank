using FplLiveRank.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Application.Services;

// Read-through cache for computed live snapshots. Coordinates concurrent recomputes
// via a Redis distributed lock so a cold cache doesn't fan out N copies of the
// expensive compute. If we don't get the lock we poll the snapshot key briefly
// and only fall through to compute if the lock-holder is still working past the
// poll budget — that fallback keeps us live even if Redis lock support is broken.
internal static class SnapshotCache
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    // Must be just under CacheTtl.RefreshLock (60 s) so we wait for the lock-holder
    // before falling back to an unlocked compute. Previously 8 s was far too short:
    // any large-league compute that took >8 s caused every concurrent waiter to also
    // run ComputeAsync, thundering the FPL API with redundant parallel requests.
    private static readonly TimeSpan PollBudget = TimeSpan.FromSeconds(55);

    public static async Task<T> GetOrComputeAsync<T>(
        ICacheService cache,
        string snapshotKey,
        string lockKey,
        TimeSpan snapshotTtl,
        TimeSpan lockTtl,
        Func<CancellationToken, Task<T>> compute,
        ILogger logger,
        CancellationToken ct)
        where T : class
    {
        var cached = await cache.GetAsync<T>(snapshotKey, ct).ConfigureAwait(false);
        if (cached is not null) return cached;

        await using var handle = await cache.AcquireLockAsync(lockKey, lockTtl, ct).ConfigureAwait(false);
        if (handle is null)
        {
            logger.LogDebug("Snapshot {Key}: lock held elsewhere, polling for result", snapshotKey);
            var deadline = DateTimeOffset.UtcNow + PollBudget;
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
                var polled = await cache.GetAsync<T>(snapshotKey, ct).ConfigureAwait(false);
                if (polled is not null) return polled;
            }
            logger.LogWarning("Snapshot {Key}: poll budget exhausted, computing without lock", snapshotKey);
            return await ComputeAndStoreAsync(cache, snapshotKey, snapshotTtl, compute, ct).ConfigureAwait(false);
        }

        // Double-check inside the lock: another holder may have just stored a value.
        var afterLock = await cache.GetAsync<T>(snapshotKey, ct).ConfigureAwait(false);
        if (afterLock is not null) return afterLock;

        return await ComputeAndStoreAsync(cache, snapshotKey, snapshotTtl, compute, ct).ConfigureAwait(false);
    }

    private static async Task<T> ComputeAndStoreAsync<T>(
        ICacheService cache,
        string snapshotKey,
        TimeSpan snapshotTtl,
        Func<CancellationToken, Task<T>> compute,
        CancellationToken ct)
        where T : class
    {
        var fresh = await compute(ct).ConfigureAwait(false);
        await cache.SetAsync(snapshotKey, fresh, snapshotTtl, ct).ConfigureAwait(false);
        return fresh;
    }
}
