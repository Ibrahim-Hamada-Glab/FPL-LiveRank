using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FplLiveRank.Api.Controllers;

[ApiController]
[Route("api/fpl/manager")]
public sealed class ManagerController : ControllerBase
{
    private readonly IManagerLiveScoreService _liveScores;
    private readonly IManagerLeaguesService _managerLeagues;

    public ManagerController(IManagerLiveScoreService liveScores, IManagerLeaguesService managerLeagues)
    {
        _liveScores = liveScores;
        _managerLeagues = managerLeagues;
    }

    [HttpGet("{managerId:int}/leagues")]
    [ProducesResponseType(typeof(ManagerLeaguesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ManagerLeaguesDto>> GetLeagues(
        [FromRoute] int managerId,
        CancellationToken ct)
    {
        var dto = await _managerLeagues.GetAsync(managerId, ct).ConfigureAwait(false);
        return Ok(dto);
    }

    [HttpGet("{managerId:int}/live")]
    [ProducesResponseType(typeof(ManagerLiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ManagerLiveDto>> GetLive(
        [FromRoute] int managerId,
        [FromQuery] int? eventId,
        CancellationToken ct)
    {
        var dto = await _liveScores.GetAsync(managerId, eventId, ct).ConfigureAwait(false);
        return Ok(dto);
    }
}
