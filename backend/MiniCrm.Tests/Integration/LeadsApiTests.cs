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

    // The tracker would answer 422 for this email, so the CRM rejects it up front.
    [Fact]
    public async Task Create_with_space_in_email_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/leads", new { name = "Ann", email = "a b@c.com" }, Json);

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

        var byStatus = await ListLeadIds("status=new");
        var bySource = await ListLeadIds("source=Facebook");
        var byBoth = await ListLeadIds("status=lost&source=facebook");

        // Sorted, so the test doesn't depend on created_at order of leads made microseconds apart.
        Assert.Equal(new[] { facebookNew.Id, googleNew.Id }.Order(), byStatus.Order());
        Assert.Equal(new[] { facebookNew.Id, facebookLost.Id }.Order(), bySource.Order());
        Assert.Equal([facebookLost.Id], byBoth);
    }

    [Fact]
    public async Task Text_filters_match_part_of_the_value_and_ignore_case()
    {
        var joanna = await CreateLead(new { name = "Joanna", email = "joanna@mail.com", phone = "+12025550123", source = "facebook", amount = 10m, currency = "USD" });
        var bob = await CreateLead(new { name = "Bob", email = "bob@work.org", phone = "+44700900", source = "google", amount = 10m, currency = "EUR" });

        Assert.Equal([joanna.Id], await ListLeadIds("name=ANN"));
        Assert.Equal([bob.Id], await ListLeadIds("email=work"));
        Assert.Equal([bob.Id], await ListLeadIds("phone=4470"));
        Assert.Equal([joanna.Id], await ListLeadIds("source=Face"));
        Assert.Equal([joanna.Id], await ListLeadIds("name=ann&source=book"));
    }

    [Fact]
    public async Task Currency_filter_takes_several_codes_and_matches_exactly()
    {
        var usd = await CreateLead(new { name = "A", email = "a@x.com", amount = 10m, currency = "USD" });
        var eur = await CreateLead(new { name = "B", email = "b@x.com", amount = 10m, currency = "EUR" });
        await CreateLead(new { name = "C", email = "c@x.com", amount = 10m, currency = "ILS" });

        Assert.Equal(new[] { usd.Id, eur.Id }.Order(), (await ListLeadIds("currency=usd,EUR")).Order());
        Assert.Equal([eur.Id], await ListLeadIds("currency=EUR"));
        Assert.Empty(await ListLeadIds("currency=EU"));
    }

    [Fact]
    public async Task Default_page_size_is_10()
    {
        var page = await ListLeads("");

        Assert.Equal(10, page.PageSize);
    }

    [Fact]
    public async Task Like_wildcards_in_a_filter_are_plain_characters()
    {
        await CreateLead(new { name = "Ann", email = "a@x.com" });
        var percent = await CreateLead(new { name = "100% sure", email = "b@x.com" });

        // In SQL LIKE, "%" and "_" mean "anything". Here they must only find themselves.
        Assert.Equal([percent.Id], await ListLeadIds("name=%25"));
        Assert.Empty(await ListLeadIds("name=_"));
    }

    [Fact]
    public async Task Id_amount_and_created_filters()
    {
        var cheap = await CreateLead(new { name = "A", email = "a@x.com", amount = 10m, currency = "USD" });
        var expensive = await CreateLead(new { name = "B", email = "b@x.com", amount = 500m, currency = "USD" });
        // "+" in a query string means a space, so the offset is sent as %2B.
        string Moment(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));
        var hourAgo = Moment(cheap.CreatedAt.AddHours(-1));
        var hourLater = Moment(expensive.CreatedAt.AddHours(1));

        Assert.Equal([cheap.Id], await ListLeadIds($"id={cheap.Id}"));
        Assert.Equal([expensive.Id], await ListLeadIds("amount_from=100"));
        Assert.Equal([cheap.Id], await ListLeadIds("amount_from=10&amount_to=10"));
        Assert.Equal(2, (await ListLeadIds($"created_from={hourAgo}&created_to={hourLater}")).Count);
        Assert.Empty(await ListLeadIds($"created_from={hourLater}"));
        Assert.Empty(await ListLeadIds($"created_to={hourAgo}"));
    }

    [Fact]
    public async Task List_is_paged_newest_first()
    {
        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
            ids.Add((await CreateLead()).Id);

        var page1 = await ListLeads("page=1&page_size=2");
        var page3 = await ListLeads("page=3&page_size=2");
        var page4 = await ListLeads("page=4&page_size=2");

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal([ids[4], ids[3]], page1.Items.Select(l => l.Id));
        Assert.Equal([ids[0]], page3.Items.Select(l => l.Id));
        Assert.Empty(page4.Items);
        Assert.Equal(5, page4.TotalCount);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page_size=0", "page_size")]
    [InlineData("page_size=101", "page_size")]
    [InlineData("id=abc", "id")]
    [InlineData("amount_to=-1", "amount_to")]
    [InlineData("created_from=yesterday", "created_from")]
    public async Task Bad_list_query_is_400(string query, string field)
    {
        var response = await Client.GetAsync($"/api/leads?{query}");

        Assert.Contains(field, await ErrorFields(response));
    }

    // Extreme values must give a clean answer, never a 500.
    [Theory]
    [InlineData("page=2147483647")]
    [InlineData("created_to=9999-12-31T23:59:59Z")]
    [InlineData("created_from=0001-01-01T00:00:00%2B05:00")]
    [InlineData("amount_from=100000000000000000000")]
    public async Task Extreme_list_query_is_not_500(string query)
    {
        var response = await Client.GetAsync($"/api/leads?{query}");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private async Task<LeadPageResponse> ListLeads(string query) =>
        (await Client.GetFromJsonAsync<LeadPageResponse>($"/api/leads?{query}", Json))!;

    private async Task<List<int>> ListLeadIds(string query) =>
        (await ListLeads(query)).Items.Select(l => l.Id).ToList();

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
    public async Task Lost_lead_cannot_be_converted_through_another_status()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "lost");

        Assert.Equal(HttpStatusCode.Conflict, (await ChangeStatus(lead.Id, "contacted")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ChangeStatus(lead.Id, "converted")).StatusCode);
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
