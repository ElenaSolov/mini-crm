using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;

namespace MiniCrm.Tests.Integration;

// Helpers that call the API the way the UI does.
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    // The same JSON settings as the app: snake_case names, enums as strings.
    protected static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    protected readonly ApiFactory Factory = factory;
    protected readonly HttpClient Client = factory.CreateClient();

    public Task InitializeAsync() => Factory.ResetDatabase();
    public Task DisposeAsync() => Task.CompletedTask;

    protected static object ValidLead(string name = "Ann") => new
    {
        name,
        email = "lead@example.com",
        phone = "+12025550123",
        source = "facebook",
        amount = 199.00m,
        currency = "USD"
    };

    protected async Task<LeadResponse> CreateLead(object? body = null)
    {
        var response = await Client.PostAsJsonAsync("/api/leads", body ?? ValidLead(), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!;
    }

    protected Task<HttpResponseMessage> ChangeStatus(int leadId, string status, bool simulateServerError = false) =>
        Client.PostAsJsonAsync($"/api/leads/{leadId}/status",
            new { status, simulate_server_error = simulateServerError }, Json);

    protected async Task<List<TrackerEventResponse>> GetEvents(int leadId) =>
        (await Client.GetFromJsonAsync<List<TrackerEventResponse>>($"/api/leads/{leadId}/events", Json))!;

    // One run of the background worker.
    protected async Task SendDueEvents()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<TrackerEventSender>();
        await sender.SendDueEvents(CancellationToken.None);
    }

    // Skips the backoff wait, so the next run retries pending events now.
    protected async Task MakeRetriesDue()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.TrackerEvents
            .Where(e => e.Status == TrackerEventStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
    }

    protected async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // The field names listed in a 400 ValidationProblem response.
    protected static async Task<List<string>> ErrorFields(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
    }
}
