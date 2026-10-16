namespace MiniCrm.Api.Models;

// One row of the LeadStatuses table: the list of statuses a lead can have.
// The table is the source of the list (the UI reads it through the API), so a
// plain new status can be added with a row, without a code change.
public class LeadStatus
{
    // Used by the table, by Leads.Status and by the request DTO, so they stay in step.
    public const int MaxNameLength = 20;

    // E.g. "converted". Also the key that Leads.Status points to.
    public string Name { get; set; } = "";

    // The order in dropdowns.
    public int SortOrder { get; set; }
}
