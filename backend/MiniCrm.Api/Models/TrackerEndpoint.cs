namespace MiniCrm.Api.Models;

// One tracker URL the app can call. The URL, body and event_id are templates
// with {macro} placeholders, filled in from the lead when an event is created.
public class TrackerEndpoint
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Method { get; set; } = "POST";

    // When a lead moves to this status, an event is queued for this endpoint.
    // Null means the endpoint is never triggered by a lead (e.g. ping).
    public LeadStatus? TriggerStatus { get; set; }

    public string UrlTemplate { get; set; } = "";
    public string? BodyTemplate { get; set; }

    // Only stable macros ({lead_id}, {event_type}), so the same conversion
    // always gets the same event_id.
    public string EventIdTemplate { get; set; } = "";

    public bool IsActive { get; set; } = true;
}
