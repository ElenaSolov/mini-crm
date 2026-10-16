using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

// LeadService with EF InMemory. Upper/lower case and foreign keys need SQL Server,
// so those are in the integration tests.
public class LeadServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestData.InMemoryDb();
    private readonly LeadService _service;

    public LeadServiceTests()
    {
        _service = new LeadService(_db, new TrackerEventQueue(_db));
    }

    public void Dispose() => _db.Dispose();

    private async Task<Lead> SavedLead(Action<Lead>? change = null)
    {
        var lead = TestData.ConvertibleLead(id: 0);
        change?.Invoke(lead);
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return lead;
    }

    [Fact]
    public async Task Converting_queues_one_pending_event_with_the_rendered_request()
    {
        var lead = await SavedLead();

        var result = await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        Assert.Equal(LeadOutcome.Success, result.Outcome);
        Assert.Equal(LeadStatusNames.Converted, result.Lead!.Status);

        var trackerEvent = await _db.TrackerEvents.SingleAsync();
        Assert.Equal($"conv_{lead.Id}", trackerEvent.EventId);
        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);
        Assert.Equal("POST", trackerEvent.Method);
        Assert.Equal(DefaultEndpoints.BaseUrl + "/conversions", trackerEvent.Url);
        Assert.Contains($"\"event_id\": \"conv_{lead.Id}\"", trackerEvent.Body);
        Assert.Contains("\"amount\": 199.00", trackerEvent.Body);
        Assert.NotNull(trackerEvent.NextAttemptAt);
    }

    [Fact]
    public async Task Converting_again_reuses_the_same_event()
    {
        var lead = await SavedLead();
        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);
        var trackerEvent = await _db.TrackerEvents.SingleAsync();
        trackerEvent.RecordAttempt(new TrackerAttempt { StatusCode = 201, AttemptedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        Assert.Equal(LeadOutcome.Success, result.Outcome);
        var again = await _db.TrackerEvents.SingleAsync();
        Assert.Equal(trackerEvent.Id, again.Id);
        Assert.Equal($"conv_{lead.Id}", again.EventId);
        Assert.Equal(TrackerEventStatus.Pending, again.Status);
    }

    [Fact]
    public async Task Request_is_a_snapshot_taken_when_the_lead_converts()
    {
        var lead = await SavedLead();
        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);
        var trackerEvent = await _db.TrackerEvents.SingleAsync();
        var bodyAtConversion = trackerEvent.Body;
        trackerEvent.RecordAttempt(new TrackerAttempt { StatusCode = 201, AttemptedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        await _service.Update(lead.Id, new SaveLeadRequest
        {
            Name = "Ann", Email = "changed@example.com", Amount = 500m, Currency = "EUR"
        });
        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted); // re-queues the sent event

        var again = await _db.TrackerEvents.SingleAsync();
        Assert.Equal(TrackerEventStatus.Pending, again.Status);
        Assert.Equal(bodyAtConversion, again.Body);
    }

    [Fact]
    public async Task Other_statuses_queue_no_event()
    {
        var lead = await SavedLead();

        await _service.ChangeStatus(lead.Id, "qualified");

        Assert.Empty(_db.TrackerEvents);
    }

    [Fact]
    public async Task Inactive_endpoint_queues_no_event()
    {
        var purchase = await _db.TrackerEndpoints.SingleAsync(e => e.Name == DefaultEndpoints.PurchaseName);
        purchase.IsActive = false;
        var lead = await SavedLead();

        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        Assert.Empty(_db.TrackerEvents);
    }

    [Fact]
    public async Task Body_template_that_renders_invalid_json_makes_a_failed_event()
    {
        var purchase = await _db.TrackerEndpoints.SingleAsync(e => e.Name == DefaultEndpoints.PurchaseName);
        purchase.BodyTemplate = """{"event_id":"{event_id}","source":{source}}""";
        var lead = await SavedLead(l => l.Source = null);

        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        var trackerEvent = await _db.TrackerEvents.SingleAsync();
        Assert.Equal(TrackerEventStatus.Failed, trackerEvent.Status);
        Assert.Contains("not valid JSON", trackerEvent.LastError);
    }

    [Fact]
    public async Task Unknown_status_is_invalid_and_names_the_field()
    {
        var lead = await SavedLead();

        var result = await _service.ChangeStatus(lead.Id, "won");

        Assert.Equal(LeadOutcome.Invalid, result.Outcome);
        Assert.Equal("status", result.Field);
    }

    [Fact]
    public async Task Broken_rule_is_a_conflict_and_queues_no_event()
    {
        var lead = await SavedLead(l => l.Amount = null);

        var result = await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        Assert.Equal(LeadOutcome.Conflict, result.Outcome);
        Assert.Empty(_db.TrackerEvents);
    }

    [Fact]
    public async Task Missing_lead_is_not_found()
    {
        Assert.Equal(LeadOutcome.NotFound, (await _service.ChangeStatus(999, "qualified")).Outcome);
        Assert.Equal(LeadOutcome.NotFound, (await _service.Delete(999)).Outcome);
        Assert.Null(await _service.GetEvents(999));
    }

    [Fact]
    public async Task Lead_with_events_cannot_be_deleted()
    {
        var lead = await SavedLead();
        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        var result = await _service.Delete(lead.Id);

        Assert.Equal(LeadOutcome.Conflict, result.Outcome);
        Assert.Single(_db.Leads);
    }

    [Fact]
    public async Task Lead_without_events_can_be_deleted()
    {
        var lead = await SavedLead();

        var result = await _service.Delete(lead.Id);

        Assert.Equal(LeadOutcome.Success, result.Outcome);
        Assert.Empty(_db.Leads);
    }

    [Fact]
    public async Task Converted_lead_cannot_lose_its_amount()
    {
        var lead = await SavedLead();
        await _service.ChangeStatus(lead.Id, LeadStatusNames.Converted);

        var result = await _service.Update(lead.Id, new SaveLeadRequest { Name = "Ann", Email = "lead@example.com" });

        Assert.Equal(LeadOutcome.Conflict, result.Outcome);
    }
}
