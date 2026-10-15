namespace MiniCrm.Api.Models;

// One send of an event: exactly what went out and what came back.
// Headers are not stored, so the API key never ends up in the database.
public class TrackerAttempt
{
    public int Id { get; set; }
    public int TrackerEventId { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }
    public string Url { get; set; } = "";
    public string? RequestBody { get; set; }

    // Null when the tracker didn't answer (timeout or network error); Error says why.
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? Error { get; set; }
}
