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

        Assert.Equal(HttpStatusCode.Unauthorized, pixel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postback.StatusCode);
        Assert.Equal(0, await LeadCount());
    }

    [Fact]
    public async Task Postback_with_json_is_415()
    {
        var response = await Client.PostAsJsonAsync($"/api/inbound/postback?token={Token}", ValidLead(), Json);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
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
}
