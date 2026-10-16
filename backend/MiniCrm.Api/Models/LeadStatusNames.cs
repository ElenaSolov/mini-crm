namespace MiniCrm.Api.Models;

// The statuses the code has rules for (see Lead.TryChangeStatus). They are rows
// in the LeadStatuses table like any other status, but these rows must exist
// and must not be renamed.
public static class LeadStatusNames
{
    public const string New = "new";
    public const string Converted = "converted";
    public const string Lost = "lost";
}
