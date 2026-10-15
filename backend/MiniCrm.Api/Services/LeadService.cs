using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Lead use cases. The lead rules themselves live in Lead; this class loads
// and saves leads around them.
public class LeadService(AppDbContext db)
{
    private const string EditedByAnotherRequest = "The lead was changed by another request. Reload it and try again.";

    public async Task<List<Lead>> GetAll(LeadStatus? status, string? source)
    {
        var query = db.Leads.AsNoTracking();

        if (status is not null)
            query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(source))
        {
            // Sources are saved trimmed and in lower case, so compare the same way.
            var cleanSource = source.Trim().ToLowerInvariant();
            query = query.Where(l => l.Source == cleanSource);
        }

        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    public async Task<Lead?> GetById(int id) => await db.Leads.FindAsync(id);

    public async Task<Lead> Create(SaveLeadRequest request)
    {
        var lead = new Lead { CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        request.ApplyTo(lead);

        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        return lead;
    }

    public async Task<LeadResult> Update(int id, SaveLeadRequest request)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null)
            return LeadResult.Missing();

        if (!lead.CanSetAmount(request.Amount, out var error))
            return LeadResult.Conflict(error!);

        request.ApplyTo(lead);
        lead.UpdatedAt = DateTimeOffset.UtcNow;

        return await Save(lead);
    }

    public async Task<LeadResult> Delete(int id)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null)
            return LeadResult.Missing();

        db.Leads.Remove(lead);

        return await Save(lead);
    }

    public async Task<LeadResult> ChangeStatus(int id, LeadStatus newStatus)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null)
            return LeadResult.Missing();

        if (!lead.TryChangeStatus(newStatus, out var error))
            return LeadResult.Conflict(error!);

        lead.UpdatedAt = DateTimeOffset.UtcNow;

        return await Save(lead);
    }

    private async Task<LeadResult> Save(Lead lead)
    {
        try
        {
            await db.SaveChangesAsync();
            return LeadResult.Success(lead);
        }
        catch (DbUpdateConcurrencyException)
        {
            return LeadResult.Conflict(EditedByAnotherRequest);
        }
    }
}
