using System.Net;
using Microsoft.Extensions.Configuration;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

public class TrackerClientTests
{
    private const string Url = "https://tracker.test/conversions";

    private readonly FakeTrackerHandler _tracker = new();

    private TrackerClient Client(string? apiKey = "test-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tracker:ApiKey"] = apiKey })
            .Build();
        return new TrackerClient(new HttpClient(_tracker), config);
    }

    [Fact]
    public async Task Sends_the_api_key_as_bearer_token_and_the_json_body()
    {
        _tracker.Reply(HttpStatusCode.Created, """{"ok":true}""");

        await Client().Send(HttpMethod.Post, Url, """{"event_id":"conv_42"}""");

        var request = Assert.Single(_tracker.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Url, request.Url);
        Assert.Equal("Bearer test-key", request.Authorization);
        Assert.Equal("""{"event_id":"conv_42"}""", request.Body);
    }

    [Fact]
    public async Task Without_a_key_sends_no_authorization_header()
    {
        _tracker.Reply(HttpStatusCode.Unauthorized, """{"ok":false}""");

        var response = await Client(apiKey: null).Send(HttpMethod.Get, Url);

        Assert.Null(Assert.Single(_tracker.Requests).Authorization);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Returns_status_code_and_body()
    {
        _tracker.Reply(HttpStatusCode.UnprocessableEntity, """{"ok":false,"error":"validation_error"}""");

        var response = await Client().Send(HttpMethod.Post, Url, "{}");

        Assert.Equal(422, response.StatusCode);
        Assert.Equal("""{"ok":false,"error":"validation_error"}""", response.Body);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task Network_error_is_returned_not_thrown()
    {
        _tracker.Throw(new HttpRequestException("Connection refused"));

        var response = await Client().Send(HttpMethod.Post, Url, "{}");

        Assert.Null(response.StatusCode);
        Assert.Contains("Connection refused", response.Error);
    }

    [Fact]
    public async Task Timeout_is_returned_not_thrown()
    {
        _tracker.Throw(new TaskCanceledException());

        var response = await Client().Send(HttpMethod.Post, Url, "{}");

        Assert.Null(response.StatusCode);
        Assert.Contains("No answer within", response.Error);
    }
}
