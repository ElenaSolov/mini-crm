using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

// The settings page: the tracker endpoint templates and the macros they can use.
[ApiController]
[Route("api/tracker")]
public class TrackerEndpointsController(TrackerEndpointService endpoints) : ControllerBase
{
    [HttpGet("endpoints")]
    public async Task<List<TrackerEndpointResponse>> GetAll() =>
        (await endpoints.GetAll()).Select(TrackerEndpointResponse.From).ToList();

    [HttpGet("macros")]
    public MacroListResponse GetMacros() => new(MacroRenderer.AllMacros, MacroRenderer.EventIdMacros);

    // Renders the templates for a sample lead without saving. Always 200 for a known
    // endpoint: problems are listed in "errors", so the page can show them while typing.
    [HttpPost("endpoints/{id:int}/preview")]
    public async Task<ActionResult<EndpointPreview>> Preview(int id, SaveTrackerEndpointRequest request)
    {
        var preview = await endpoints.Preview(id, request);
        if (preview is null)
            return NotFound();

        return preview;
    }

    [HttpPut("endpoints/{id:int}")]
    public async Task<ActionResult<TrackerEndpointResponse>> Update(int id, SaveTrackerEndpointRequest request) =>
        ToResponse(await endpoints.Update(id, request));

    [HttpPost("endpoints/{id:int}/reset")]
    public async Task<ActionResult<TrackerEndpointResponse>> Reset(int id) =>
        ToResponse(await endpoints.Reset(id));

    // A 400 has the same shape as the automatic validation errors.
    private ActionResult<TrackerEndpointResponse> ToResponse(TrackerEndpointResult result)
    {
        switch (result.Outcome)
        {
            case EndpointOutcome.Success:
                return TrackerEndpointResponse.From(result.Endpoint!);
            case EndpointOutcome.NotFound:
                return NotFound();
            case EndpointOutcome.Invalid:
                foreach (var (field, error) in result.Errors!)
                    ModelState.AddModelError(field, error);
                return ValidationProblem(ModelState);
            case EndpointOutcome.Conflict:
                return Problem(detail: result.Error, statusCode: StatusCodes.Status409Conflict);
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown endpoint outcome.");
        }
    }
}
