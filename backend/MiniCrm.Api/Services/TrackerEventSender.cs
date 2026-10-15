using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Sends the pending events that are due and records every attempt.
// The worker calls this every few seconds.
public class TrackerEventSender(AppDbContext db, TrackerClient tracker, ILogger<TrackerEventSender> logger)
{
    // The tracker allows 30 requests a minute. 2 sends every 5 seconds is at most 24.
    private const int BatchSize = 2;

    // Keeps "+" in phone numbers as is, not +.
    private static readonly JsonSerializerOptions RelaxedJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task SendDueEvents(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var dueEvents = await db.TrackerEvents
            .Where(e => e.Status == TrackerEventStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var trackerEvent in dueEvents)
            await Send(trackerEvent, cancellationToken);
    }

    private async Task Send(TrackerEvent trackerEvent, CancellationToken cancellationToken)
    {
        var body = trackerEvent.SimulateServerError ? AddSimulateServerError(trackerEvent.Body) : trackerEvent.Body;

        var response = await tracker.Send(new HttpMethod(trackerEvent.Method), trackerEvent.Url, body, cancellationToken);

        trackerEvent.RecordAttempt(new TrackerAttempt
        {
            AttemptedAt = DateTimeOffset.UtcNow,
            Url = trackerEvent.Url,
            RequestBody = body,
            StatusCode = response.StatusCode,
            ResponseBody = response.Body,
            Error = response.Error
        });

        // Saved after each send, so a crash loses at most one attempt.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Event {EventId} attempt {Attempt}: {StatusCode} -> {Status}",
            trackerEvent.EventId, trackerEvent.AttemptCount, response.StatusCode?.ToString() ?? response.Error,
            trackerEvent.Status);
    }

    // The stored body stays clean; only this one attempt gets the extra field.
    private static string? AddSimulateServerError(string? body)
    {
        if (body is null)
            return null;

        try
        {
            if (JsonNode.Parse(body) is not JsonObject json)
                return body;

            json["simulate"] = "server_error";
            return json.ToJsonString(RelaxedJson);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
