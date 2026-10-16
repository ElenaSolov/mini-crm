using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

// The worker's send step with EF InMemory and a fake tracker.
public class TrackerEventSenderTests : IDisposable
{
    private const string Created = """{"ok":true,"duplicate":false,"event_id":"conv_1"}""";

    private readonly AppDbContext _db = TestData.InMemoryDb();
    private readonly FakeTrackerHandler _tracker = new();
    private readonly TrackerEventSender _sender;

    public TrackerEventSenderTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tracker:ApiKey"] = "test-key" })
            .Build();
        var client = new TrackerClient(new HttpClient(_tracker), config);
        _sender = new TrackerEventSender(_db, client, NullLogger<TrackerEventSender>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task<TrackerEvent> QueuedEvent(string eventId = "conv_1", bool simulateServerError = false,
        DateTimeOffset? dueAt = null)
    {
        var trackerEvent = new TrackerEvent
        {
            EventId = eventId,
            TrackerEndpointId = 2,
            Url = "https://tracker.test/conversions",
            Body = $$"""{"event_id":"{{eventId}}","phone":"+12025550123"}""",
            SimulateServerError = simulateServerError
        };
        trackerEvent.Queue(dueAt ?? DateTimeOffset.UtcNow.AddSeconds(-1));
        _db.TrackerEvents.Add(trackerEvent);
        await _db.SaveChangesAsync();
        return trackerEvent;
    }

    // Skips the backoff wait, so the next send happens now.
    private async Task MakeDue(TrackerEvent trackerEvent)
    {
        _db.Entry(trackerEvent).Property(e => e.NextAttemptAt).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Sends_due_event_and_stores_the_attempt()
    {
        var trackerEvent = await QueuedEvent();
        _tracker.Reply(HttpStatusCode.Created, Created);

        await _sender.SendDueEvents(CancellationToken.None);

        var request = Assert.Single(_tracker.Requests);
        Assert.Equal(trackerEvent.Body, request.Body);
        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);

        var attempt = Assert.Single(await _db.TrackerAttempts.ToListAsync());
        Assert.Equal(201, attempt.StatusCode);
        Assert.Equal(Created, attempt.ResponseBody);
        Assert.Equal(trackerEvent.Body, attempt.RequestBody);
        Assert.Equal(trackerEvent.Url, attempt.Url);
    }

    [Fact]
    public async Task Duplicate_answer_counts_as_sent()
    {
        var trackerEvent = await QueuedEvent();
        _tracker.Reply(HttpStatusCode.OK, """{"ok":true,"duplicate":true,"event_id":"conv_1"}""");

        await _sender.SendDueEvents(CancellationToken.None);

        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
    }

    [Fact]
    public async Task Simulate_is_added_to_the_first_attempt_only()
    {
        var trackerEvent = await QueuedEvent(simulateServerError: true);
        var storedBody = trackerEvent.Body;
        _tracker.Reply(HttpStatusCode.InternalServerError, """{"ok":false,"error":"server_error"}""");
        _tracker.Reply(HttpStatusCode.Created, Created);

        await _sender.SendDueEvents(CancellationToken.None);
        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);
        await MakeDue(trackerEvent);
        await _sender.SendDueEvents(CancellationToken.None);

        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        Assert.Equal(storedBody, trackerEvent.Body);

        var first = JsonDocument.Parse(_tracker.Requests[0].Body!).RootElement;
        var second = JsonDocument.Parse(_tracker.Requests[1].Body!).RootElement;
        Assert.Equal("server_error", first.GetProperty("simulate").GetString());
        Assert.Equal("+12025550123", first.GetProperty("phone").GetString());
        Assert.False(second.TryGetProperty("simulate", out _));
        Assert.Equal(first.GetProperty("event_id").GetString(), second.GetProperty("event_id").GetString());
    }

    [Fact]
    public async Task Event_waiting_for_its_retry_time_is_not_sent()
    {
        await QueuedEvent(dueAt: DateTimeOffset.UtcNow.AddMinutes(1));

        await _sender.SendDueEvents(CancellationToken.None);

        Assert.Empty(_tracker.Requests);
    }

    [Fact]
    public async Task Sent_and_failed_events_are_not_sent_again()
    {
        var sent = await QueuedEvent("conv_1");
        sent.RecordAttempt(new TrackerAttempt { StatusCode = 201 });
        var failed = await QueuedEvent("conv_2");
        failed.MarkFailed("Broken template.");
        await _db.SaveChangesAsync();

        await _sender.SendDueEvents(CancellationToken.None);

        Assert.Empty(_tracker.Requests);
    }

    [Fact]
    public async Task Sends_at_most_two_events_per_run_to_respect_the_rate_limit()
    {
        await QueuedEvent("conv_1");
        await QueuedEvent("conv_2");
        await QueuedEvent("conv_3");
        _tracker.Reply(HttpStatusCode.Created, Created);
        _tracker.Reply(HttpStatusCode.Created, Created);

        await _sender.SendDueEvents(CancellationToken.None);

        Assert.Equal(2, _tracker.Requests.Count);
        Assert.Equal(1, await _db.TrackerEvents.CountAsync(e => e.Status == TrackerEventStatus.Pending));
    }

    [Fact]
    public async Task Network_error_is_stored_and_retried_later()
    {
        var trackerEvent = await QueuedEvent();
        _tracker.Throw(new HttpRequestException("Connection refused"));

        await _sender.SendDueEvents(CancellationToken.None);

        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);
        var attempt = Assert.Single(trackerEvent.Attempts);
        Assert.Null(attempt.StatusCode);
        Assert.Contains("Connection refused", attempt.Error);
    }
}
