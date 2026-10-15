using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Lead use cases. The lead rules themselves live in Lead; this class loads
// and saves leads around them.
public class LeadService(AppDbContext db, TrackerEventQueue eventQueue)
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

        if (await db.TrackerEvents.AnyAsync(e => e.LeadId == id))
            return LeadResult.Conflict("This lead has tracker events, so it can't be deleted. They are kept as an audit trail.");

        db.Leads.Remove(lead);

        return await Save(lead);
    }

    public async Task<LeadResult> ChangeStatus(int id, LeadStatus newStatus, bool simulateServerError = false)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null)
            return LeadResult.Missing();

        if (!lead.TryChangeStatus(newStatus, out var error))
            return LeadResult.Conflict(error!);

        lead.UpdatedAt = DateTimeOffset.UtcNow;

        // Adds the outbound event (e.g. conv_42) to the same save as the status change.
        await eventQueue.QueueFor(lead, simulateServerError);

        return await Save(lead);
    }

    // Null when there is no such lead. Oldest first, attempts too, so it reads like a log.
    public async Task<List<TrackerEvent>?> GetEvents(int id)
    {
        if (!await db.Leads.AnyAsync(l => l.Id == id))
            return null;

        return await db.TrackerEvents
            .AsNoTracking()
            .Include(e => e.TrackerEndpoint)
            .Include(e => e.Attempts.OrderBy(a => a.Id))
            .Where(e => e.LeadId == id)
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    private async Task<LeadResult> Save(Lead lead)
    {
        try
        {
            await db.SaveChangesAsync();
            return LeadResult.Success(lead);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsDuplicateKey(ex))
        {
            // A duplicate key happens when two "convert" requests for the same lead
            // both try to create its event at the same time. Only one wins.
            return LeadResult.Conflict(EditedByAnotherRequest);
        }
    }

    // SQL Server error numbers for a unique index violation.
    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
