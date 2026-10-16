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

    // Text filters use Contains, which EF turns into a SQL parameter, so the value can't
    // inject SQL, and "%" or "_" are searched as plain characters, not as LIKE wildcards.
    // SQL Server ignores case, so "ANN" also finds "Ann".
    public async Task<LeadPage> GetAll(LeadListQuery filter)
    {
        var query = db.Leads.AsNoTracking();

        if (filter.Id is not null)
            query = query.Where(l => l.Id == filter.Id);

        if (Clean(filter.Name) is { } name)
            query = query.Where(l => l.Name.Contains(name));
        if (Clean(filter.Email) is { } email)
            query = query.Where(l => l.Email != null && l.Email.Contains(email));
        if (Clean(filter.Phone) is { } phone)
            query = query.Where(l => l.Phone != null && l.Phone.Contains(phone));
        // Sources are saved in lower case and currencies in upper case, so compare the same way.
        if (Clean(filter.Source)?.ToLowerInvariant() is { } source)
            query = query.Where(l => l.Source != null && l.Source.Contains(source));
        if (CurrencyCodes(filter.Currency) is { Count: > 0 } currencies)
            query = query.Where(l => l.Currency != null && currencies.Contains(l.Currency));

        if (Clean(filter.Status) is { } status)
            query = query.Where(l => l.Status == status);

        if (filter.AmountFrom is not null)
            query = query.Where(l => l.Amount >= filter.AmountFrom);
        if (filter.AmountTo is not null)
            query = query.Where(l => l.Amount <= filter.AmountTo);

        if (filter.CreatedFrom is not null)
            query = query.Where(l => l.CreatedAt >= filter.CreatedFrom);
        if (filter.CreatedTo is not null)
            query = query.Where(l => l.CreatedAt <= filter.CreatedTo);

        var totalCount = await query.CountAsync();

        // Id as a tie-breaker, so leads created at the same moment keep the same order
        // on every page and none is shown twice or skipped.
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var outboundStatuses = await GetOutboundStatuses(items.Select(l => l.Id).ToList());
        return new LeadPage(items, outboundStatuses, totalCount);
    }

    public async Task<Lead?> GetById(int id) => await db.Leads.FindAsync(id);

    // Null when the lead has no tracker event yet.
    public async Task<TrackerEventStatus?> GetOutboundStatus(int leadId) =>
        (await GetOutboundStatuses([leadId])).TryGetValue(leadId, out var status) ? status : null;

    // The status of each lead's newest event, in one query for the whole page.
    // A lead has one event per endpoint; with the default setup that is just "purchase".
    private async Task<Dictionary<int, TrackerEventStatus>> GetOutboundStatuses(List<int> leadIds) =>
        await db.TrackerEvents
            .Where(e => leadIds.Contains(e.LeadId))
            .GroupBy(e => e.LeadId)
            .Select(g => new { LeadId = g.Key, Status = g.OrderByDescending(e => e.Id).Select(e => e.Status).First() })
            .ToDictionaryAsync(x => x.LeadId, x => x.Status);

    // The status names from the LeadStatuses table, in dropdown order.
    public async Task<List<string>> GetStatuses() =>
        await db.LeadStatuses.OrderBy(s => s.SortOrder).Select(s => s.Name).ToListAsync();

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

    public async Task<LeadResult> ChangeStatus(int id, string newStatus, bool simulateServerError = false)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null)
            return LeadResult.Missing();

        // Use the name exactly as the table has it. SQL Server ignores case here, so
        // "Converted" finds "converted", and the lead never gets a differently cased copy.
        // Checked here for a clear 400; the foreign key would also reject it, but as a database error.
        var status = await db.LeadStatuses
            .Where(s => s.Name == newStatus)
            .Select(s => s.Name)
            .SingleOrDefaultAsync();
        if (status is null)
            return LeadResult.Invalid("status", $"Unknown status '{newStatus}'.");

        if (!lead.TryChangeStatus(status, out var error))
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

    // Trimmed, or null when there is nothing to filter by.
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // "usd, EUR," -> ["USD", "EUR"]. EF sends the list as a parameter, so it can't inject SQL.
    private static List<string> CurrencyCodes(string? value) =>
        (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .Distinct()
            .ToList();

    // SQL Server error numbers for a unique index violation.
    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
