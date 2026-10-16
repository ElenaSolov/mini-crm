using MiniCrm.Api.Models;

namespace MiniCrm.Api.Data;

// The statuses from the task. The migration seeds the LeadStatuses table from here.
public static class DefaultLeadStatuses
{
    public static LeadStatus[] All() =>
    [
        new() { Name = LeadStatusNames.New, SortOrder = 1 },
        new() { Name = "contacted", SortOrder = 2 },
        new() { Name = "qualified", SortOrder = 3 },
        new() { Name = LeadStatusNames.Converted, SortOrder = 4 },
        new() { Name = LeadStatusNames.Lost, SortOrder = 5 },
    ];
}
