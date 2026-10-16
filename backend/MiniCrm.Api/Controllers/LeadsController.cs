using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

[ApiController]
[Route("api/leads")]
public class LeadsController(LeadService leads) : ControllerBase
{
    [HttpGet]
    public async Task<LeadPageResponse> GetAll([FromQuery] LeadListQuery query)
    {
        var page = await leads.GetAll(query);
        return new LeadPageResponse(
            page.Items.Select(LeadResponse.From).ToList(), query.Page, query.PageSize, page.TotalCount);
    }

    // The statuses from the LeadStatuses table, so the UI doesn't keep its own copy of the list.
    [HttpGet("statuses")]
    public async Task<List<string>> GetStatuses() => await leads.GetStatuses();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadResponse>> GetById(int id)
    {
        var lead = await leads.GetById(id);
        if (lead is null)
            return NotFound();

        return LeadResponse.From(lead);
    }

    [HttpPost]
    public async Task<ActionResult<LeadResponse>> Create(SaveLeadRequest request)
    {
        var lead = await leads.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = lead.Id }, LeadResponse.From(lead));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LeadResponse>> Update(int id, SaveLeadRequest request) =>
        ToResponse(await leads.Update(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await leads.Delete(id);
        return ErrorResponse(result) ?? NoContent();
    }

    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<LeadResponse>> ChangeStatus(int id, ChangeStatusRequest request) =>
        ToResponse(await leads.ChangeStatus(id, request.Status!, request.SimulateServerError));

    // The lead's outbound events with every attempt: what was sent and what the tracker answered.
    [HttpGet("{id:int}/events")]
    public async Task<ActionResult<List<TrackerEventResponse>>> GetEvents(int id)
    {
        var events = await leads.GetEvents(id);
        if (events is null)
            return NotFound();

        return events.Select(TrackerEventResponse.From).ToList();
    }

    private ActionResult<LeadResponse> ToResponse(LeadResult result)
    {
        var error = ErrorResponse(result);
        if (error is not null)
            return error;

        return LeadResponse.From(result.Lead!);
    }

    // Null on success. A 400 has the same shape as the automatic validation
    // errors: the message is listed under the field it is about.
    private ActionResult? ErrorResponse(LeadResult result)
    {
        switch (result.Outcome)
        {
            case LeadOutcome.NotFound:
                return NotFound();
            case LeadOutcome.Invalid:
                ModelState.AddModelError(result.Field!, result.Error!);
                return ValidationProblem(ModelState);
            case LeadOutcome.Conflict:
                return Problem(detail: result.Error, statusCode: StatusCodes.Status409Conflict);
            case LeadOutcome.Success:
                return null;
            default:
                // A new outcome was added without a mapping here: fail loudly, not with a null lead.
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown lead outcome.");
        }
    }
}
