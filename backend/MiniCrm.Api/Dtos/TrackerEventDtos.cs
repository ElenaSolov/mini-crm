using MiniCrm.Api.Models;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Dtos;

public record TrackerEventResponse(
    int Id,
    string EventId,
    string? Endpoint,
    TrackerEventStatus Status,
    string Method,
    string Url,
    string? Body,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    string? LastError,
    List<TrackerAttemptResponse> Attempts)
{
    public static TrackerEventResponse From(TrackerEvent e) => new(
        e.Id, e.EventId, e.TrackerEndpoint?.Name, e.Status, e.Method, e.Url, e.Body, e.OccurredAt,
        e.CreatedAt, e.AttemptCount, e.NextAttemptAt, e.LastError,
        e.Attempts.Select(TrackerAttemptResponse.From).ToList());
}

public record TrackerAttemptResponse(
    int Id,
    DateTimeOffset AttemptedAt,
    string Url,
    string? RequestBody,
    int? StatusCode,
    string? ResponseBody,
    // A short line read from ResponseBody for the event log, e.g. "Provide email or phone.".
    string? ResponseMessage,
    string? Error)
{
    public static TrackerAttemptResponse From(TrackerAttempt a) => new(
        a.Id, a.AttemptedAt, a.Url, a.RequestBody, a.StatusCode, a.ResponseBody,
        TrackerResponseMessage.Describe(a.StatusCode, a.ResponseBody), a.Error);
}
