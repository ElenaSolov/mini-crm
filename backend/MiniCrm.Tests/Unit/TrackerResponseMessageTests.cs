using MiniCrm.Api.Services;

namespace MiniCrm.Tests.Unit;

// The bodies are the tracker's answers from docs/Task.md.
public class TrackerResponseMessageTests
{
    [Fact]
    public void Created_is_accepted() =>
        Assert.Equal("Accepted by the tracker.", TrackerResponseMessage.Describe(201,
            """{"ok":true,"duplicate":false,"event_id":"conv_42"}"""));

    [Fact]
    public void Duplicate_says_it_counts_as_success() =>
        Assert.Equal("Duplicate: the tracker already had this event_id (counts as success).",
            TrackerResponseMessage.Describe(200, """{"ok":true,"duplicate":true,"event_id":"conv_42"}"""));

    [Fact]
    public void Validation_error_shows_the_field_messages() =>
        Assert.Equal("Provide email or phone.", TrackerResponseMessage.Describe(422,
            """{"ok":false,"error":"validation_error","errors":{"contact":["Provide email or phone."]}}"""));

    [Fact]
    public void Unauthorized_shows_the_message() =>
        Assert.Equal("Missing or invalid API key.", TrackerResponseMessage.Describe(401,
            """{"ok":false,"error":"unauthorized","message":"Missing or invalid API key."}"""));

    [Fact]
    public void Server_error_shows_the_message() =>
        Assert.Equal("Simulated 500. Retry this event_id.", TrackerResponseMessage.Describe(500,
            """{"ok":false,"error":"server_error","message":"Simulated 500. Retry this event_id."}"""));

    [Fact]
    public void Falls_back_to_the_error_code() =>
        Assert.Equal("rate_limited", TrackerResponseMessage.Describe(429, """{"ok":false,"error":"rate_limited"}"""));

    [Theory]
    [InlineData(null, null)]                    // no answer (timeout / network error)
    [InlineData(502, "")]                       // empty body
    [InlineData(502, "<html>Bad Gateway</html>")] // not JSON, e.g. a proxy page
    [InlineData(500, "[1,2]")]                  // JSON, but not an object
    [InlineData(500, """{"ok":false}""")]       // nothing to show
    public void Returns_null_when_there_is_nothing_to_show(int? statusCode, string? body) =>
        Assert.Null(TrackerResponseMessage.Describe(statusCode, body));
}
