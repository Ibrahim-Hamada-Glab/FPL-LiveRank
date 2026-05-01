using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FplLiveRank.Api.Controllers;

[ApiController]
[Route("api/fpl/league")]
public sealed class LeagueController : ControllerBase
{
    private readonly ILeagueLiveRankService _liveRanks;
    private readonly ILeagueEffectiveOwnershipService _effectiveOwnership;

    public LeagueController(
        ILeagueLiveRankService liveRanks,
        ILeagueEffectiveOwnershipService effectiveOwnership)
    {
        _liveRanks = liveRanks;
        _effectiveOwnership = effectiveOwnership;
    }

    [HttpGet("{leagueId:int}/live")]
    [ProducesResponseType(typeof(LeagueLiveRankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LeagueLiveRankDto>> GetLive(
        [FromRoute] int leagueId,
        [FromQuery] int? eventId,
        CancellationToken ct)
    {
        var dto = await _liveRanks.GetAsync(leagueId, eventId, ct).ConfigureAwait(false);
        return Ok(dto);
    }

    [HttpPost("{leagueId:int}/refresh")]
    [ProducesResponseType(typeof(LeagueLiveRankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LeagueLiveRankDto>> Refresh(
        [FromRoute] int leagueId,
        [FromQuery] int? eventId,
        CancellationToken ct)
    {
        var dto = await _liveRanks.RefreshAsync(leagueId, eventId, ct).ConfigureAwait(false);
        return Ok(dto);
    }

    [HttpGet("{leagueId:int}/effective-ownership")]
    [ProducesResponseType(typeof(LeagueEffectiveOwnershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LeagueEffectiveOwnershipDto>> GetEffectiveOwnership(
        [FromRoute] int leagueId,
        [FromQuery] int? eventId,
        [FromQuery] int? managerId,
        CancellationToken ct)
    {
        var dto = await _effectiveOwnership.GetAsync(leagueId, eventId, managerId, ct).ConfigureAwait(false);
        return Ok(dto);
    }
}
