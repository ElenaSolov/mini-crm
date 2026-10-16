using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniCrm.Api.Services;

// Turns a stored tracker answer into one short line for the event log, so the
// user doesn't have to read raw JSON. The full body is still stored and returned.
// Only for display: whether to retry is decided by the status code (TrackerEvent.RecordAttempt).
public static class TrackerResponseMessage
{
    // Null when there is nothing useful to show (no answer, empty or non-JSON body).
    public static string? Describe(int? statusCode, string? body)
    {
        if (statusCode is null || string.IsNullOrWhiteSpace(body))
            return null;

        JsonObject? json;
        try
        {
            json = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (json is null)
            return null;

        if (statusCode is >= 200 and < 300)
            return json["duplicate"]?.GetValueKind() == JsonValueKind.True
                ? "Duplicate: the tracker already had this event_id (counts as success)."
                : "Accepted by the tracker.";

        // 401 and 500 have a "message"; 422 has "errors": { "contact": ["Provide email or phone."] }.
        return AsString(json["message"])
               ?? ValidationErrors(json["errors"] as JsonObject)
               ?? AsString(json["error"]);
    }

    private static string? ValidationErrors(JsonObject? errors)
    {
        if (errors is null)
            return null;

        var messages = errors
            .SelectMany(field => field.Value is JsonArray list ? list.ToArray() : [field.Value])
            .Select(AsString)
            .OfType<string>()
            .ToList();

        return messages.Count > 0 ? string.Join(" ", messages) : null;
    }

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
