using System.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;

namespace FplLiveRank.Infrastructure.External.Fpl;

/// <summary>
/// Polly policies for the FPL HTTP client. Kept in its own class (not buried in DI)
/// so unit tests can exercise the Retry-After resolution rules without booting the host.
/// </summary>
public static class FplRetryPolicies
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Builds an exponential-backoff retry policy that also honors a <c>Retry-After</c>
    /// header (delta-seconds or HTTP-date) on 429/503 responses, capped at 30 seconds.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> Build(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount,
                sleepDurationProvider: (attempt, response, _) =>
                {
                    var retryAfter = ResolveRetryAfter(response.Result?.Headers.RetryAfter);
                    var fallback = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200);
                    var chosen = retryAfter ?? fallback;
                    return chosen > MaxRetryDelay ? MaxRetryDelay : chosen;
                },
                onRetryAsync: (_, _, _, _) => Task.CompletedTask);

    /// <summary>
    /// Resolves a wait time from a <c>Retry-After</c> response header.
    /// Returns <c>null</c> when the header is absent.
    /// </summary>
    public static TimeSpan? ResolveRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header is null) return null;
        if (header.Delta.HasValue) return header.Delta.Value;
        if (header.Date.HasValue)
        {
            var delta = header.Date.Value.UtcDateTime - DateTime.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        return null;
    }
}
