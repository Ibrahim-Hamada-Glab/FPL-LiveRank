using FplLiveRank.Application.DTOs;
using FplLiveRank.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FplLiveRank.Api.Controllers;

[ApiController]
[Route("api/fpl/events")]
public sealed class FplEventsController : ControllerBase
{
    private readonly IFplBootstrapService _bootstrap;

    public FplEventsController(IFplBootstrapService bootstrap)
    {
        _bootstrap = bootstrap;
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CurrentEventDto>> GetCurrent(CancellationToken ct)
    {
        var current = await _bootstrap.GetCurrentEventAsync(ct).ConfigureAwait(false);
        return Ok(current);
    }
}

[ApiController]
[Route("api/fpl/bootstrap")]
public sealed class FplBootstrapController : ControllerBase
{
    private readonly IFplBootstrapService _bootstrap;

    public FplBootstrapController(IFplBootstrapService bootstrap)
    {
        _bootstrap = bootstrap;
    }

    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        await _bootstrap.SyncAsync(ct).ConfigureAwait(false);
        return NoContent();
    }
}
