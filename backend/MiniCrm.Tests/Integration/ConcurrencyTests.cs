using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Dtos;

namespace MiniCrm.Tests.Integration;

// Requests that change the same lead at the same time. The database (unique index
// on the event, row version on the lead) decides who wins; the loser gets a 409.
public class ConcurrencyTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const int ParallelRequests = 10;

    [Fact]
    public async Task Parallel_converts_create_exactly_one_event()
    {
        var lead = await CreateLead();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, ParallelRequests).Select(_ => ChangeStatus(lead.Id, "converted")));

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal($"conv_{lead.Id}", Assert.Single(await GetEvents(lead.Id)).EventId);
    }

    [Fact]
    public async Task Parallel_edits_never_fail_with_a_server_error()
    {
        var lead = await CreateLead();

        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelRequests).Select(i =>
            Client.PutAsJsonAsync($"/api/leads/{lead.Id}", new { name = $"Ann {i}", email = "lead@example.com" }, Json)));

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
    }

    // Settings edits are "last save wins". Each save writes all three templates in one
    // UPDATE, so the row always holds one request's templates, never a mix of two.
    [Fact]
    public async Task Parallel_endpoint_edits_leave_one_requests_templates()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelRequests).Select(i =>
            Client.PutAsJsonAsync("/api/tracker/endpoints/2", new
            {
                url_template = $"https://tracker.test/conversions/{i}",
                body_template = $$"""{"event_id":"{event_id}","n":{{i}}}""",
                event_id_template = $"conv{i}_{{lead_id}}"
            }, Json)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var endpoints = await Client.GetFromJsonAsync<List<TrackerEndpointResponse>>("/api/tracker/endpoints", Json);
        var purchase = endpoints!.Single(e => e.Id == 2);
        var winner = purchase.UrlTemplate.Split('/').Last();
        Assert.Equal($$"""{"event_id":"{event_id}","n":{{winner}}}""", purchase.BodyTemplate);
        Assert.Equal($"conv{winner}_{{lead_id}}", purchase.EventIdTemplate);
    }

    // Same for the shared auth row: the header name and value always come from one save.
    [Fact]
    public async Task Parallel_auth_edits_leave_one_requests_header()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelRequests).Select(i =>
            Client.PutAsJsonAsync("/api/tracker/auth", new
            {
                header_name = $"X-Key-{i}",
                header_template = $"Key{i} {{api_key}}",
                api_key = $"key-{i}"
            }, Json)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var auth = await Client.GetFromJsonAsync<TrackerAuthResponse>("/api/tracker/auth", Json);
        var winner = auth!.HeaderName.Split('-').Last();
        Assert.Equal($"Key{winner} {{api_key}}", auth.HeaderTemplate);
        await WithDb(async db =>
            Assert.Equal($"key-{winner}", (await db.TrackerAuthSettings.SingleAsync()).ApiKey));
    }

    [Fact]
    public async Task Convert_and_lose_at_the_same_time_leave_a_consistent_lead()
    {
        var lead = await CreateLead();

        var responses = await Task.WhenAll(ChangeStatus(lead.Id, "converted"), ChangeStatus(lead.Id, "lost"));

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var events = await GetEvents(lead.Id);
        var status = (await Client.GetFromJsonAsync<LeadResponse>($"/api/leads/{lead.Id}", Json))!.Status;

        // Either it ended converted with its event, or lost with no event. Never a mix.
        if (status == "converted")
            Assert.Single(events);
        else
            Assert.Empty(events);
    }

    // Soft delete is an update, so the row version picks one winner, like any other edit.
    [Fact]
    public async Task Delete_and_convert_at_the_same_time_never_fail_with_a_server_error()
    {
        var lead = await CreateLead();

        var responses = await Task.WhenAll(
            Client.DeleteAsync($"/api/leads/{lead.Id}"), ChangeStatus(lead.Id, "converted"));

        Assert.All(responses, r => Assert.Contains(r.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.Conflict, HttpStatusCode.NotFound }));
        // The row version lets at least one of them through, never neither.
        Assert.Contains(responses, r => r.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Parallel_deletes_succeed_once()
    {
        var lead = await CreateLead();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, ParallelRequests).Select(_ => Client.DeleteAsync($"/api/leads/{lead.Id}")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.All(responses, r => Assert.Contains(r.StatusCode,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict, HttpStatusCode.NotFound }));
    }
}
