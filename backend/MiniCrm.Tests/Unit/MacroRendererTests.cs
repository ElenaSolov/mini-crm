using System.Globalization;
using System.Text.Json;
using MiniCrm.Api.Data;
using MiniCrm.Api.Models;
using MiniCrm.Api.Services;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

public class MacroRendererTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 2, 14, 0, 0, TimeSpan.FromHours(3));

    private static Dictionary<string, string> Values(Action<Lead>? change = null)
    {
        var lead = TestData.ConvertibleLead();
        change?.Invoke(lead);
        var values = MacroRenderer.ValuesFor(lead, "purchase", OccurredAt);
        values["event_id"] = "conv_42";
        return values;
    }

    [Fact]
    public void Default_purchase_body_renders_the_tasks_json()
    {
        var body = MacroRenderer.RenderBody(DefaultEndpoints.PurchaseBody, Values());

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("conv_42", json.GetProperty("event_id").GetString());
        Assert.Equal("purchase", json.GetProperty("event_name").GetString());
        Assert.Equal("lead@example.com", json.GetProperty("email").GetString());
        Assert.Equal("+12025550123", json.GetProperty("phone").GetString());
        Assert.Equal("42", json.GetProperty("lead_id").GetString());
        Assert.Equal(JsonValueKind.Number, json.GetProperty("amount").ValueKind);
        Assert.Equal(199.00m, json.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", json.GetProperty("currency").GetString());
        Assert.Equal("2026-09-02T14:00:00+03:00", json.GetProperty("occurred_at").GetString());
    }

    [Fact]
    public void Body_keeps_plus_in_phone_as_is()
    {
        var body = MacroRenderer.RenderBody("""{"phone":"{phone}"}""", Values());

        Assert.Equal("""{"phone":"+12025550123"}""", body);
    }

    [Fact]
    public void Body_escapes_quotes_and_backslashes()
    {
        const string trickyName = """Ann "A" \ B""";

        var body = MacroRenderer.RenderBody("""{"name":"{name}"}""", Values(l => l.Name = trickyName));

        Assert.True(MacroRenderer.IsValidJson(body));
        Assert.Equal(trickyName, JsonDocument.Parse(body).RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Body_cannot_be_broken_by_injected_json()
    {
        var body = MacroRenderer.RenderBody("""{"name":"{name}"}""",
            Values(l => l.Name = """x","amount":0,"y":"""));

        var json = JsonDocument.Parse(body).RootElement;
        Assert.False(json.TryGetProperty("amount", out _));
    }

    [Fact]
    public void Url_encodes_values()
    {
        var url = MacroRenderer.RenderUrl("https://tracker.test/c?email={email}&name={name}",
            Values(l => { l.Email = "a+b@x.com"; l.Name = "Ann & Bob"; }));

        Assert.Equal("https://tracker.test/c?email=a%2Bb%40x.com&name=Ann%20%26%20Bob", url);
    }

    [Fact]
    public void Missing_value_becomes_empty_string()
    {
        var body = MacroRenderer.RenderBody("""{"phone":"{phone}"}""", Values(l => l.Phone = null));

        Assert.Equal("""{"phone":""}""", body);
    }

    [Fact]
    public void Unknown_macro_is_left_as_is()
    {
        var body = MacroRenderer.RenderBody("""{"x":"{unknown}"}""", Values());

        Assert.Equal("""{"x":"{unknown}"}""", body);
    }

    [Fact]
    public void Event_id_uses_lead_id()
    {
        Assert.Equal("conv_42", MacroRenderer.RenderEventId("conv_{lead_id}", Values()));
    }

    [Fact]
    public void Amount_uses_a_dot_on_any_machine_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // writes 199,5 by default

            var body = MacroRenderer.RenderBody("""{"amount":{amount}}""", Values(l => l.Amount = 199.5m));

            Assert.Equal("""{"amount":199.5}""", body);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("""{"a":1}""", true)]
    [InlineData("""{"amount":}""", false)]
    [InlineData("not json", false)]
    public void IsValidJson_checks_the_text(string text, bool expected)
    {
        Assert.Equal(expected, MacroRenderer.IsValidJson(text));
    }
}
