using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

// The "Logs" list, with EF InMemory.
public class TrackerEventServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestData.InMemoryDb();
    private readonly TrackerEventService _service;

    public TrackerEventServiceTests()
    {
        _service = new TrackerEventService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<TrackerEvent> ConvertedLeadWithEvent(string name, bool failed = false)
    {
        var lead = TestData.ConvertibleLead(id: 0);
        lead.Name = name;
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        var trackerEvent = new TrackerEvent { EventId = $"conv_{lead.Id}", LeadId = lead.Id, TrackerEndpointId = 2 };
        trackerEvent.Queue(DateTimeOffset.UtcNow);
        if (failed)
            trackerEvent.MarkFailed("Tracker answered 422.");
        _db.TrackerEvents.Add(trackerEvent);
        await _db.SaveChangesAsync();
        return trackerEvent;
    }

    [Fact]
    public async Task Lists_newest_first_with_the_lead_names()
    {
        var ann = await ConvertedLeadWithEvent("Ann");
        var bob = await ConvertedLeadWithEvent("Bob");

        var page = await _service.GetAll(new TrackerEventListQuery());

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([bob.Id, ann.Id], page.Items.Select(e => e.Id));
        Assert.Equal("Ann", page.LeadNames[ann.LeadId]);
        Assert.Equal("Bob", page.LeadNames[bob.LeadId]);
    }

    [Fact]
    public async Task Keeps_the_name_of_a_deleted_lead()
    {
        var ann = await ConvertedLeadWithEvent("Ann");
        (await _db.Leads.SingleAsync()).DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var page = await _service.GetAll(new TrackerEventListQuery());

        Assert.Equal("Ann", page.LeadNames[ann.LeadId]);
    }

    [Fact]
    public async Task Filters_by_status_and_counts_only_the_matches()
    {
        await ConvertedLeadWithEvent("Ann");
        var failed = await ConvertedLeadWithEvent("Bob", failed: true);

        var page = await _service.GetAll(new TrackerEventListQuery { Status = TrackerEventStatus.Failed });

        Assert.Equal(1, page.TotalCount);
        Assert.Equal([failed.Id], page.Items.Select(e => e.Id));
    }

    [Fact]
    public async Task Returns_the_requested_page()
    {
        var first = await ConvertedLeadWithEvent("A");
        await ConvertedLeadWithEvent("B");
        await ConvertedLeadWithEvent("C");

        var page = await _service.GetAll(new TrackerEventListQuery { Page = 2, PageSize = 2 });

        Assert.Equal(3, page.TotalCount);
        Assert.Equal([first.Id], page.Items.Select(e => e.Id));
    }
}
