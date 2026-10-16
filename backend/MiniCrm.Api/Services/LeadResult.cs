using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

public enum LeadOutcome
{
    Success,
    NotFound, // 404
    Invalid,  // 400: bad input, e.g. an unknown status
    Conflict  // 409: a broken rule
}

// What a lead use case ended with. A result instead of exceptions, because
// these are normal outcomes, not crashes. Field is the input field an Invalid
// result is about, so the 400 can name it like the other validation errors.
public record LeadResult(LeadOutcome Outcome, Lead? Lead = null, string? Error = null, string? Field = null)
{
    public static LeadResult Success(Lead lead) => new(LeadOutcome.Success, lead);
    public static LeadResult Invalid(string field, string error) => new(LeadOutcome.Invalid, Error: error, Field: field);
    public static LeadResult Conflict(string error) => new(LeadOutcome.Conflict, Error: error);
    public static LeadResult Missing() => new(LeadOutcome.NotFound);
}
