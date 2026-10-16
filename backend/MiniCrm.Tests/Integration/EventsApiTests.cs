using System.Net;
using System.Net.Http.Json;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Tests.Integration;

// The outbound status on each lead (table and detail) and the "Logs" page (GET /api/events).
public class EventsApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<LeadResponse> GetLead(int id) =>
        (await Client.GetFromJsonAsync<LeadResponse>($"/api/leads/{id}", Json))!;

    private async Task<LeadResponse> LeadInList(int id) =>
        (await Client.GetFromJsonAsync<LeadPageResponse>("/api/leads", Json))!.Items.Single(l => l.Id == id);

    private async Task<TrackerEventPageResponse> ListEvents(string query = "") =>
        (await Client.GetFromJsonAsync<TrackerEventPageResponse>($"/api/events?{query}", Json))!;

    [Fact]
    public async Task Outbound_status_follows_the_event_from_none_to_pending_to_sent()
    {
        var lead = await CreateLead();
        Assert.Null(lead.OutboundStatus);
        Assert.Null((await LeadInList(lead.Id)).OutboundStatus);

        var converted = await (await ChangeStatus(lead.Id, "converted")).Content.ReadFromJsonAsync<LeadResponse>(Json);
        Assert.Equal(TrackerEventStatus.Pending, converted!.OutboundStatus);

        Factory.Tracker.Reply(HttpStatusCode.Created, """{"ok":true,"duplicate":false}""");
        await SendDueEvents();

        Assert.Equal(TrackerEventStatus.Sent, (await GetLead(lead.Id)).OutboundStatus);
        Assert.Equal(TrackerEventStatus.Sent, (await LeadInList(lead.Id)).OutboundStatus);
    }

    [Fact]
    public async Task Outbound_status_shows_a_failed_send()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.UnprocessableEntity, """{"ok":false,"error":"validation_error"}""");

        await SendDueEvents();

        Assert.Equal(TrackerEventStatus.Failed, (await LeadInList(lead.Id)).OutboundStatus);
    }

    [Fact]
    public async Task Logs_list_every_event_newest_first_with_the_lead_name_and_attempts()
    {
        var ann = await CreateLead(ValidLead("Ann"));
        var bob = await CreateLead(ValidLead("Bob"));
        await ChangeStatus(ann.Id, "converted");
        await ChangeStatus(bob.Id, "converted");
        // One answer per request: both events are sent in the same run.
        Factory.Tracker.Reply(HttpStatusCode.Created, """{"ok":true,"duplicate":false}""");
        Factory.Tracker.Reply(HttpStatusCode.Created, """{"ok":true,"duplicate":false}""");
        await SendDueEvents();

        var page = await ListEvents();

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(["Bob", "Ann"], page.Items.Select(i => i.LeadName));
        Assert.Equal(bob.Id, page.Items[0].Event.LeadId);
        Assert.All(page.Items, i => Assert.Single(i.Event.Attempts));
    }

    [Fact]
    public async Task Logs_filter_by_status_and_page()
    {
        var sent = await CreateLead(ValidLead("Sent"));
        await ChangeStatus(sent.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.Created, """{"ok":true,"duplicate":false}""");
        await SendDueEvents();
        var pending = await CreateLead(ValidLead("Pending"));
        await ChangeStatus(pending.Id, "converted");

        Assert.Equal(["Sent"], (await ListEvents("status=sent")).Items.Select(i => i.LeadName));
        Assert.Equal(["Pending"], (await ListEvents("status=pending")).Items.Select(i => i.LeadName));
        Assert.Empty((await ListEvents("status=failed")).Items);

        var secondPage = await ListEvents("page=2&page_size=1");
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal(["Sent"], secondPage.Items.Select(i => i.LeadName));
    }

    [Theory]
    [InlineData("status=done", "status")]
    [InlineData("page=0", "page")]
    [InlineData("page_size=101", "page_size")]
    public async Task Bad_logs_query_is_400(string query, string field)
    {
        var response = await Client.GetAsync($"/api/events?{query}");

        Assert.Contains(field, await ErrorFields(response));
    }
}
