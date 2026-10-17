using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

// The settings page: the auth header and API key shared by every tracker request.
// The key can be set or cleared, but never read back.
[ApiController]
[Route("api/tracker/auth")]
public class TrackerAuthController(TrackerAuthService auth) : ControllerBase
{
    [HttpGet]
    public Task<TrackerAuthResponse> Get() => auth.Get();

    // A 400 has the same shape as the automatic validation errors.
    [HttpPut]
    public async Task<ActionResult<TrackerAuthResponse>> Update(SaveTrackerAuthRequest request)
    {
        var errors = await auth.Update(request);
        if (errors.Count == 0)
            return await auth.Get();

        foreach (var (field, error) in errors)
            ModelState.AddModelError(field, error);
        return ValidationProblem(ModelState);
    }

    [HttpPost("reset")]
    public async Task<TrackerAuthResponse> Reset()
    {
        await auth.Reset();
        return await auth.Get();
    }

    [HttpDelete("key")]
    public async Task<TrackerAuthResponse> ClearKey()
    {
        await auth.ClearKey();
        return await auth.Get();
    }
}
