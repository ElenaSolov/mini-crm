using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

// URLs the client puts in their own website form, so new leads land in the CRM.
// Same lead validation as POST /api/leads (400 on bad fields).
[ApiController]
[Route("api/inbound")]
public class InboundController(InboundLeadService inbound) : ControllerBase
{
    public const string RateLimitPolicy = "inbound";

    // A 1x1 transparent PNG.
    private static readonly byte[] PixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAALSURBVBhXY2AAAgAABQABqtXIUQAAAABJRU5ErkJggg==");

    // For the settings page: the token to put in the URLs, and the fields.
    [HttpGet("settings")]
    public async Task<InboundSettingsResponse> GetSettings() => new(await inbound.GetToken(), InboundLeadService.Fields);

    // The client picks their own token. A bad value is a 400 (see SetInboundTokenRequest).
    [HttpPut("token")]
    public async Task<InboundSettingsResponse> SetToken(SetInboundTokenRequest request) =>
        new(await inbound.SetToken(request.Token!), InboundLeadService.Fields);

    // A new random token, e.g. when the old one leaked.
    [HttpPost("token/generate")]
    public async Task<InboundSettingsResponse> GenerateToken() =>
        new(await inbound.GenerateToken(), InboundLeadService.Fields);

    // Called by the browser as an image after the form is sent:
    // <img src="/api/inbound/pixel.png?token=...&name=Ann&email=ann@example.com">
    [HttpGet("pixel.png")]
    [InboundToken, EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Pixel([FromQuery] SaveLeadRequest request)
    {
        await inbound.Create(request, InboundLeadService.PixelSource);

        // Never cached: a cached image would skip the request, and the lead with it.
        Response.Headers.CacheControl = "no-store";
        return File(PixelPng, "image/png");
    }

    // Called by an HTML form, by the client's server, or as a plain link (GET). The fields can be
    // form fields, query parameters, or both (a form field wins when both have it).
    // A body that is not a form (e.g. JSON) is a 415, so a wrong setup gets a clear answer.
    [HttpGet("postback"), HttpPost("postback")]
    [InboundToken, EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<LeadResponse>> Postback()
    {
        if (Request.ContentType is not null && !Request.HasFormContentType)
            return Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                detail: "Send the fields as form fields or query parameters, not JSON.");

        // Not a [FromForm] or [FromQuery] parameter: those read only one place. This reads
        // the form first, then the query string, and runs the same checks as other requests.
        var request = new SaveLeadRequest();
        if (!await TryUpdateModelAsync(request))
            return ValidationProblem(ModelState);

        var lead = await inbound.Create(request, InboundLeadService.PostbackSource);
        return CreatedAtAction(nameof(LeadsController.GetById), "Leads", new { id = lead.Id }, LeadResponse.From(lead));
    }
}
