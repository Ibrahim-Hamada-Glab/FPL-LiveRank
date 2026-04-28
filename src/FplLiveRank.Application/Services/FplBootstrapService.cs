using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Errors;
using FplLiveRank.Application.External.Fpl.Models;
using FplLiveRank.Application.Interfaces;
using FplLiveRank.Domain.Entities;
using FplLiveRank.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FplLiveRank.Application.Services;

public sealed class FplBootstrapService : IFplBootstrapService
{
    private readonly IFplApiClient _fpl;
    private readonly ICacheService _cache;
    private readonly ILogger<FplBootstrapService> _logger;

    public FplBootstrapService(IFplApiClient fpl, ICacheService cache, ILogger<FplBootstrapService> logger)
    {
        _fpl = fpl;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CurrentEventDto> GetCurrentEventAsync(CancellationToken ct = default)
    {
        var bootstrap = await GetBootstrapAsync(ct).ConfigureAwait(false);
        var current = bootstrap.Events.FirstOrDefault(e => e.IsCurrent)
            ?? bootstrap.Events.FirstOrDefault(e => e.IsNext)
            ?? throw new NotFoundException("No current or next gameweek found.");

        return new CurrentEventDto(
            Id: current.Id,
            Name: current.Name,
            DeadlineTime: current.DeadlineTime,
            IsCurrent: current.IsCurrent,
            IsNext: current.IsNext,
            IsFinished: current.Finished,
            IsDataChecked: current.DataChecked);
    }

    public async Task<IReadOnlyDictionary<int, FplPlayer>> GetPlayersAsync(CancellationToken ct = default)
    {
        var bootstrap = await GetBootstrapAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        return bootstrap.Elements.ToDictionary(
            e => e.Id,
            e => new FplPlayer
            {
                Id = e.Id,
                WebName = e.WebName,
                FirstName = e.FirstName,
                SecondName = e.SecondName,
                TeamId = e.Team,
                ElementType = (ElementType)e.ElementType,
                Status = e.Status,
                NowCost = e.NowCost,
                SelectedByPercent = decimal.TryParse(e.SelectedByPercent, System.Globalization.CultureInfo.InvariantCulture, out var sbp) ? sbp : 0m,
                UpdatedAtUtc = now
            });
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        await _cache.RemoveAsync(CacheKeys.Bootstrap, ct).ConfigureAwait(false);
        await GetBootstrapAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Bootstrap cache refreshed");
    }

    private Task<BootstrapResponse> GetBootstrapAsync(CancellationToken ct)
        => _cache.GetOrSetAsync(CacheKeys.Bootstrap, CacheTtl.Bootstrap,
            inner => _fpl.GetBootstrapAsync(inner), ct);
}
