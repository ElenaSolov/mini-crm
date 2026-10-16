using System.Net;
using System.Net.Http.Json;
using MiniCrm.Api.Data;
using MiniCrm.Api.Services;

namespace MiniCrm.Tests.Integration;

// Step 1 of the task's test sequence: GET /ping with the key.
public class TrackerPingApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Ping_calls_the_tracker_with_the_key_and_returns_its_answer()
    {
        Factory.Tracker.Reply(HttpStatusCode.OK, """{"ok":true,"candidate":"test"}""");

        var response = await Client.GetFromJsonAsync<TrackerResponse>("/api/tracker/ping", Json);

        var request = Assert.Single(Factory.Tracker.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(DefaultEndpoints.BaseUrl + "/ping", request.Url);
        Assert.Equal($"Bearer {ApiFactory.TestApiKey}", request.Authorization);
        Assert.Equal(200, response!.StatusCode);
        Assert.Equal("""{"ok":true,"candidate":"test"}""", response.Body);
    }

    [Fact]
    public async Task Wrong_key_shows_as_the_trackers_401()
    {
        Factory.Tracker.Reply(HttpStatusCode.Unauthorized, """{"ok":false,"error":"unauthorized"}""");

        var response = await Client.GetAsync("/api/tracker/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(401, (await response.Content.ReadFromJsonAsync<TrackerResponse>(Json))!.StatusCode);
    }

    [Fact]
    public async Task No_answer_from_the_tracker_is_502()
    {
        Factory.Tracker.Throw(new HttpRequestException("Connection refused"));

        var response = await Client.GetAsync("/api/tracker/ping");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
