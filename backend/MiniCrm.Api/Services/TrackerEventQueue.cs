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
                Requeue(existing, lead, endpoint, simulateServerError, now);
        }
    }

    // Renders the URL and body now (a snapshot), so automatic retries send the same
    // request even if the lead or the endpoint is edited later.
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
        FailIfBodyIsInvalid(trackerEvent);

        return trackerEvent;
    }

    // "Convert again": the URL and body are rendered again from the current endpoint
    // and lead, so a fixed URL or email is used. The event_id and occurred_at stay the
    // same, so the tracker still sees the same conversion.
    private static void Requeue(TrackerEvent trackerEvent, Lead lead, TrackerEndpoint endpoint, bool simulateServerError, DateTimeOffset now)
    {
        var values = MacroRenderer.ValuesFor(lead, endpoint.Name, trackerEvent.OccurredAt);
        values["event_id"] = trackerEvent.EventId;

        var url = MacroRenderer.RenderUrl(endpoint.UrlTemplate, values);
        var body = endpoint.BodyTemplate is null ? null : MacroRenderer.RenderBody(endpoint.BodyTemplate, values);

        trackerEvent.Requeue(url, body, simulateServerError, now);
        FailIfBodyIsInvalid(trackerEvent);
    }

    // E.g. "amount": {amount} with no amount. Keep the event, so the reason is visible.
    private static void FailIfBodyIsInvalid(TrackerEvent trackerEvent)
    {
        if (trackerEvent.Body is not null && !MacroRenderer.IsValidJson(trackerEvent.Body))
            trackerEvent.MarkFailed("The rendered body is not valid JSON. Check the endpoint's body template.");
    }
}
