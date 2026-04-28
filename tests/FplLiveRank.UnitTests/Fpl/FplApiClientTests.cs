using System.Net;
using System.Text;
using FluentAssertions;
using FplLiveRank.Infrastructure.External.Fpl;
using Microsoft.Extensions.Logging.Abstractions;

namespace FplLiveRank.UnitTests.Fpl;

public sealed class FplApiClientTests
{
    private static FplApiClient CreateClient(
        HttpStatusCode status,
        string body,
        out List<string> capturedPaths)
    {
        var paths = new List<string>();
        capturedPaths = paths;
        var handler = new StubHandler((req, _) =>
        {
            paths.Add(req.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://fpl.test/api/") };
        return new FplApiClient(http, NullLogger<FplApiClient>.Instance);
    }

    [Fact]
    public async Task GetBootstrap_parses_events_and_elements()
    {
        const string body = """
        {
          "events": [{ "id": 30, "name": "Gameweek 30", "is_current": true, "is_next": false, "finished": false, "data_checked": false }],
          "teams": [{ "id": 1, "name": "Arsenal", "short_name": "ARS" }],
          "elements": [{ "id": 100, "web_name": "Saka", "first_name": "Bukayo", "second_name": "Saka", "team": 1, "element_type": 3, "status": "a", "now_cost": 95, "selected_by_percent": "42.1" }],
          "total_players": 9000000
        }
        """;
        var client = CreateClient(HttpStatusCode.OK, body, out var paths);

        var result = await client.GetBootstrapAsync();

        paths.Should().ContainSingle().Which.Should().EndWith("bootstrap-static/");
        result.Events.Should().HaveCount(1);
        result.Events[0].IsCurrent.Should().BeTrue();
        result.Elements.Should().HaveCount(1);
        result.Elements[0].WebName.Should().Be("Saka");
    }

    [Fact]
    public async Task GetEventLive_parses_player_stats()
    {
        const string body = """
        {
          "elements": [
            { "id": 100, "stats": { "minutes": 90, "goals_scored": 1, "assists": 1, "clean_sheets": 0, "goals_conceded": 1, "own_goals": 0, "penalties_saved": 0, "penalties_missed": 0, "yellow_cards": 0, "red_cards": 0, "saves": 0, "bonus": 2, "bps": 28, "influence": "40.0", "creativity": "20.0", "threat": "30.0", "ict_index": "9.0", "total_points": 9, "in_dreamteam": false } }
          ]
        }
        """;
        var client = CreateClient(HttpStatusCode.OK, body, out var paths);

        var result = await client.GetEventLiveAsync(30);

        paths.Should().ContainSingle().Which.Should().EndWith("event/30/live/");
        result.Elements[0].Stats.TotalPoints.Should().Be(9);
        result.Elements[0].Stats.Bonus.Should().Be(2);
    }

    [Fact]
    public async Task NotFound_throws_FplApiException_with_404()
    {
        var client = CreateClient(HttpStatusCode.NotFound, "{}", out _);

        var act = async () => await client.GetPicksAsync(123, 30);

        var ex = await act.Should().ThrowAsync<FplApiException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ServerError_throws_FplApiException()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, "boom", out _);

        var act = async () => await client.GetBootstrapAsync();

        await act.Should().ThrowAsync<FplApiException>();
    }

    [Fact]
    public async Task GetFixtures_with_event_query_includes_event_param()
    {
        var client = CreateClient(HttpStatusCode.OK, "[]", out var paths);

        await client.GetFixturesAsync(30);

        paths.Should().ContainSingle().Which.Should().EndWith("fixtures/?event=30");
    }

    [Fact]
    public async Task GetLeagueStandings_parses_paginated_standings()
    {
        const string body = """
        {
          "league": { "id": 314, "name": "Work League" },
          "standings": {
            "has_next": true,
            "page": 2,
            "results": [
              { "id": 9, "entry": 123, "entry_name": "Test FC", "player_name": "Test Manager", "rank": 4, "last_rank": 5, "rank_sort": 4, "total": 999, "event_total": 55 }
            ]
          }
        }
        """;
        var client = CreateClient(HttpStatusCode.OK, body, out var paths);

        var result = await client.GetLeagueStandingsAsync(314, page: 2);

        paths.Should().ContainSingle().Which.Should().EndWith("leagues-classic/314/standings/?page_standings=2");
        result.League.Name.Should().Be("Work League");
        result.Standings.HasNext.Should().BeTrue();
        result.Standings.Results[0].Entry.Should().Be(123);
        result.Standings.Results[0].EntryName.Should().Be("Test FC");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _func;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
        {
            _func = func;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _func(request, cancellationToken);
    }
}
