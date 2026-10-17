using System.Text;

namespace MiniCrm.Api.Services;

// What the tracker answered. StatusCode is null when there was no answer at all
// (timeout or network error); Error then says why.
public record TrackerResponse(int? StatusCode, string? Body, string? Error);

// Sends one request to the tracker with the API key. It never throws for
// network problems, so callers (the ping endpoint and TrackerEventSender) can store
// or show the outcome.
public class TrackerClient(HttpClient http, TrackerAuthService auth)
{
    public async Task<TrackerResponse> Send(HttpMethod method, string url, string? jsonBody = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, url);

        // The shared auth header with the key filled in (settings page, or .env). It is never
        // logged or saved with the attempt. The name and value were checked on save, so they can't
        // break the request. Without a key the tracker answers 401, which we pass on as is.
        if (await auth.CurrentHeader(cancellationToken) is { } header)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TrackerResponse((int)response.StatusCode, body, null);
        }
        catch (HttpRequestException ex)
        {
            return new TrackerResponse(null, null, $"Network error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancelled task.
            return new TrackerResponse(null, null, $"No answer within {http.Timeout.TotalSeconds} seconds.");
        }
    }
}
