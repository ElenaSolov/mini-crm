using System.Net;
using System.Net.Http.Json;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Integration;

// The "Tracker auth" card: one auth header and API key for ping and purchase.
public class TrackerAuthApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task<HttpResponseMessage> SaveAuth(string? apiKey = null, string headerName = TestData.AuthHeaderName,
        string headerTemplate = TestData.AuthHeaderTemplate) =>
        Client.PutAsJsonAsync("/api/tracker/auth",
            new { header_name = headerName, header_template = headerTemplate, api_key = apiKey }, Json);

    private async Task<SentRequest> Ping()
    {
        Factory.Tracker.Reply(HttpStatusCode.OK, """{"ok":true}""");
        await Client.GetAsync("/api/tracker/ping");
        return Factory.Tracker.Requests[^1];
    }

    [Fact]
    public async Task Starts_with_the_env_header_and_the_env_key()
    {
        var auth = await Client.GetFromJsonAsync<TrackerAuthResponse>("/api/tracker/auth", Json);

        Assert.Equal(TestData.AuthHeaderName, auth!.HeaderName);
        Assert.Equal(TestData.AuthHeaderTemplate, auth.HeaderTemplate);
        Assert.Equal(ApiKeySource.Env, auth.KeySource);
    }

    [Fact]
    public async Task Saved_key_is_used_by_ping_and_never_sent_back()
    {
        var response = await SaveAuth(apiKey: "settings-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("settings-key", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("settings-key", await Client.GetStringAsync("/api/tracker/auth"));
        Assert.Equal("Bearer settings-key", (await Ping()).Authorization);
    }

    [Fact]
    public async Task Clearing_the_key_goes_back_to_the_env_key()
    {
        await SaveAuth(apiKey: "settings-key");

        var response = await Client.DeleteAsync("/api/tracker/auth/key");

        var auth = await response.Content.ReadFromJsonAsync<TrackerAuthResponse>(Json);
        Assert.Equal(ApiKeySource.Env, auth!.KeySource);
        Assert.Equal($"Bearer {ApiFactory.TestApiKey}", (await Ping()).Authorization);
    }

    [Fact]
    public async Task Invalid_values_are_400_per_field()
    {
        var response = await SaveAuth(apiKey: "has space", headerName: "Bad Name", headerTemplate: "Bearer real-key");

        Assert.Equal(["api_key", "header_name", "header_template"], (await ErrorFields(response)).Order());
    }

    // One header for both: the purchase event uses the header saved for ping too. It is read
    // when the event is sent, so a header saved after the event was queued is the one used.
    [Fact]
    public async Task Queued_purchase_event_is_sent_with_the_header_saved_after_it_was_queued()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, LeadStatusNames.Converted);

        await SaveAuth(headerName: "X-Api-Key", headerTemplate: "Key {api_key}");
        Factory.Tracker.Reply(HttpStatusCode.Created, """{"ok":true,"duplicate":false}""");
        await SendDueEvents();

        var request = Assert.Single(Factory.Tracker.Requests);
        Assert.Equal($"Key {ApiFactory.TestApiKey}", request.Headers["X-Api-Key"]);
        Assert.Null(request.Authorization);
    }

    [Fact]
    public async Task Reset_puts_back_the_env_header()
    {
        await SaveAuth(headerName: "X-Api-Key", headerTemplate: "{api_key}");

        var response = await Client.PostAsync("/api/tracker/auth/reset", null);

        var auth = await response.Content.ReadFromJsonAsync<TrackerAuthResponse>(Json);
        Assert.Equal(TestData.AuthHeaderName, auth!.HeaderName);
        Assert.Equal(TestData.AuthHeaderTemplate, auth.HeaderTemplate);
    }
}
