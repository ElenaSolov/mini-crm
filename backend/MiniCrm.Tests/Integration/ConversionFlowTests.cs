using System.Net;
using System.Text.Json;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;

namespace MiniCrm.Tests.Integration;

// The task's test sequence (steps 2-5), end to end: HTTP -> database -> worker -> fake tracker.
public class ConversionFlowTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Created = """{"ok":true,"duplicate":false,"event_id":"conv_1"}""";
    private const string Duplicate = """{"ok":true,"duplicate":true,"event_id":"conv_1"}""";
    private const string ServerError = """{"ok":false,"error":"server_error","message":"Simulated 500. Retry this event_id."}""";
    private const string ValidationError = """{"ok":false,"error":"validation_error","errors":{"contact":["Provide email or phone."]}}""";

    [Fact]
    public async Task Convert_stores_a_pending_event_and_does_not_call_the_tracker_yet()
    {
        var lead = await CreateLead();

        var response = await ChangeStatus(lead.Id, "converted");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Factory.Tracker.Requests);
        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal($"conv_{lead.Id}", trackerEvent.EventId);
        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);
        Assert.Equal(DefaultEndpoints.PurchaseName, trackerEvent.Endpoint);
    }

    // Step 2: create a lead, convert it -> 201.
    [Fact]
    public async Task Convert_sends_the_event_with_the_key_and_stores_request_and_response()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.Created, Created);

        await SendDueEvents();

        var request = Assert.Single(Factory.Tracker.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(DefaultEndpoints.BaseUrl + "/conversions", request.Url);
        Assert.Equal($"Bearer {ApiFactory.TestApiKey}", request.Authorization);

        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal($"conv_{lead.Id}", body.GetProperty("event_id").GetString());
        Assert.Equal("lead@example.com", body.GetProperty("email").GetString());
        Assert.Equal(199.00m, body.GetProperty("amount").GetDecimal());
        Assert.False(body.TryGetProperty("simulate", out _));

        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        var attempt = Assert.Single(trackerEvent.Attempts);
        Assert.Equal(201, attempt.StatusCode);
        Assert.Equal(Created, attempt.ResponseBody);
        Assert.Equal(request.Body, attempt.RequestBody);
        Assert.Equal(request.Url, attempt.Url);
    }

    // Step 3: convert the same lead again -> 200 + duplicate:true, same event_id.
    [Fact]
    public async Task Convert_again_resends_the_same_event_and_duplicate_counts_as_success()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.Created, Created);
        await SendDueEvents();

        Assert.Equal(HttpStatusCode.OK, (await ChangeStatus(lead.Id, "converted")).StatusCode);
        Factory.Tracker.Reply(HttpStatusCode.OK, Duplicate);
        await SendDueEvents();

        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        Assert.Equal([201, 200], trackerEvent.Attempts.Select(a => a.StatusCode));

        var sentEventIds = Factory.Tracker.Requests
            .Select(r => JsonDocument.Parse(r.Body!).RootElement.GetProperty("event_id").GetString());
        Assert.Equal([$"conv_{lead.Id}", $"conv_{lead.Id}"], sentEventIds);
    }

    // Step 4: the tracker answers 422 -> failed, no retries.
    [Fact]
    public async Task Validation_error_from_the_tracker_marks_the_event_failed()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.UnprocessableEntity, ValidationError);

        await SendDueEvents();
        await MakeRetriesDue();
        await SendDueEvents();

        Assert.Single(Factory.Tracker.Requests);
        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal(TrackerEventStatus.Failed, trackerEvent.Status);
        Assert.Equal("Tracker answered 422.", trackerEvent.LastError);
        Assert.Equal(ValidationError, Assert.Single(trackerEvent.Attempts).ResponseBody);
    }

    [Fact]
    public async Task Convert_again_retries_a_failed_event()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.UnprocessableEntity, ValidationError);
        await SendDueEvents();

        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.Created, Created);
        await SendDueEvents();

        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        Assert.Equal([422, 201], trackerEvent.Attempts.Select(a => a.StatusCode));
    }

    // Step 5: simulate a 500, then the retry of the same event_id -> 201.
    [Fact]
    public async Task Server_error_is_retried_with_the_same_event_id()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted", simulateServerError: true);
        Factory.Tracker.Reply(HttpStatusCode.InternalServerError, ServerError);
        Factory.Tracker.Reply(HttpStatusCode.Created, Created);

        await SendDueEvents();

        var afterFirst = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal(TrackerEventStatus.Pending, afterFirst.Status);
        Assert.True(afterFirst.NextAttemptAt > DateTimeOffset.UtcNow); // waits before retrying

        await SendDueEvents(); // not due yet: nothing is sent
        Assert.Single(Factory.Tracker.Requests);

        await MakeRetriesDue();
        await SendDueEvents();

        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        Assert.Equal([500, 201], trackerEvent.Attempts.Select(a => a.StatusCode));

        var first = JsonDocument.Parse(Factory.Tracker.Requests[0].Body!).RootElement;
        var second = JsonDocument.Parse(Factory.Tracker.Requests[1].Body!).RootElement;
        Assert.Equal("server_error", first.GetProperty("simulate").GetString());
        Assert.False(second.TryGetProperty("simulate", out _));
        Assert.Equal($"conv_{lead.Id}", first.GetProperty("event_id").GetString());
        Assert.Equal($"conv_{lead.Id}", second.GetProperty("event_id").GetString());
    }

    [Fact]
    public async Task Event_log_never_contains_the_api_key()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");
        Factory.Tracker.Reply(HttpStatusCode.Created, Created);
        await SendDueEvents();

        var eventLog = await Client.GetStringAsync($"/api/leads/{lead.Id}/events");

        Assert.Contains("conv_", eventLog);
        Assert.DoesNotContain(ApiFactory.TestApiKey, eventLog);
    }
}
