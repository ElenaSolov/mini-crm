using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// Fills {macro} placeholders in endpoint templates with values from a lead.
// Each place escapes values its own way, so a name like  Ann "A" & B  can't
// break the URL or the JSON.
public static class MacroRenderer
{
    // {lead_id}, {email}, ... Lower case letters and underscores only, so JSON braces never match.
    private static readonly Regex MacroPattern = new(@"\{([a-z_]+)\}");

    // Every macro a URL or body template can use: the keys of ValuesFor, plus event_id.
    public static readonly string[] AllMacros =
        ["event_id", "lead_id", "event_type", "name", "email", "phone", "source", "amount", "currency", "occurred_at"];

    // Values that never change for a lead's event, so the event_id stays the same
    // even after the lead's email or name is edited.
    public static readonly string[] EventIdMacros = ["lead_id", "event_type"];

    // The macro names used in a template, e.g. ["lead_id", "email"].
    public static IEnumerable<string> MacrosIn(string template) =>
        MacroPattern.Matches(template).Select(match => match.Groups[1].Value).Distinct();

    public static Dictionary<string, string> ValuesFor(Lead lead, string eventType, DateTimeOffset occurredAt) => new()
    {
        ["lead_id"] = lead.Id.ToString(CultureInfo.InvariantCulture),
        ["event_type"] = eventType,
        ["name"] = lead.Name,
        ["email"] = lead.Email ?? "",
        ["phone"] = lead.Phone ?? "",
        ["source"] = lead.Source ?? "",
        ["amount"] = lead.Amount?.ToString(CultureInfo.InvariantCulture) ?? "",
        ["currency"] = lead.Currency ?? "",
        ["occurred_at"] = occurredAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)
    };

    public static string RenderUrl(string template, IReadOnlyDictionary<string, string> values) =>
        Render(template, values, Uri.EscapeDataString);

    public static string RenderBody(string template, IReadOnlyDictionary<string, string> values) =>
        Render(template, values, JsonEscape);

    // The event_id goes into the body later, where it gets JSON-escaped.
    public static string RenderEventId(string template, IReadOnlyDictionary<string, string> values) =>
        Render(template, values, value => value);

    public static bool IsValidJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Unknown macros are left as they are; TrackerEndpointService rejects them on save.
    private static string Render(string template, IReadOnlyDictionary<string, string> values, Func<string, string> escape) =>
        MacroPattern.Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) ? escape(value) : match.Value);

    // Escapes only what JSON requires (quotes, backslashes, control characters).
    // The default encoder would also turn "+" in a phone number into +.
    private static string JsonEscape(string value) =>
        JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
}
