using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FplLiveRank.Api.Controllers;

[ApiController]
[Route("api/fpl/manager")]
public sealed class ManagerController : ControllerBase
{
    private readonly IManagerLiveScoreService _liveScores;

    public ManagerController(IManagerLiveScoreService liveScores)
    {
        _liveScores = liveScores;
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
