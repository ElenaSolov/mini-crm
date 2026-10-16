using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// One page of leads, plus how many leads match the filters in total.
// OutboundStatuses: the newest tracker event status per lead (leads without events are missing).
public record LeadPage(List<Lead> Items, Dictionary<int, TrackerEventStatus> OutboundStatuses, int TotalCount)
{
    // Null when the lead has no tracker event yet.
    public TrackerEventStatus? OutboundStatusOf(int leadId) =>
        OutboundStatuses.TryGetValue(leadId, out var status) ? status : null;
}
