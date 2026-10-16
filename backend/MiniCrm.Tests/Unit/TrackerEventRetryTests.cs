using MiniCrm.Api.Models;

namespace MiniCrm.Tests.Unit;

// The retry rules in TrackerEvent.RecordAttempt: what happens after one send.
public class TrackerEventRetryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);

    private static TrackerEvent PendingEvent()
    {
        var trackerEvent = new TrackerEvent { EventId = "conv_42" };
        trackerEvent.Queue(Now);
        return trackerEvent;
    }

    private static TrackerAttempt Answer(int? statusCode, DateTimeOffset? at = null) =>
        new() { StatusCode = statusCode, AttemptedAt = at ?? Now, Error = statusCode is null ? "Network error" : null };

    [Theory]
    [InlineData(201)] // created
    [InlineData(200)] // duplicate: true
    public void Success_answer_marks_event_sent(int statusCode)
    {
        var trackerEvent = PendingEvent();

        trackerEvent.RecordAttempt(Answer(statusCode));

        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        Assert.Null(trackerEvent.NextAttemptAt);
        Assert.Null(trackerEvent.LastError);
        Assert.Single(trackerEvent.Attempts);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(429)]
    [InlineData(null)] // timeout or network error
    public void Temporary_failure_keeps_event_pending_for_a_retry(int? statusCode)
    {
        var trackerEvent = PendingEvent();

        trackerEvent.RecordAttempt(Answer(statusCode));

        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);
        Assert.Equal(Now + TrackerEvent.FirstRetryDelay, trackerEvent.NextAttemptAt);
        Assert.NotNull(trackerEvent.LastError);
    }

    [Theory]
    [InlineData(422)] // validation error
    [InlineData(401)] // wrong key
    [InlineData(400)]
    public void Client_error_marks_event_failed_without_retry(int statusCode)
    {
        var trackerEvent = PendingEvent();

        trackerEvent.RecordAttempt(Answer(statusCode));

        Assert.Equal(TrackerEventStatus.Failed, trackerEvent.Status);
        Assert.Null(trackerEvent.NextAttemptAt);
        Assert.Equal($"Tracker answered {statusCode}.", trackerEvent.LastError);
    }

    [Fact]
    public void Retry_delay_doubles_after_each_failure()
    {
        var trackerEvent = PendingEvent();

        trackerEvent.RecordAttempt(Answer(500, Now));
        Assert.Equal(Now.AddSeconds(10), trackerEvent.NextAttemptAt);

        trackerEvent.RecordAttempt(Answer(500, Now));
        Assert.Equal(Now.AddSeconds(20), trackerEvent.NextAttemptAt);

        trackerEvent.RecordAttempt(Answer(500, Now));
        Assert.Equal(Now.AddSeconds(40), trackerEvent.NextAttemptAt);
    }

    [Fact]
    public void Event_fails_after_max_attempts()
    {
        var trackerEvent = PendingEvent();

        for (var i = 0; i < TrackerEvent.MaxAttempts - 1; i++)
            trackerEvent.RecordAttempt(Answer(500));
        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);

        trackerEvent.RecordAttempt(Answer(500));

        Assert.Equal(TrackerEventStatus.Failed, trackerEvent.Status);
        Assert.Equal(TrackerEvent.MaxAttempts, trackerEvent.AttemptCount);
        Assert.Null(trackerEvent.NextAttemptAt);
    }

    [Fact]
    public void Server_error_then_success_ends_sent()
    {
        var trackerEvent = PendingEvent();

        trackerEvent.RecordAttempt(Answer(500));
        trackerEvent.RecordAttempt(Answer(201));

        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
        Assert.Equal([500, 201], trackerEvent.Attempts.Select(a => a.StatusCode));
    }

    [Fact]
    public void Simulate_flag_is_cleared_after_the_first_attempt()
    {
        var trackerEvent = PendingEvent();
        trackerEvent.SimulateServerError = true;

        trackerEvent.RecordAttempt(Answer(500));

        Assert.False(trackerEvent.SimulateServerError);
    }

    [Theory]
    [InlineData(201)] // sent
    [InlineData(422)] // failed
    public void Requeue_sends_a_finished_event_again_from_zero_attempts(int lastStatusCode)
    {
        var trackerEvent = PendingEvent();
        trackerEvent.RecordAttempt(Answer(lastStatusCode));
        var later = Now.AddMinutes(5);

        trackerEvent.Requeue(simulateServerError: true, later);

        Assert.Equal(TrackerEventStatus.Pending, trackerEvent.Status);
        Assert.Equal(0, trackerEvent.AttemptCount);
        Assert.Equal(later, trackerEvent.NextAttemptAt);
        Assert.Null(trackerEvent.LastError);
        Assert.True(trackerEvent.SimulateServerError);
        Assert.Single(trackerEvent.Attempts); // the history stays
    }

    [Fact]
    public void Requeue_leaves_a_pending_event_alone()
    {
        var trackerEvent = PendingEvent();
        trackerEvent.RecordAttempt(Answer(500));
        var nextAttemptAt = trackerEvent.NextAttemptAt;

        trackerEvent.Requeue(simulateServerError: true, Now.AddMinutes(5));

        Assert.Equal(1, trackerEvent.AttemptCount);
        Assert.Equal(nextAttemptAt, trackerEvent.NextAttemptAt);
        Assert.False(trackerEvent.SimulateServerError);
    }
}
