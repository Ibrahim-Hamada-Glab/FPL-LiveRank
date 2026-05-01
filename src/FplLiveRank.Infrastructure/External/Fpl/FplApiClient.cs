using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Infrastructure.External.Fpl;

public sealed class FplApiClient : IFplApiClient
{
    public const string HttpClientName = "Fpl";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<FplApiClient> _logger;

    public FplApiClient(HttpClient http, ILogger<FplApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<BootstrapResponse> GetBootstrapAsync(CancellationToken ct = default)
        => GetAsync<BootstrapResponse>("bootstrap-static/", ct);

    public Task<EventLiveResponse> GetEventLiveAsync(int eventId, CancellationToken ct = default)
        => GetAsync<EventLiveResponse>($"event/{eventId}/live/", ct);

    public async Task<IReadOnlyList<FplFixture>> GetFixturesAsync(int? eventId, CancellationToken ct = default)
    {
        var path = eventId.HasValue ? $"fixtures/?event={eventId.Value}" : "fixtures/";
        var result = await GetAsync<List<FplFixture>>(path, ct).ConfigureAwait(false);
        return result;
    }

    public Task<ManagerEntryResponse> GetManagerEntryAsync(int managerId, CancellationToken ct = default)
        => GetAsync<ManagerEntryResponse>($"entry/{managerId}/", ct);

    public Task<PicksResponse> GetPicksAsync(int managerId, int eventId, CancellationToken ct = default)
        => GetAsync<PicksResponse>($"entry/{managerId}/event/{eventId}/picks/", ct);

    public Task<HistoryResponse> GetHistoryAsync(int managerId, CancellationToken ct = default)
        => GetAsync<HistoryResponse>($"entry/{managerId}/history/", ct);

    public Task<LeagueStandingsResponse> GetLeagueStandingsAsync(int leagueId, int page, CancellationToken ct = default)
        => GetAsync<LeagueStandingsResponse>($"leagues-classic/{leagueId}/standings/?page_standings={page}", ct);

    public Task<EventStatusResponse> GetEventStatusAsync(CancellationToken ct = default)
        => GetAsync<EventStatusResponse>("event-status/", ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(path, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FplApiException(
                    $"FPL resource not found at '{path}'.",
                    requestPath: path,
                    statusCode: HttpStatusCode.NotFound);
            }

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
            if (data is null)
            {
                throw new FplApiException(
                    $"FPL response body for '{path}' was empty or null.",
                    requestPath: path,
                    statusCode: response.StatusCode);
            }
            return data;
        }
        catch (FplApiException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "FPL request failed: {Path}", path);
            throw new FplApiException($"FPL request failed for '{path}': {ex.Message}", path, ex.StatusCode, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "FPL request timed out: {Path}", path);
            throw new FplApiException($"FPL request timed out for '{path}'.", path, HttpStatusCode.RequestTimeout, ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse FPL response: {Path}", path);
            throw new FplApiException($"Failed to deserialize FPL response for '{path}'.", path, inner: ex);
        }
    }
}
