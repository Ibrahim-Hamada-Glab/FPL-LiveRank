using System.Net;
using System.Net.Http.Json;
using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Api.Controllers;
using FplLiveRank.Api.Middleware;
using FplLiveRank.Domain.Entities;
using FplLiveRank.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Microsoft.AspNetCore.Http;

namespace FplLiveRank.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly FakeBootstrapService _bootstrap;

    public ApiSmokeTests(ApiFactory factory)
    {
        _client = factory.Client;
        _bootstrap = factory.Bootstrap;
    }

    [Fact]
    public async Task Health_returns_ok_status()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Current_event_endpoint_returns_bootstrap_event()
    {
        var response = await _client.GetAsync("/api/fpl/events/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CurrentEventDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(7);
        dto.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Manager_live_endpoint_passes_route_and_query_to_service()
    {
        var response = await _client.GetAsync("/api/fpl/manager/123/live?eventId=7");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ManagerLiveDto>();
        dto.Should().NotBeNull();
        dto!.ManagerId.Should().Be(123);
        dto.EventId.Should().Be(7);
        dto.RawLivePoints.Should().Be(42);
    }

    [Fact]
    public async Task League_live_endpoint_passes_route_and_query_to_service()
    {
        var response = await _client.GetAsync("/api/fpl/league/99/live?eventId=7");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LeagueLiveRankDto>();
        dto.Should().NotBeNull();
        dto!.LeagueId.Should().Be(99);
        dto.EventId.Should().Be(7);
        dto.Standings.Should().ContainSingle();
        dto.Standings[0].ManagerId.Should().Be(123);
    }

    [Fact]
    public async Task Bootstrap_sync_endpoint_returns_no_content()
    {
        var response = await _client.PostAsync("/api/fpl/bootstrap/sync", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _bootstrap.SyncCalls.Should().Be(1);
    }

    [Fact]
    public async Task Middleware_formats_application_validation_errors()
    {
        var response = await _client.GetAsync("/api/fpl/manager/0/live?eventId=7");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var dto = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        dto.Should().NotBeNull();
        dto!.Code.Should().Be("VALIDATION_FAILED");
        dto.ValidationErrors.Should().ContainKey("managerId");
    }
}

public sealed class ApiFactory : IDisposable
{
    public FakeBootstrapService Bootstrap { get; } = new();
    public HttpClient Client { get; }
    private readonly IHost _host;

    public ApiFactory()
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers()
                        .AddApplicationPart(typeof(ManagerController).Assembly);
                    services.AddSingleton<IFplBootstrapService>(Bootstrap);
                    services.AddSingleton<IManagerLiveScoreService, FakeManagerLiveScoreService>();
                    services.AddSingleton<ILeagueLiveRankService, FakeLeagueLiveRankService>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<ErrorHandlingMiddleware>();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapGet("/health", () => Results.Ok(new { status = "ok" }));
                    });
                });
            })
            .Start();

        Client = _host.GetTestClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}

public sealed class FakeBootstrapService : IFplBootstrapService
{
    public int SyncCalls { get; private set; }

    public Task<CurrentEventDto> GetCurrentEventAsync(CancellationToken ct = default)
        => Task.FromResult(new CurrentEventDto(7, "Gameweek 7", null, true, false, false, true));

    public Task<IReadOnlyDictionary<int, FplPlayer>> GetPlayersAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, FplPlayer>>(new Dictionary<int, FplPlayer>());

    public Task SyncAsync(CancellationToken ct = default)
    {
        SyncCalls++;
        return Task.CompletedTask;
    }
}

public sealed class FakeManagerLiveScoreService : IManagerLiveScoreService
{
    public Task<ManagerLiveDto> GetAsync(int managerId, int? eventId, CancellationToken ct = default)
    {
        if (managerId <= 0)
        {
            throw new Application.Errors.ValidationException(
                new Dictionary<string, string[]> { ["managerId"] = new[] { "Manager ID must be positive." } });
        }

        return Task.FromResult(new ManagerLiveDto(
            ManagerId: managerId,
            EventId: eventId ?? 7,
            PlayerName: "Test Player",
            TeamName: "Test Team",
            RawLivePoints: 42,
            TransferCost: 4,
            LivePointsAfterHits: 38,
            PreviousTotal: 100,
            LiveSeasonTotal: 138,
            ActiveChip: ChipType.None,
            CaptainElementId: 10,
            ViceCaptainElementId: 20,
            CaptaincyStatus: CaptaincyStatus.CaptainPlayed,
            EffectiveCaptainElementId: 10,
            AutoSubs: Array.Empty<SubstitutionDto>(),
            BlockedStarterElementIds: Array.Empty<int>(),
            AutoSubProjectionFinal: true,
            Picks: new List<ManagerLivePickDto>
            {
                new(10, "Captain", 1, (int)ElementType.Midfielder, 1, 2, true, false, 21, 90, 3, 42)
            },
            CalculatedAtUtc: DateTimeOffset.UtcNow));
    }
}

public sealed class FakeLeagueLiveRankService : ILeagueLiveRankService
{
    public Task<LeagueLiveRankDto> GetAsync(int leagueId, int? eventId, CancellationToken ct = default)
    {
        if (leagueId <= 0)
        {
            throw new Application.Errors.ValidationException(
                new Dictionary<string, string[]> { ["leagueId"] = new[] { "League ID must be positive." } });
        }

        return Task.FromResult(new LeagueLiveRankDto(
            LeagueId: leagueId,
            LeagueName: "Test League",
            EventId: eventId ?? 7,
            ManagerCount: 1,
            Standings: new List<LeagueLiveRankEntryDto>
            {
                new(
                    ManagerId: 123,
                    EntryName: "Test Team",
                    PlayerName: "Test Player",
                    OfficialRank: 2,
                    LiveRank: 1,
                    RankChange: 1,
                    OfficialTotal: 100,
                    LiveTotal: 138,
                    LiveGwPoints: 38,
                    TransferCost: 4,
                    ActiveChip: ChipType.None,
                    CaptainElementId: 10,
                    CaptainName: "Captain",
                    AutoSubs: Array.Empty<SubstitutionDto>(),
                    AutoSubProjectionFinal: true,
                    IsTiedOnLiveTotal: false)
            },
            CalculatedAtUtc: DateTimeOffset.UtcNow));
    }
}
