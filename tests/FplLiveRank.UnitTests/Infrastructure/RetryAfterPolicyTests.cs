using System.Net.Http.Headers;
using FluentAssertions;
using FplLiveRank.Infrastructure.External.Fpl;

namespace FplLiveRank.UnitTests.Infrastructure;

public sealed class RetryAfterPolicyTests
{
    [Fact]
    public void ResolveRetryAfter_returns_null_when_header_is_missing()
    {
        FplRetryPolicies.ResolveRetryAfter(null).Should().BeNull();
    }

    [Fact]
    public void ResolveRetryAfter_returns_delta_when_provided()
    {
        var header = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

        FplRetryPolicies.ResolveRetryAfter(header).Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ResolveRetryAfter_returns_remaining_time_until_http_date()
    {
        var future = DateTimeOffset.UtcNow.AddSeconds(12);
        var header = new RetryConditionHeaderValue(future);

        var resolved = FplRetryPolicies.ResolveRetryAfter(header);

        resolved.Should().NotBeNull();
        resolved!.Value.Should().BeCloseTo(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ResolveRetryAfter_returns_zero_for_past_http_date()
    {
        var past = DateTimeOffset.UtcNow.AddSeconds(-30);
        var header = new RetryConditionHeaderValue(past);

        FplRetryPolicies.ResolveRetryAfter(header).Should().Be(TimeSpan.Zero);
    }
}
