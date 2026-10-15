using System.ComponentModel.DataAnnotations;
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

    [EmailAddress, MaxLength(200)]
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
    [Required]
    public LeadStatus? Status { get; set; }
}

public record LeadResponse(
    int Id,
    string Name,
    string? Email,
    string? Phone,
    string? Source,
    LeadStatus Status,
    decimal? Amount,
    string? Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static LeadResponse From(Lead lead) => new(
        lead.Id, lead.Name, lead.Email, lead.Phone, lead.Source, lead.Status,
        lead.Amount, lead.Currency, lead.CreatedAt, lead.UpdatedAt);
}
