using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

// Checks the ?token= of the inbound URLs. An authorization filter runs before the
// fields are read and validated, so a caller without the token gets a 401 and
// learns nothing about the fields.
public class InboundTokenAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var inbound = context.HttpContext.RequestServices.GetRequiredService<InboundLeadService>();
        string? token = context.HttpContext.Request.Query["token"];

        if (!await inbound.IsValidToken(token))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Missing or wrong token. Copy the URL from the settings page."
            }) { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }
}
