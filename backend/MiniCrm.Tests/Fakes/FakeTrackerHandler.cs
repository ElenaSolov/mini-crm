using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace MiniCrm.Tests.Fakes;

// What the app sent to the fake tracker.
public record SentRequest(HttpMethod Method, string Url, string? Authorization, string? Body);

// Stands in for the real tracker, so tests never call it and never need the API key.
// Each test queues the answers it wants; a request with no answer queued fails the test.
// Concurrent queues, because the integration tests share one handler across requests.
public class FakeTrackerHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpResponseMessage>> _answers = new();
    private readonly ConcurrentQueue<SentRequest> _requests = new();

    public IReadOnlyList<SentRequest> Requests => _requests.ToList();

    public void Reply(HttpStatusCode statusCode, string body) =>
        _answers.Enqueue(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    // E.g. HttpRequestException (network error) or TaskCanceledException (timeout).
    public void Throw(Exception exception) => _answers.Enqueue(() => throw exception);

    public void Reset()
    {
        _answers.Clear();
        _requests.Clear();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new SentRequest(request.Method, request.RequestUri!.ToString(),
            request.Headers.Authorization?.ToString(), body));

        if (!_answers.TryDequeue(out var answer))
            throw new InvalidOperationException($"The fake tracker has no answer queued for {request.RequestUri}.");

        return answer();
    }
}
