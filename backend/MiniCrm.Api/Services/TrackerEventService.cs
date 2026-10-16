using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// The "Logs" page: every outbound event of every lead, with all its attempts.
public class TrackerEventService(AppDbContext db)
{
    // Newest event first. Attempts oldest first, so each event reads like a log.
    public async Task<TrackerEventPage> GetAll(TrackerEventListQuery filter)
    {
        var query = db.TrackerEvents.AsNoTracking();
        if (filter.Status is not null)
            query = query.Where(e => e.Status == filter.Status);

        var totalCount = await query.CountAsync();

        var events = await query
            .Include(e => e.TrackerEndpoint)
            .Include(e => e.Attempts.OrderBy(a => a.Id))
            .OrderByDescending(e => e.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // A second small query, so the page can show who each event is for.
        var leadIds = events.Select(e => e.LeadId).Distinct().ToList();
        var leadNames = await db.Leads
            .Where(l => leadIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name);

        return new TrackerEventPage(events, leadNames, totalCount);
    }
}

public record TrackerEventPage(List<TrackerEvent> Items, Dictionary<int, string> LeadNames, int TotalCount);
