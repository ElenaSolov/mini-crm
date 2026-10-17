using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Integration;

// The settings page API, and how an edited template reaches the next conversion.
public class TrackerEndpointsApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const int PurchaseId = 2;

    private static object PurchaseTemplates(
        string? url = null, string? body = null, string? eventId = null) => new
    {
        url_template = url ?? TestData.ConversionsUrl,
        body_template = body ?? TrackerDefaults.PurchaseBody,
        event_id_template = eventId ?? "conv_{lead_id}"
    };

    private Task<HttpResponseMessage> SavePurchase(object templates) =>
        Client.PutAsJsonAsync($"/api/tracker/endpoints/{PurchaseId}", templates, Json);

    [Fact]
    public async Task Lists_the_endpoints_added_from_the_env_defaults()
    {
        var endpoints = (await Client.GetFromJsonAsync<List<TrackerEndpointResponse>>("/api/tracker/endpoints", Json))!;

        Assert.Equal([TrackerDefaults.PingName, TrackerDefaults.PurchaseName], endpoints.Select(e => e.Name));
        var purchase = endpoints[1];
        Assert.Equal("POST", purchase.Method);
        Assert.Equal(LeadStatusNames.Converted, purchase.TriggerStatus);
        Assert.Equal("conv_{lead_id}", purchase.EventIdTemplate);
        Assert.Equal(TestData.ConversionsUrl, purchase.UrlTemplate);
    }

    [Fact]
    public async Task Lists_the_macros()
    {
        var macros = await Client.GetFromJsonAsync<MacroListResponse>("/api/tracker/macros", Json);

        Assert.Contains("email", macros!.Macros);
        Assert.Equal(["lead_id", "event_type"], macros.EventIdMacros);
    }

    [Fact]
    public async Task Invalid_templates_are_400_per_field()
    {
        var response = await SavePurchase(PurchaseTemplates(
            url: "http://tracker.test/c", body: """{"x":"{nope}"}""", eventId: "conv_{email}"));

        Assert.Equal(["body_template", "event_id_template", "url_template"], (await ErrorFields(response)).Order());
    }

    [Fact]
    public async Task Missing_url_is_400()
    {
        var response = await SavePurchase(new { body_template = TrackerDefaults.PurchaseBody, event_id_template = "conv_{lead_id}" });

        Assert.Equal(["url_template"], await ErrorFields(response));
    }

    [Fact]
    public async Task Unknown_endpoint_is_404()
    {
        var response = await Client.PutAsJsonAsync("/api/tracker/endpoints/999", PurchaseTemplates(), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Preview_returns_the_rendered_request()
    {
        var response = await Client.PostAsJsonAsync($"/api/tracker/endpoints/{PurchaseId}/preview",
            PurchaseTemplates(eventId: "purchase_{lead_id}"), Json);

        var preview = await response.Content.ReadFromJsonAsync<EndpointPreview>(Json);
        Assert.Equal("purchase_42", preview!.EventId);
        Assert.Empty(preview.Errors);
        Assert.Equal("purchase_42", JsonDocument.Parse(preview.Body!).RootElement.GetProperty("event_id").GetString());
    }

    [Fact]
    public async Task Preview_of_an_empty_url_is_200_with_the_error()
    {
        var response = await Client.PostAsJsonAsync($"/api/tracker/endpoints/{PurchaseId}/preview",
            PurchaseTemplates(url: ""), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<EndpointPreview>(Json);
        Assert.Equal("The URL is required.", preview!.Errors["url_template"]);
    }

    [Fact]
    public async Task Edited_templates_are_used_by_the_next_conversion()
    {
        var saved = await SavePurchase(PurchaseTemplates(
            url: "https://tracker.test/conversions?source={source}",
            body: """{"event_id":"{event_id}","email":"{email}","name":"{name}"}""",
            eventId: "purchase_{lead_id}"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var lead = await CreateLead(ValidLead("Ann \"A\""));

        await ChangeStatus(lead.Id, LeadStatusNames.Converted);

        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal($"purchase_{lead.Id}", trackerEvent.EventId);
        Assert.Equal("https://tracker.test/conversions?source=facebook", trackerEvent.Url);
        var body = JsonDocument.Parse(trackerEvent.Body!).RootElement;
        Assert.Equal("Ann \"A\"", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Convert_again_after_editing_the_url_sends_to_the_new_url()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, LeadStatusNames.Converted);
        Factory.Tracker.Reply(HttpStatusCode.NotFound, """{"ok":false}""");
        await SendDueEvents();

        await SavePurchase(PurchaseTemplates(url: "https://tracker.test/fixed", eventId: "other_{lead_id}"));
        Factory.Tracker.Reply(HttpStatusCode.Created, """{"ok":true,"duplicate":false}""");
        await ChangeStatus(lead.Id, LeadStatusNames.Converted); // convert again
        await SendDueEvents();

        Assert.Equal(
            [TestData.ConversionsUrl, "https://tracker.test/fixed"],
            Factory.Tracker.Requests.Select(r => r.Url));
        var trackerEvent = Assert.Single(await GetEvents(lead.Id));
        Assert.Equal($"conv_{lead.Id}", trackerEvent.EventId); // the event_id never changes
        Assert.Equal(TrackerEventStatus.Sent, trackerEvent.Status);
    }

    [Fact]
    public async Task Reset_puts_back_the_default_templates()
    {
        await SavePurchase(PurchaseTemplates(url: "https://tracker.test/other", eventId: "other_{lead_id}"));

        var response = await Client.PostAsync($"/api/tracker/endpoints/{PurchaseId}/reset", null);

        var purchase = await response.Content.ReadFromJsonAsync<TrackerEndpointResponse>(Json);
        Assert.Equal(TestData.ConversionsUrl, purchase!.UrlTemplate);
        Assert.Equal("conv_{lead_id}", purchase.EventIdTemplate);
        Assert.Equal(TrackerDefaults.PurchaseBody, purchase.BodyTemplate);
    }
}
