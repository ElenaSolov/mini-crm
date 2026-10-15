using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// What a lead use case ended with: the lead, a broken rule (409) or no such lead (404).
// A result instead of exceptions, because these are normal outcomes, not crashes.
public record LeadResult(Lead? Lead, string? Error, bool NotFound)
{
    public static LeadResult Success(Lead lead) => new(lead, null, false);
    public static LeadResult Conflict(string error) => new(null, error, false);
    public static LeadResult Missing() => new(null, null, true);
}
