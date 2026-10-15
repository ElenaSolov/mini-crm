using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Creates (or re-queues) the outbound events for a lead's new status.
// It never calls the tracker: the worker sends the events later.
public class TrackerEventQueue(AppDbContext db)
{
    // Doesn't save. The caller saves the lead and its events in one SaveChanges,
    // so the status change and the event are stored together or not at all.
    public async Task QueueFor(Lead lead, bool simulateServerError)
    {
        var endpoints = await db.TrackerEndpoints
            .Where(e => e.IsActive && e.TriggerStatus == lead.Status)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;

        foreach (var endpoint in endpoints)
        {
            var existing = await db.TrackerEvents
                .SingleOrDefaultAsync(e => e.LeadId == lead.Id && e.TrackerEndpointId == endpoint.Id);

            if (existing is null)
                db.TrackerEvents.Add(CreateEvent(lead, endpoint, simulateServerError, now));
            else
                existing.Requeue(simulateServerError, now);
        }
    }

    // Renders the URL and body now (a snapshot), so retries send the same request
    // even if the lead or the endpoint is edited later.
    private static TrackerEvent CreateEvent(Lead lead, TrackerEndpoint endpoint, bool simulateServerError, DateTimeOffset now)
    {
        var values = MacroRenderer.ValuesFor(lead, endpoint.Name, now);
        values["event_id"] = MacroRenderer.RenderEventId(endpoint.EventIdTemplate, values);

        var trackerEvent = new TrackerEvent
        {
            EventId = values["event_id"],
            LeadId = lead.Id,
            TrackerEndpointId = endpoint.Id,
            Method = endpoint.Method,
            Url = MacroRenderer.RenderUrl(endpoint.UrlTemplate, values),
            Body = endpoint.BodyTemplate is null ? null : MacroRenderer.RenderBody(endpoint.BodyTemplate, values),
            OccurredAt = now,
            CreatedAt = now,
            SimulateServerError = simulateServerError
        };
        trackerEvent.Queue(now);

        // E.g. "amount": {amount} with no amount. Keep the event, so the reason is visible.
        if (trackerEvent.Body is not null && !MacroRenderer.IsValidJson(trackerEvent.Body))
            trackerEvent.MarkFailed("The rendered body is not valid JSON. Check the endpoint's body template.");

        return trackerEvent;
    }
}
