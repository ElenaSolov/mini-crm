using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniCrm.Api.Data;
using MiniCrm.Api.Dtos;

namespace MiniCrm.Tests.Integration;

// The pixel and postback URLs that the client puts in their own website form.
public class InboundApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    // The random token the app created at startup.
    private readonly string Token = ReadToken(factory);

    private static string ReadToken(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().InboundSettings.Single().Token;
    }

    private Task<HttpResponseMessage> Postback(string token, Dictionary<string, string> fields) =>
        Client.PostAsync($"/api/inbound/postback?token={token}", new FormUrlEncodedContent(fields));

    private async Task<int> LeadCount()
    {
        var count = 0;
        await WithDb(async db => count = await db.Leads.CountAsync());
        return count;
    }

    [Fact]
    public async Task Pixel_creates_a_lead_and_returns_an_uncached_png()
    {
        var response = await Client.GetAsync(
            $"/api/inbound/pixel.png?token={Token}&name=Ann&email=ann%2Btest%40example.com&amount=199.50&currency=usd");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var png = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);

        await WithDb(async db =>
        {
            var lead = await db.Leads.SingleAsync();
            Assert.Equal("Ann", lead.Name);
            Assert.Equal("ann+test@example.com", lead.Email);
            Assert.Equal("pixel", lead.Source);
            Assert.Equal("new", lead.Status);
            Assert.Equal(199.50m, lead.Amount);
            Assert.Equal("USD", lead.Currency);
        });
    }

    [Fact]
    public async Task Empty_fields_in_the_copied_pixel_url_are_saved_as_empty()
    {
        var response = await Client.GetAsync(
            $"/api/inbound/pixel.png?token={Token}&name=Ann&email=ann@example.com&phone=&source=&amount=&currency=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WithDb(async db =>
        {
            var lead = await db.Leads.SingleAsync();
            Assert.Null(lead.Phone);
            Assert.Null(lead.Amount);
            Assert.Null(lead.Currency);
            Assert.Equal("pixel", lead.Source);
        });
    }

    [Fact]
    public async Task Postback_creates_a_lead_from_form_fields()
    {
        var response = await Postback(Token, new() { ["name"] = "Bob", ["phone"] = "+12025550123", ["source"] = "Landing" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lead = (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!;
        Assert.Equal($"/api/leads/{lead.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Bob", lead.Name);
        Assert.Equal("landing", lead.Source);
    }

    [Fact]
    public async Task Postback_without_a_source_is_marked_postback()
    {
        var response = await Postback(Token, new() { ["name"] = "Bob", ["email"] = "bob@example.com" });

        Assert.Equal("postback", (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!.Source);
    }

    [Fact]
    public async Task Postback_reads_query_parameters_without_a_form()
    {
        var response = await Client.PostAsync(
            $"/api/inbound/postback?token={Token}&name=Bob&email=bob%40example.com&amount=10&currency=usd", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lead = (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!;
        Assert.Equal("Bob", lead.Name);
        Assert.Equal("bob@example.com", lead.Email);
        Assert.Equal("USD", lead.Currency);
    }

    [Fact]
    public async Task Postback_mixes_query_and_form_and_the_form_wins()
    {
        var response = await Client.PostAsync(
            $"/api/inbound/postback?token={Token}&name=FromQuery&source=ads",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["name"] = "FromForm", ["email"] = "bob@example.com" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lead = (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!;
        Assert.Equal("FromForm", lead.Name);
        Assert.Equal("ads", lead.Source);
    }

    [Fact]
    public async Task Postback_with_bad_query_parameters_is_a_400()
    {
        var response = await Client.PostAsync($"/api/inbound/postback?token={Token}&name=Ann&email=not-an-email", null);

        Assert.Contains("email", await ErrorFields(response), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, await LeadCount());
    }

    [Fact]
    public async Task Bad_fields_are_a_400_and_create_nothing()
    {
        var pixel = await Client.GetAsync($"/api/inbound/pixel.png?token={Token}&name=Ann");
        var postback = await Postback(Token, new() { ["name"] = "Ann", ["email"] = "not-an-email" });

        // Form and query errors use the C# name ("Email"), JSON errors the JSON name ("email").
        Assert.Contains("email", await ErrorFields(pixel), StringComparer.OrdinalIgnoreCase);
        Assert.Contains("email", await ErrorFields(postback), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, await LeadCount());
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong")]
    public async Task An_empty_or_wrong_token_is_a_401_and_creates_nothing(string token)
    {
        // The fields are also bad: the token is checked first, so the answer says nothing about them.
        var pixel = await Client.GetAsync($"/api/inbound/pixel.png?token={token}&name=Ann");
        var postback = await Postback(token, new() { ["name"] = "Ann" });
        var postbackGet = await Client.GetAsync($"/api/inbound/postback?token={token}&name=Ann");

        Assert.Equal(HttpStatusCode.Unauthorized, pixel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postback.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postbackGet.StatusCode);
        Assert.Equal(0, await LeadCount());
    }

    [Fact]
    public async Task Postback_with_json_is_415()
    {
        var response = await Client.PostAsJsonAsync($"/api/inbound/postback?token={Token}", ValidLead(), Json);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Contains("form fields", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Postback_with_get_creates_a_lead_from_query_parameters()
    {
        var response = await Client.GetAsync($"/api/inbound/postback?token={Token}&name=Ann&email=ann%40example.com");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lead = (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!;
        Assert.Equal("Ann", lead.Name);
        Assert.Equal("postback", lead.Source);
    }

    [Fact]
    public async Task Postback_with_get_and_bad_fields_is_a_400()
    {
        // "abc" can't be read as a number at all, so this also covers that kind of error.
        var response = await Client.GetAsync($"/api/inbound/postback?token={Token}&email=test&amount=abc");

        var fields = await ErrorFields(response);
        Assert.Contains("name", fields, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("email", fields, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("amount", fields, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, await LeadCount());
    }

    [Fact]
    public async Task A_status_field_is_ignored_so_the_lead_is_always_new()
    {
        await Postback(Token, new() { ["name"] = "Ann", ["email"] = "ann@example.com", ["status"] = "converted" });

        await WithDb(async db => Assert.Equal("new", (await db.Leads.SingleAsync()).Status));
    }

    [Fact]
    public async Task Settings_return_the_generated_token_and_the_fields()
    {
        var settings = await Client.GetFromJsonAsync<InboundSettingsResponse>("/api/inbound/settings", Json);

        Assert.Equal(64, Token.Length);
        Assert.Equal(Token, settings!.Token);
        Assert.Equal(["name", "email", "phone", "source", "amount", "currency"], settings.Fields);
    }

    // The token row is not reset between tests, so put the startup token back.
    private Task RestoreToken() =>
        WithDb(db => db.InboundSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.Token, Token)));

    [Fact]
    public async Task A_custom_token_replaces_the_old_one()
    {
        try
        {
            var response = await Client.PutAsJsonAsync("/api/inbound/token", new { token = "my-shop_token-2026" }, Json);
            var settings = await response.Content.ReadFromJsonAsync<InboundSettingsResponse>(Json);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("my-shop_token-2026", settings!.Token);
            Assert.Equal(HttpStatusCode.Created, (await Postback("my-shop_token-2026", new() { ["name"] = "Ann", ["email"] = "ann@example.com" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Postback(Token, new() { ["name"] = "Ann", ["email"] = "ann@example.com" })).StatusCode);
        }
        finally
        {
            await RestoreToken();
        }
    }

    // One shared row: parallel saves must all succeed, and the last one wins.
    [Fact]
    public async Task Parallel_token_saves_all_succeed_and_one_value_is_kept()
    {
        try
        {
            var tokens = Enumerable.Range(1, 5).Select(i => $"parallel-token-{i:D4}").ToList();

            var responses = await Task.WhenAll(tokens.Select(t => Client.PutAsJsonAsync("/api/inbound/token", new { token = t }, Json)));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            await WithDb(async db => Assert.Contains((await db.InboundSettings.SingleAsync()).Token, tokens));
        }
        finally
        {
            await RestoreToken();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]                        // under 16 characters, easy to guess
    [InlineData("has spaces in the token")]
    [InlineData("has&ampersand=and-equals")]     // would break the URL
    public async Task A_bad_custom_token_is_400_and_the_old_one_still_works(string token)
    {
        var response = await Client.PutAsJsonAsync("/api/inbound/token", new { token }, Json);

        Assert.Contains("token", await ErrorFields(response), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Created, (await Postback(Token, new() { ["name"] = "Ann", ["email"] = "ann@example.com" })).StatusCode);
    }

    [Fact]
    public async Task Generate_makes_a_new_random_token()
    {
        try
        {
            var response = await Client.PostAsync("/api/inbound/token/generate", null);
            var settings = await response.Content.ReadFromJsonAsync<InboundSettingsResponse>(Json);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(64, settings!.Token!.Length);
            Assert.NotEqual(Token, settings.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Postback(Token, new() { ["name"] = "Ann", ["email"] = "ann@example.com" })).StatusCode);
        }
        finally
        {
            await RestoreToken();
        }
    }
}
