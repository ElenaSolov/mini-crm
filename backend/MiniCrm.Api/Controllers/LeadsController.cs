using Microsoft.AspNetCore.Mvc;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;

namespace MiniCrm.Api.Controllers;

[ApiController]
[Route("api/leads")]
public class LeadsController(LeadService leads) : ControllerBase
{
    [HttpGet]
    public async Task<List<LeadResponse>> GetAll([FromQuery] LeadStatus? status, [FromQuery] string? source)
    {
        var result = await leads.GetAll(status, source);
        return result.Select(LeadResponse.From).ToList();
    }

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

        if (result.NotFound)
            return NotFound();
        if (result.Error is not null)
            return Problem(detail: result.Error, statusCode: StatusCodes.Status409Conflict);

        return NoContent();
    }

    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<LeadResponse>> ChangeStatus(int id, ChangeStatusRequest request) =>
        ToResponse(await leads.ChangeStatus(id, request.Status!.Value));

    private ActionResult<LeadResponse> ToResponse(LeadResult result)
    {
        if (result.NotFound)
            return NotFound();
        if (result.Error is not null)
            return Problem(detail: result.Error, statusCode: StatusCodes.Status409Conflict);

        return LeadResponse.From(result.Lead!);
    }
}
