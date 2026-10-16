using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Dtos;

public record TrackerEventResponse(
    int Id,
    string EventId,
    int LeadId,
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
        e.Id, e.EventId, e.LeadId, e.TrackerEndpoint?.Name, e.Status, e.Method, e.Url, e.Body, e.OccurredAt,
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

// GET /api/events. Status is optional: "pending", "sent" or "failed"; anything else is a 400.
public class TrackerEventListQuery
{
    [FromQuery(Name = "status")]
    public TrackerEventStatus? Status { get; set; }

    [FromQuery(Name = "page"), Range(1, 1_000_000)]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "page_size"), Range(1, LeadListQuery.MaxPageSize)]
    public int PageSize { get; set; } = 10;
}

public record TrackerEventLogResponse(string? LeadName, TrackerEventResponse Event);

public record TrackerEventPageResponse(List<TrackerEventLogResponse> Items, int Page, int PageSize, int TotalCount);
