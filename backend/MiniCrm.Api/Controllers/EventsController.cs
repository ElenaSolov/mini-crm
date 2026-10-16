using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

// All outbound events, for the "Logs" page. One lead's events are at /api/leads/{id}/events.
[ApiController]
[Route("api/events")]
public class EventsController(TrackerEventService events) : ControllerBase
{
    [HttpGet]
    public async Task<TrackerEventPageResponse> GetAll([FromQuery] TrackerEventListQuery query)
    {
        var page = await events.GetAll(query);
        var items = page.Items
            .Select(e => new TrackerEventLogResponse(page.LeadNames.GetValueOrDefault(e.LeadId), TrackerEventResponse.From(e)))
            .ToList();

        return new TrackerEventPageResponse(items, query.Page, query.PageSize, page.TotalCount);
    }
}
