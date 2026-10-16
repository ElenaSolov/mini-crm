using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

public enum EndpointOutcome
{
    Success,
    NotFound, // 404
    Invalid,  // 400: a template breaks a macro rule
    Conflict  // 409: e.g. no default to reset to
}

// What a settings use case ended with, like LeadResult. Errors are per field
// ("url_template", "body_template", "event_id_template"), so the 400 can list them.
public record TrackerEndpointResult(
    EndpointOutcome Outcome,
    TrackerEndpoint? Endpoint = null,
    Dictionary<string, string>? Errors = null,
    string? Error = null)
{
    public static TrackerEndpointResult Success(TrackerEndpoint endpoint) => new(EndpointOutcome.Success, endpoint);
    public static TrackerEndpointResult Invalid(Dictionary<string, string> errors) => new(EndpointOutcome.Invalid, Errors: errors);
    public static TrackerEndpointResult Conflict(string error) => new(EndpointOutcome.Conflict, Error: error);
    public static TrackerEndpointResult Missing() => new(EndpointOutcome.NotFound);
}

// The request a template would send for a sample lead, plus what is wrong with it.
// Errors is empty when the templates can be saved.
public record EndpointPreview(string? EventId, string Url, string? Body, Dictionary<string, string> Errors);
