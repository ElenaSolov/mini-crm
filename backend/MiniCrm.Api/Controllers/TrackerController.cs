using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Data;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

[ApiController]
[Route("api/tracker")]
public class TrackerController(TrackerClient tracker) : ControllerBase
{
    // Connectivity check. Returns 200 with the tracker's own status code and body
    // (so a wrong key shows up as status_code 401), or 502 if the tracker didn't answer.
    [HttpGet("ping")]
    public async Task<ActionResult<TrackerResponse>> Ping(CancellationToken cancellationToken)
    {
        var response = await tracker.Send(HttpMethod.Get, DefaultEndpoints.PingUrl, cancellationToken: cancellationToken);

        if (response.StatusCode is null)
            return Problem(detail: response.Error, statusCode: StatusCodes.Status502BadGateway);

        return response;
    }
}
