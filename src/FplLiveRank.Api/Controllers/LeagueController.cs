using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FplLiveRank.Api.Controllers;

[ApiController]
[Route("api/fpl/league")]
public sealed class LeagueController : ControllerBase
{
    private readonly ILeagueLiveRankService _liveRanks;

    public LeagueController(ILeagueLiveRankService liveRanks)
    {
        _liveRanks = liveRanks;
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
}
