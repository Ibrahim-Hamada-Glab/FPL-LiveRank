using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Application.Services;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Application.Jobs;

// Recurring background job that keeps the FPL event caches warm during active matches.
// On each tick we re-fetch event-status, event-live and fixtures, store them in the cache
// (overwriting whatever's there), and broadcast an EventLiveRefreshed signal so connected
// clients know to re-query manager/league snapshots.
//
// This is the only place we proactively bypass the read-through cache — every other call
// site uses GetOrSetAsync, which would happily serve stale data within the TTL window.
public sealed class EventLiveRefreshJob
{
    private readonly IFplApiClient _fpl;
    private readonly IFplBootstrapService _bootstrap;
    private readonly ICacheService _cache;
    private readonly IFplLiveBroadcaster _broadcaster;
    private readonly ILogger<EventLiveRefreshJob> _logger;

    public EventLiveRefreshJob(
        IFplApiClient fpl,
        IFplBootstrapService bootstrap,
        ICacheService cache,
        IFplLiveBroadcaster broadcaster,
        ILogger<EventLiveRefreshJob> logger)
    {
        _fpl = fpl;
        _bootstrap = bootstrap;
        _cache = cache;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public const string RecurringJobId = "fpl-event-live-refresh";

    // Hangfire calls this. CancellationToken comes from Hangfire's IJobCancellationToken when wired,
    // but for simplicity we accept default and rely on the Polly timeout in the FPL client.
    public async Task RunAsync()
    {
        var ct = CancellationToken.None;
        try
        {
            var current = await _bootstrap.GetCurrentEventAsync(ct).ConfigureAwait(false);
            var eventId = current.Id;

            var statusTask = RefreshEventStatusAsync(ct);
            var liveTask = RefreshEventLiveAsync(eventId, ct);
            var fixturesTask = RefreshFixturesAsync(eventId, ct);

            await Task.WhenAll(statusTask, liveTask, fixturesTask).ConfigureAwait(false);

            await _broadcaster.EventLiveRefreshed(eventId, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            _logger.LogDebug("Event live refresh completed for event {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event live refresh failed");
            // Swallow: Hangfire will retry on its own schedule and we don't want to spam
            // the failure dashboard for transient FPL hiccups.
        }
    }

    private async Task RefreshEventStatusAsync(CancellationToken ct)
    {
        var status = await _fpl.GetEventStatusAsync(ct).ConfigureAwait(false);
        await _cache.SetAsync(CacheKeys.EventStatus, status, CacheTtl.EventStatus, ct).ConfigureAwait(false);
    }

    private async Task RefreshEventLiveAsync(int eventId, CancellationToken ct)
    {
        var live = await _fpl.GetEventLiveAsync(eventId, ct).ConfigureAwait(false);
        await _cache.SetAsync(CacheKeys.EventLive(eventId), live, CacheTtl.EventLive, ct).ConfigureAwait(false);
    }

    private async Task RefreshFixturesAsync(int eventId, CancellationToken ct)
    {
        var fixtures = (await _fpl.GetFixturesAsync(eventId, ct).ConfigureAwait(false)).ToList();
        await _cache.SetAsync(CacheKeys.EventFixtures(eventId), fixtures, CacheTtl.EventFixtures, ct).ConfigureAwait(false);
    }
}
