using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Dtos;

// Used by both POST and PUT. It has no Status: status changes only go through
// POST /api/leads/{id}/status.
public class SaveLeadRequest : IValidatableObject
{
    // Text is cleaned as soon as it is set (trimmed, "" becomes null), so the
    // checks below see clean values: a form that sends email: "" with a phone is valid.
    private string _name = "";
    private string? _email;
    private string? _phone;
    private string? _source;
    private string? _currency;

    [Required, MaxLength(200)]
    public string Name { get => _name; set => _name = value?.Trim() ?? ""; }

    // Not [EmailAddress]: it only checks for one "@", so "a b@c.com" would pass here
    // and then fail at the tracker with a 422. This asks for a plain address:
    // words joined by single dots, "@", then a domain that ends in at least 2 letters.
    [MaxLength(200)]
    [RegularExpression(@"^[A-Za-z0-9_%+-]+(\.[A-Za-z0-9_%+-]+)*@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$",
        ErrorMessage = "Email is not valid.")]
    public string? Email { get => _email; set => _email = EmptyToNull(value); }

    // The tracker accepts at most 32 characters.
    [MaxLength(32)]
    public string? Phone { get => _phone; set => _phone = EmptyToNull(value); }

    // Lower case, so "Facebook" and "facebook" are the same source.
    [MaxLength(100)]
    public string? Source { get => _source; set => _source = EmptyToNull(value)?.ToLowerInvariant(); }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal? Amount { get; set; }

    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be 3 letters, e.g. USD.")]
    public string? Currency { get => _currency; set => _currency = EmptyToNull(value)?.ToUpperInvariant(); }

    // Field names here are the JSON names, so the 400 response says "email", not "Email".
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Email is null && Phone is null)
            yield return new ValidationResult("Email or phone is required.", ["email", "phone"]);

        if (Amount is not null && Currency is null)
            yield return new ValidationResult("Currency is required when amount is set.", ["currency"]);

        // The database keeps 2 decimals and would round anything more without telling.
        if (Amount is not null && decimal.Round(Amount.Value, 2) != Amount)
            yield return new ValidationResult("Amount can have at most 2 decimals.", ["amount"]);
    }

    public void ApplyTo(Lead lead)
    {
        lead.Name = Name;
        lead.Email = Email;
        lead.Phone = Phone;
        lead.Source = Source;
        lead.Amount = Amount;
        lead.Currency = Currency;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class ChangeStatusRequest
{
    private string? _status;

    // A name from the LeadStatuses table; the service checks that it exists.
    [Required, MaxLength(LeadStatus.MaxNameLength)]
    public string? Status { get => _status; set => _status = value?.Trim(); }

    // Only for testing retries: the first send of the conversion event gets
    // "simulate":"server_error", so the tracker answers 500 and the worker retries.
    public bool SimulateServerError { get; set; }
}

// GET /api/leads query string. Every filter is optional. Text filters match part
// of the value ("ann" finds "Joanna"); numbers and dates are ranges.
public class LeadListQuery
{
    public const int MaxPageSize = 100;

    [FromQuery(Name = "id")]
    public int? Id { get; set; }

    [FromQuery(Name = "name"), MaxLength(200)]
    public string? Name { get; set; }

    [FromQuery(Name = "email"), MaxLength(200)]
    public string? Email { get; set; }

    [FromQuery(Name = "phone"), MaxLength(32)]
    public string? Phone { get; set; }

    [FromQuery(Name = "source"), MaxLength(100)]
    public string? Source { get; set; }

    // Exact match: the UI picks it from the status dropdown.
    [FromQuery(Name = "status"), MaxLength(LeadStatus.MaxNameLength)]
    public string? Status { get; set; }

    [FromQuery(Name = "currency"), MaxLength(3)]
    public string? Currency { get; set; }

    // Same range as a lead's amount. A bigger number doesn't fit the database column (a 500).
    [FromQuery(Name = "amount_from"), Range(typeof(decimal), "0", "1000000000")]
    public decimal? AmountFrom { get; set; }

    [FromQuery(Name = "amount_to"), Range(typeof(decimal), "0", "1000000000")]
    public decimal? AmountTo { get; set; }

    // Exact moments, both included, e.g. 2026-10-16T00:00:00Z. Not whole days: a "day"
    // depends on the user's time zone, so the UI turns the picked days into moments.
    [FromQuery(Name = "created_from")]
    public DateTimeOffset? CreatedFrom { get; set; }

    [FromQuery(Name = "created_to")]
    public DateTimeOffset? CreatedTo { get; set; }

    // Capped, so (page - 1) * page_size always fits in an int.
    [FromQuery(Name = "page"), Range(1, 1_000_000)]
    public int Page { get; set; } = 1;

    // Capped, so one request can't load the whole table.
    [FromQuery(Name = "page_size"), Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;
}

public record LeadPageResponse(List<LeadResponse> Items, int Page, int PageSize, int TotalCount);

public record LeadResponse(
    int Id,
    string Name,
    string? Email,
    string? Phone,
    string? Source,
    string Status,
    decimal? Amount,
    string? Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static LeadResponse From(Lead lead) => new(
        lead.Id, lead.Name, lead.Email, lead.Phone, lead.Source, lead.Status,
        lead.Amount, lead.Currency, lead.CreatedAt, lead.UpdatedAt);
}
