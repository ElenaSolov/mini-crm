using System.Net;
using System.Net.Http.Json;
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
}
