namespace MiniCrm.Api.Models;

public class Lead
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Source { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    // Set only through TryChangeStatus, so the status rules can't be skipped.
    // A name from the LeadStatuses table (a foreign key keeps it valid).
    public string Status { get; private set; } = LeadStatusNames.New;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Changed by SQL Server on every update. If two requests edit the same lead
    // at once, the second save fails instead of silently overwriting the first.
    public byte[] RowVersion { get; set; } = [];

    public bool HasContact => Email is not null || Phone is not null;

    public bool TryChangeStatus(string newStatus, out string? error)
    {
        error = null;

        // Converting again is allowed: it re-queues the same conversion event.
        if (Status == LeadStatusNames.Converted && newStatus != LeadStatusNames.Converted)
            error = "A converted lead can't change status.";
        else if (newStatus == LeadStatusNames.Converted && Status == LeadStatusNames.Lost)
            error = "A lost lead can't be converted.";
        else if (newStatus == LeadStatusNames.Converted && !HasContact)
            error = "A lead needs an email or a phone to be converted.";
        else if (newStatus == LeadStatusNames.Converted && Amount is null)
            error = "A lead needs an amount to be converted.";

        if (error is not null)
            return false;

        Status = newStatus;
        return true;
    }

    // A converted lead must keep its amount, otherwise "convert again" would fail.
    public bool CanSetAmount(decimal? newAmount, out string? error)
    {
        error = Status == LeadStatusNames.Converted && newAmount is null
            ? "A converted lead must keep its amount."
            : null;

        return error is null;
    }
}
