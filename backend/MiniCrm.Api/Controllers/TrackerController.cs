using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

[ApiController]
[Route("api/tracker")]
public class TrackerController(AppDbContext db, TrackerClient tracker) : ControllerBase
{
    // Connectivity check, using the "ping" row of TrackerEndpoints. Returns 200 with the
    // tracker's own status code and body (so a wrong key shows up as status_code 401),
    // or 502 if the tracker didn't answer.
    [HttpGet("ping")]
    public async Task<ActionResult<TrackerResponse>> Ping(CancellationToken cancellationToken)
    {
        var ping = await db.TrackerEndpoints
            .SingleOrDefaultAsync(e => e.Name == DefaultEndpoints.PingName && e.IsActive, cancellationToken);
        if (ping is null)
            return Problem(detail: "The ping endpoint is missing or turned off.", statusCode: StatusCodes.Status404NotFound);

        var response = await tracker.Send(new HttpMethod(ping.Method), ping.UrlTemplate, cancellationToken: cancellationToken);

        if (response.StatusCode is null)
            return Problem(detail: response.Error, statusCode: StatusCodes.Status502BadGateway);

        return response;
    }
}
