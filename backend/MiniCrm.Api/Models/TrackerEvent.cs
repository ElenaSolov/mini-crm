namespace MiniCrm.Api.Models;

// An outbound event (the outbox row). It is saved together with the status
// change, and the worker sends it later. The URL and body are rendered once,
// when the event is created, so every retry sends exactly the same request.
public class TrackerEvent
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(10);

    public int Id { get; set; }

    // The idempotency key sent to the tracker, e.g. "conv_42". Unique.
    public string EventId { get; set; } = "";

    public int LeadId { get; set; }
    public int TrackerEndpointId { get; set; }
    public TrackerEndpoint? TrackerEndpoint { get; set; }

    public string Method { get; set; } = "POST";
    public string Url { get; set; } = "";
    public string? Body { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Set only through the methods below, so the retry rules can't be skipped.
    public TrackerEventStatus Status { get; private set; } = TrackerEventStatus.Pending;
    // Attempts since the event was last queued, used for MaxAttempts. "Convert again"
    // starts from 0, so it can be lower than Attempts.Count (the full history).
    public int AttemptCount { get; private set; }

    // When the worker should send it next. Null when the event is not pending.
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }

    // Adds "simulate":"server_error" to the next attempt only, to show retries.
    public bool SimulateServerError { get; set; }

    public List<TrackerAttempt> Attempts { get; set; } = [];

    public void Queue(DateTimeOffset now)
    {
        Status = TrackerEventStatus.Pending;
        AttemptCount = 0;
        NextAttemptAt = now;
        LastError = null;
    }

    // "Convert again": send the same event (same event_id, URL and body) once more.
    // A pending event is already on its way, so it is left alone.
    public void Requeue(bool simulateServerError, DateTimeOffset now)
    {
        if (Status == TrackerEventStatus.Pending)
            return;

        Queue(now);
        SimulateServerError = simulateServerError;
    }

    public void MarkFailed(string error)
    {
        Status = TrackerEventStatus.Failed;
        NextAttemptAt = null;
        LastError = error;
    }

    // Decides what happens after one send:
    //   2xx (201, or 200 with duplicate:true)  -> sent
    //   5xx, 429 or no answer                  -> retry later (10s, 20s, 40s...), failed after MaxAttempts
    //   other 4xx (422, 401)                   -> failed, because sending the same request again won't help
    public void RecordAttempt(TrackerAttempt attempt)
    {
        Attempts.Add(attempt);
        AttemptCount++;
        SimulateServerError = false;

        var statusCode = attempt.StatusCode;
        if (statusCode is >= 200 and < 300)
        {
            Status = TrackerEventStatus.Sent;
            NextAttemptAt = null;
            LastError = null;
            return;
        }

        var error = statusCode is null ? attempt.Error : $"Tracker answered {statusCode}.";
        var worthRetrying = statusCode is null or >= 500 or 429;

        if (worthRetrying && AttemptCount < MaxAttempts)
        {
            NextAttemptAt = attempt.AttemptedAt + FirstRetryDelay * Math.Pow(2, AttemptCount - 1);
            LastError = error;
        }
        else
        {
            MarkFailed(error ?? "Unknown error.");
        }
    }
}
