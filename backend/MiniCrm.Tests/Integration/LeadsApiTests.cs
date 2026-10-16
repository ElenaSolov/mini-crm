using System.Net;
using System.Net.Http.Json;
using MiniCrm.Api.Dtos;

namespace MiniCrm.Tests.Integration;

public class LeadsApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_saves_a_new_lead_with_cleaned_values()
    {
        var lead = await CreateLead(new { name = " Ann ", email = "lead@example.com", source = "Facebook", amount = 10m, currency = "usd" });

        Assert.Equal("Ann", lead.Name);
        Assert.Equal("facebook", lead.Source);
        Assert.Equal("USD", lead.Currency);
        Assert.Equal("new", lead.Status);
        Assert.NotEqual(default, lead.CreatedAt);

        var loaded = await Client.GetFromJsonAsync<LeadResponse>($"/api/leads/{lead.Id}", Json);
        Assert.Equal(lead.Id, loaded!.Id);
    }

    [Fact]
    public async Task Create_without_email_and_phone_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/leads", new { name = "Ann" }, Json);

        Assert.Contains("email", await ErrorFields(response));
    }

    [Fact]
    public async Task Create_with_amount_but_no_currency_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/leads", new { name = "Ann", email = "a@b.com", amount = 5m }, Json);

        Assert.Contains("currency", await ErrorFields(response));
    }

    [Fact]
    public async Task List_filters_by_status_and_source()
    {
        var facebookNew = await CreateLead(new { name = "A", email = "a@x.com", source = "facebook" });
        var googleNew = await CreateLead(new { name = "B", email = "b@x.com", source = "google" });
        var facebookLost = await CreateLead(new { name = "C", email = "c@x.com", source = "facebook" });
        await ChangeStatus(facebookLost.Id, "lost");

        var byStatus = await Client.GetFromJsonAsync<List<LeadResponse>>("/api/leads?status=new", Json);
        var bySource = await Client.GetFromJsonAsync<List<LeadResponse>>("/api/leads?source=Facebook", Json);
        var byBoth = await Client.GetFromJsonAsync<List<LeadResponse>>("/api/leads?status=lost&source=facebook", Json);

        // Sorted, so the test doesn't depend on created_at order of leads made microseconds apart.
        Assert.Equal(new[] { facebookNew.Id, googleNew.Id }.Order(), byStatus!.Select(l => l.Id).Order());
        Assert.Equal(new[] { facebookNew.Id, facebookLost.Id }.Order(), bySource!.Select(l => l.Id).Order());
        Assert.Equal([facebookLost.Id], byBoth!.Select(l => l.Id));
    }

    [Fact]
    public async Task Update_cannot_change_the_status()
    {
        var lead = await CreateLead();

        var response = await Client.PutAsJsonAsync($"/api/leads/{lead.Id}",
            new { name = "Ann B", email = "lead@example.com", status = "converted" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<LeadResponse>(Json);
        Assert.Equal("Ann B", updated!.Name);
        Assert.Equal("new", updated.Status);
        Assert.Empty(Factory.Tracker.Requests);
    }

    [Fact]
    public async Task Unknown_lead_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/leads/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync("/api/leads/999999", ValidLead(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync("/api/leads/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeStatus(999999, "qualified")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/leads/999999/events")).StatusCode);
    }

    [Fact]
    public async Task Delete_removes_a_lead_without_events()
    {
        var lead = await CreateLead();

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/leads/{lead.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/leads/{lead.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_of_a_converted_lead_is_409()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/leads/{lead.Id}")).StatusCode);
    }

    [Fact]
    public async Task Converting_a_lost_lead_is_409()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "lost");

        var response = await ChangeStatus(lead.Id, "converted");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("lost", await response.Content.ReadAsStringAsync());
        Assert.Empty(await GetEvents(lead.Id));
    }

    [Fact]
    public async Task Converting_without_amount_is_409()
    {
        var lead = await CreateLead(new { name = "Ann", email = "lead@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, (await ChangeStatus(lead.Id, "converted")).StatusCode);
        Assert.Empty(await GetEvents(lead.Id));
    }

    [Fact]
    public async Task Converted_lead_cannot_change_status_again()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "converted");

        Assert.Equal(HttpStatusCode.Conflict, (await ChangeStatus(lead.Id, "lost")).StatusCode);
    }

    [Fact]
    public async Task Status_change_without_status_is_400()
    {
        var lead = await CreateLead();

        var response = await Client.PostAsJsonAsync($"/api/leads/{lead.Id}/status", new { }, Json);

        Assert.Contains("status", await ErrorFields(response));
    }
}
